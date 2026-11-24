// Serviços simulados que o agente chama: caixa de entrada, LLM, estoque, média do
// cliente, ERP, fila de revisão e e-mail. Cada um tem um modo de falha que se liga
// por POST /controle/{servico}/{modo}, para provocar no vídeo cada tipo de falha.
using System.Collections.Concurrent;
using System.Text.RegularExpressions;

var builder = WebApplication.CreateBuilder(args);
builder.WebHost.UseUrls(Environment.GetEnvironmentVariable("SIMULADORES_URL") ?? "http://localhost:5080");
var app = builder.Build();

var estado = new Estado();  // trocado inteiro pelo /controle/reset

// ---------------------------------------------------------------- controle

// Modos do LLM: normal, fora (503), sobrecarga (529), limite (429 com retry-after),
// sku-invalido e quantidade-errada. Modos do e-mail: normal e timeout.
app.MapPost("/controle/{servico}/{modo}", (string servico, string modo) =>
{
    switch (servico)
    {
        case "llm" when Estado.ModosLlm.Contains(modo):
            estado.ModoLlm = modo;
            break;
        case "email" when Estado.ModosEmail.Contains(modo):
            estado.ModoEmail = modo;
            break;
        default:
            return Results.BadRequest(new { erro = $"Serviço ou modo desconhecido: {servico}/{modo}" });
    }
    app.Logger.LogWarning("Modo de {Servico} agora é {Modo}", servico, modo);
    return Results.Ok(new { llm = estado.ModoLlm, email = estado.ModoEmail });
});

// Zera o estado entre uma tomada e outra da gravação.
app.MapPost("/controle/reset", () =>
{
    estado = new Estado();
    app.Logger.LogWarning("Estado dos simuladores zerado");
    return Results.Ok();
});

app.MapGet("/controle", () => Results.Ok(new
{
    llm = estado.ModoLlm,
    email = estado.ModoEmail,
    chamadasLlm = estado.ChamadasLlm,
    pedidosNoErp = estado.PedidosErp.Count,
    emails = estado.Emails.Count,
    revisoes = estado.Revisoes.Count,
    quedas = estado.Quedas
}));

// ---------------------------------------------------------------- caixa de entrada

// Qualquer id gera um pedido. O texto pede 100 caixas do SKU 10021 e 40 do 10035.
app.MapGet("/caixa/{pedidoId}", (string pedidoId) =>
    Results.Ok(new
    {
        pedidoId,
        cliente = "distribuidora-norte",
        texto = $"Bom dia! Segue o pedido {pedidoId}: 100 caixas do SKU 10021 e 40 caixas do SKU 10035. Entrega na filial Centro."
    }));

// ---------------------------------------------------------------- LLM

app.MapPost("/llm/extrair", (TextoPedido entrada, HttpContext ctx) =>
{
    Interlocked.Increment(ref estado.ChamadasLlm);

    switch (estado.ModoLlm)
    {
        case "fora":
            return Results.Json(new { type = "error", error = new { type = "api_error", message = "Service Unavailable" } },
                statusCode: 503);
        case "sobrecarga":
            return Results.Json(new { type = "error", error = new { type = "overloaded_error", message = "Overloaded" } },
                statusCode: 529);
        case "limite":
            ctx.Response.Headers["retry-after"] = "30";
            return Results.Json(new { type = "error", error = new { type = "rate_limit_error", message = "Rate limit exceeded" } },
                statusCode: 429);
    }

    var itens = Regex.Matches(entrada.Texto, @"(\d+) caixas? do SKU (\S+?)[\s,.]")
        .Select(m => new Item(m.Groups[2].Value, int.Parse(m.Groups[1].Value)))
        .ToList();

    if (estado.ModoLlm == "sku-invalido" && itens.Count > 0)
        itens[0] = itens[0] with { Sku = "88412-X" };

    if (estado.ModoLlm == "quantidade-errada" && itens.Count > 0)
        itens[0] = itens[0] with { Quantidade = itens[0].Quantidade * 10 };

    return Results.Ok(itens);
});

// ---------------------------------------------------------------- estoque e cliente

var cadastro = new Dictionary<string, int> { ["10021"] = 2400, ["10022"] = 800, ["10035"] = 950 };

app.MapGet("/estoque/{sku}", (string sku) =>
    cadastro.TryGetValue(sku, out var saldo)
        ? Results.Ok(new { sku, existe = true, saldo })
        : Results.NotFound(new { sku, existe = false, saldo = 0 }));

app.MapGet("/clientes/{cliente}/media", (string cliente) =>
    Results.Ok(new { cliente, mediaPorItem = 80 }));

// ---------------------------------------------------------------- ERP

// Idempotente pelo header Idempotency-Key: a mesma chave devolve o pedido que já existe.
// Sem a chave, cada chamada cria um pedido novo (é assim que o pedido duplica).
app.MapPost("/erp/pedidos", (NovoPedido entrada, HttpContext ctx) =>
{
    if (string.IsNullOrWhiteSpace(entrada.PedidoId) || entrada.Itens is null || entrada.Itens.Count == 0)
        return Results.BadRequest(new { erro = "pedidoId e itens são obrigatórios" });

    var chave = ctx.Request.Headers["Idempotency-Key"].FirstOrDefault();
    if (chave is null)
        return Results.Created("/erp/pedidos", estado.Criar(entrada));

    var criadoAgora = false;
    var pedido = estado.PorChave.GetOrAdd(chave, _ => { criadoAgora = true; return estado.Criar(entrada); });
    if (criadoAgora)
        app.Logger.LogInformation("ERP: pedido {Numero} criado para {PedidoId}", pedido.Numero, pedido.PedidoId);
    return criadoAgora ? Results.Created($"/erp/pedidos/{pedido.Numero}", pedido) : Results.Ok(pedido);
});

app.MapGet("/erp/pedidos", () => Results.Ok(estado.PedidosErp.OrderBy(p => p.Numero)));

// ---------------------------------------------------------------- revisão e e-mail

app.MapPost("/revisoes", (PedidoEmRevisao entrada) =>
{
    estado.Revisoes.Add(entrada with { Em = DateTime.UtcNow });
    app.Logger.LogWarning("Revisão pedida para {PedidoId}: {Motivo}", entrada.PedidoId, entrada.Motivo);
    return Results.Accepted();
});

app.MapGet("/revisoes", () => Results.Ok(estado.Revisoes.OrderBy(r => r.Em)));

app.MapPost("/email/enviar", async (Email entrada, HttpContext ctx) =>
{
    if (estado.ModoEmail == "derrubar")
    {
        // O agente encerra o próprio processo ao ver este header (SimulacaoDeQuedaHandler).
        Interlocked.Increment(ref estado.Quedas);
        app.Logger.LogWarning("E-mail de {PedidoId}: derrubando a aplicação (queda {N})", entrada.PedidoId, estado.Quedas);
        ctx.Response.Headers["X-Simulador-Derrubar"] = "1";
        return Results.StatusCode(503);
    }
    if (estado.ModoEmail == "timeout")
    {
        // O agente usa timeout de 5 s; o simulador demora 15 s para responder.
        await Task.Delay(TimeSpan.FromSeconds(15));
        return Results.StatusCode(504);
    }
    estado.Emails.Add(entrada with { Em = DateTime.UtcNow });
    app.Logger.LogInformation("E-mail para {PedidoId}: {Assunto}", entrada.PedidoId, entrada.Assunto);
    return Results.Ok();
});

// Experimentos de DLQ: conta quantas vezes cada passo foi executado.
var contagens = new ConcurrentDictionary<string, int>();
app.MapGet("/experimentos/{id}/passo", (string id) =>
{
    var n = contagens.AddOrUpdate(id, 1, (_, v) => v + 1);
    app.Logger.LogWarning("Experimento {Id}: execução {N}", id, n);
    return Results.Ok(n);
});
app.MapGet("/experimentos", () => Results.Ok(contagens));

app.MapGet("/emails", () => Results.Ok(estado.Emails.OrderBy(e => e.Em)));

app.Run();

record TextoPedido(string Texto);
record Item(string Sku, int Quantidade);
record NovoPedido(string PedidoId, List<Item> Itens);
record PedidoErp(int Numero, string PedidoId, List<Item> Itens);
record PedidoEmRevisao(string PedidoId, string Motivo, DateTime Em = default);
record Email(string PedidoId, string Assunto, string Corpo, DateTime Em = default);

class Estado
{
    public static readonly HashSet<string> ModosLlm = ["normal", "fora", "sobrecarga", "limite", "sku-invalido", "quantidade-errada"];
    public static readonly HashSet<string> ModosEmail = ["normal", "timeout", "derrubar"];

    public volatile string ModoLlm = "normal";
    public volatile string ModoEmail = "normal";
    public int ChamadasLlm;
    public int ProximoNumero = 4710;
    public int Quedas;
    public readonly ConcurrentBag<PedidoErp> PedidosErp = [];
    public readonly ConcurrentDictionary<string, PedidoErp> PorChave = new();
    public readonly ConcurrentBag<PedidoEmRevisao> Revisoes = [];
    public readonly ConcurrentBag<Email> Emails = [];

    public PedidoErp Criar(NovoPedido entrada)
    {
        var criado = new PedidoErp(Interlocked.Increment(ref ProximoNumero), entrada.PedidoId, entrada.Itens);
        PedidosErp.Add(criado);
        return criado;
    }
}
