namespace AgenteDePedidos;

/// <summary>
/// Só para a demonstração do bloco 3. Quando o simulador de e-mail está no modo
/// "derrubar", ele responde com o header X-Simulador-Derrubar e este handler encerra
/// o processo da aplicação na hora, como um deploy ou falta de memória no meio do passo.
/// Nenhuma confirmação chega ao skail, que entrega a execução de novo depois do prazo.
/// </summary>
public class SimulacaoDeQuedaHandler : DelegatingHandler
{
    protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken ct)
    {
        var resposta = await base.SendAsync(request, ct);
        if (resposta.Headers.Contains("X-Simulador-Derrubar"))
        {
            Console.Error.WriteLine($"[SIMULAÇÃO] Aplicação encerrada durante {request.RequestUri}");
            System.Diagnostics.Process.GetCurrentProcess().Kill();
        }
        return resposta;
    }
}
