using System.Net;
using System.Net.Http.Json;
using Skail.Platform.Runtime;
using Skail.Platform.Runtime.Threading;

namespace AgenteDePedidos;

/// <summary>
/// Agente que lê um pedido recebido por e-mail, extrai os itens com o LLM,
/// confere estoque e regra de negócio, grava no ERP e responde o cliente.
///
/// ProcessarPedido trata cada tipo de falha do vídeo do jeito certo.
/// ProcessarPedidoRetryPadrao é a versão com o retry padrão em tudo, usada para
/// mostrar no Monitor o que vai parar na dead letter queue sem precisar.
/// </summary>
public class AgenteDePedidosFunctions
{
    // Bloco 1: espera entre tentativas quando o provedor do LLM está fora,
    // e limite em tempo (60 esperas de 2 minutos = 2 horas) antes de desistir.
    private static readonly TimeSpan EsperaProvedor = TimeSpan.FromMinutes(2);
    private const int MaxEsperasProvedor = 60;

    // Blocos 2 e 4: prazo para alguém revisar um pedido suspeito.
    private static readonly TimeSpan PrazoRevisao = TimeSpan.FromHours(24);

    private readonly IHttpClientFactory _http;

    public AgenteDePedidosFunctions(IHttpClientFactory http)
    {
        _http = http;
    }

    private HttpClient Simuladores => _http.CreateClient("simuladores");

    // ------------------------------------------------------------------
    // Versão que trata cada tipo de falha
    // ------------------------------------------------------------------

    [SkailFunction]
    public async SkailTask<int> ProcessarPedido(string pedidoId)
    {
        var pedido = await LerPedido(pedidoId);

        // Bloco 1: a falha do provedor não gasta o retry do command.
        // O command tem retryCount 0, a exceção chega aqui na primeira falha,
        // e a function espera com o delay durável antes de tentar de novo.
        List<ItemPedido>? itens = null;
        for (var espera = 0; itens is null; espera++)
        {
            try
            {
                itens = await ExtrairItensComLlm(pedido.Texto);
            }
            catch (ProvedorIndisponivelException) when (espera < MaxEsperasProvedor)
            {
                await SkailTask.Delay(EsperaProvedor);
            }
        }

        var saldos = await ConsultarEstoque(itens);

        // Blocos 2 e 4: falha de negócio volta como resultado, sem exceção.
        var validacao = await ValidarPedido(pedido.Cliente, itens, saldos);
        if (!validacao.Valido)
        {
            await AvisarRevisao(pedidoId, validacao.Motivo);

            var revisao = SkailTask.WaitForEvent<Revisao>(Eventos.PedidoRevisado, pedidoId);
            var prazo = SkailTask.Delay(PrazoRevisao);

            var vencedor = await SkailTask.WhenAny(revisao, prazo);
            if (vencedor == prazo)
            {
                await DevolverAoCliente(pedidoId, $"Pedido sem revisão no prazo: {validacao.Motivo}");
                return 0;
            }

            var resposta = await revisao;
            if (!resposta.Aprovado || resposta.Itens is null)
            {
                await DevolverAoCliente(pedidoId, $"Pedido recusado por {resposta.Revisor}: {validacao.Motivo}");
                return 0;
            }

            // Os itens corrigidos passam de novo pelo estoque e pela regra de negócio.
            itens = resposta.Itens;
            saldos = await ConsultarEstoque(itens);
            var revalidacao = await ValidarPedido(pedido.Cliente, itens, saldos);
            if (!revalidacao.Valido)
            {
                await DevolverAoCliente(pedidoId, $"Pedido revisado continua inválido: {revalidacao.Motivo}");
                return 0;
            }
        }

        // Bloco 3: o pedido gravado no ERP fica no histórico. Se ResponderCliente
        // falhar, a retomada devolve o número gravado e não chama o ERP de novo.
        var numero = await CriarPedidoNoErp(pedidoId, itens);
        await ResponderCliente(pedidoId, numero);
        return numero;
    }

    // ------------------------------------------------------------------
    // Antipadrão: retry padrão em tudo e falha de negócio como exceção
    // ------------------------------------------------------------------

    // retryCount 3 na function (o padrão é 15) só para a demonstração: com o padrão,
    // as reentregas têm intervalo crescente e a execução leva horas para chegar
    // na dead letter queue. Os commands continuam com o retry padrão (5).
    [SkailFunction(retryCount: 3)]
    public async SkailTask<int> ProcessarPedidoRetryPadrao(string pedidoId)
    {
        var pedido = await LerPedido(pedidoId);

        // Bloco 1: com o retry padrão, a queda do provedor gasta as tentativas
        // do command e da function, e a execução vai para a dead letter queue.
        var itens = await ExtrairItensComLlmRetryPadrao(pedido.Texto);

        var saldos = await ConsultarEstoque(itens);

        // Bloco 2: SKU inválido lançado como exceção é tentado de novo até o limite.
        // Bloco 4: sem regra de negócio, 1.000 caixas no lugar de 100 passam direto.
        await ValidarPedidoComExcecao(itens, saldos);

        var numero = await CriarPedidoNoErp(pedidoId, itens);
        await ResponderCliente(pedidoId, numero);
        return numero;
    }

    // ------------------------------------------------------------------
    // Commands: cada operação de I/O num command
    // ------------------------------------------------------------------

    [SkailCommand]
    public async SkailTask<PedidoRecebido> LerPedido(string pedidoId)
    {
        var pedido = await Simuladores.GetFromJsonAsync<PedidoRecebido>($"/caixa/{pedidoId}");
        return pedido ?? throw new InvalidOperationException($"Pedido {pedidoId} não encontrado na caixa de entrada.");
    }

    [SkailCommand(retryCount: 0)]
    public async SkailTask<List<ItemPedido>> ExtrairItensComLlm(string texto)
    {
        return await ChamarLlm(texto);
    }

    [SkailCommand]
    public async SkailTask<List<ItemPedido>> ExtrairItensComLlmRetryPadrao(string texto)
    {
        return await ChamarLlm(texto);
    }

    [SkailCommand]
    public async SkailTask<List<SaldoEstoque>> ConsultarEstoque(List<ItemPedido> itens)
    {
        var saldos = new List<SaldoEstoque>();
        foreach (var item in itens)
        {
            var resposta = await Simuladores.GetAsync($"/estoque/{item.Sku}");
            if (resposta.StatusCode == HttpStatusCode.NotFound)
            {
                saldos.Add(new SaldoEstoque(item.Sku, false, 0));
                continue;
            }
            resposta.EnsureSuccessStatusCode();
            var saldo = await resposta.Content.ReadFromJsonAsync<SaldoEstoque>();
            saldos.Add(saldo!);
        }
        return saldos;
    }

    [SkailCommand]
    public async SkailTask<Validacao> ValidarPedido(string cliente, List<ItemPedido> itens, List<SaldoEstoque> saldos)
    {
        var inexistente = saldos.FirstOrDefault(s => !s.Existe);
        if (inexistente is not null)
            return new Validacao(false, $"SKU {inexistente.Sku} não existe no cadastro");

        // Regra de negócio do bloco 4: quantidade acima de 10 vezes a média do cliente.
        var media = await Simuladores.GetFromJsonAsync<MediaCliente>($"/clientes/{cliente}/media");
        if (media is null)
            return new Validacao(false, $"Sem média de compra para o cliente {cliente}");
        foreach (var item in itens)
        {
            if (item.Quantidade > media.MediaPorItem * 10)
                return new Validacao(false,
                    $"Quantidade {item.Quantidade} do SKU {item.Sku} acima de 10 vezes a média do cliente ({media.MediaPorItem})");
        }

        return new Validacao(true, "");
    }

    [SkailCommand]
    public async SkailTask ValidarPedidoComExcecao(List<ItemPedido> itens, List<SaldoEstoque> saldos)
    {
        await Task.CompletedTask;
        var inexistente = saldos.FirstOrDefault(s => !s.Existe);
        if (inexistente is not null)
            throw new PedidoInvalidoException($"SKU {inexistente.Sku} não existe no cadastro");
    }

    [SkailCommand]
    public async SkailTask AvisarRevisao(string pedidoId, string motivo)
    {
        var resposta = await Simuladores.PostAsJsonAsync("/revisoes", new { pedidoId, motivo });
        resposta.EnsureSuccessStatusCode();
    }

    [SkailCommand]
    public async SkailTask DevolverAoCliente(string pedidoId, string motivo)
    {
        var resposta = await Simuladores.PostAsJsonAsync("/email/enviar",
            new { pedidoId, assunto = "Pedido não processado", corpo = motivo });
        resposta.EnsureSuccessStatusCode();
    }

    [SkailCommand]
    public async SkailTask<int> CriarPedidoNoErp(string pedidoId, List<ItemPedido> itens)
    {
        // Chave de idempotência: o id do pedido de origem. Se o ERP receber o mesmo
        // id duas vezes, devolve o pedido que já existe em vez de criar outro.
        using var requisicao = new HttpRequestMessage(HttpMethod.Post, "/erp/pedidos")
        {
            Content = JsonContent.Create(new { pedidoId, itens })
        };
        requisicao.Headers.Add("Idempotency-Key", pedidoId);

        var resposta = await Simuladores.SendAsync(requisicao);
        resposta.EnsureSuccessStatusCode();
        var criado = await resposta.Content.ReadFromJsonAsync<PedidoErp>();
        return criado!.Numero;
    }

    [SkailCommand]
    public async SkailTask ResponderCliente(string pedidoId, int numero)
    {
        var resposta = await Simuladores.PostAsJsonAsync("/email/enviar",
            new { pedidoId, assunto = $"Pedido {numero} confirmado", corpo = $"Seu pedido {pedidoId} virou o pedido {numero} no ERP." });
        resposta.EnsureSuccessStatusCode();
    }

    // Método comum (nem function nem command), chamado de dentro dos commands.
    private async Task<List<ItemPedido>> ChamarLlm(string texto)
    {
        HttpResponseMessage resposta;
        try
        {
            resposta = await Simuladores.PostAsJsonAsync("/llm/extrair", new { texto });
        }
        catch (Exception e) when (e is HttpRequestException or TaskCanceledException)
        {
            // Sem resposta (conexão recusada ou timeout) também é provedor indisponível.
            throw new ProvedorIndisponivelException($"LLM sem resposta: {e.Message}");
        }

        if (resposta.StatusCode is HttpStatusCode.TooManyRequests or HttpStatusCode.ServiceUnavailable
            or HttpStatusCode.InternalServerError || (int)resposta.StatusCode == 529)
        {
            var corpo = await resposta.Content.ReadAsStringAsync();
            throw new ProvedorIndisponivelException($"LLM respondeu {(int)resposta.StatusCode}: {corpo}");
        }

        resposta.EnsureSuccessStatusCode();
        return await resposta.Content.ReadFromJsonAsync<List<ItemPedido>>() ?? [];
    }

    private record MediaCliente(string Cliente, int MediaPorItem);

    private record PedidoErp(int Numero, string PedidoId);
}
