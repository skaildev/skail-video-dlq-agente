// Host do workload. O runtime do skail lê SKAIL_WORKLOAD, SKAIL_SIDECAR,
// SKAIL_NAMESPACE e SKAIL_KEY das variáveis de ambiente.
using Skail.Platform.Runtime;

[assembly: VisibleToSkailPlatform]

var builder = Host.CreateApplicationBuilder(args);

// Endereço dos serviços simulados (LLM, estoque, ERP e e-mail).
var simuladores = Environment.GetEnvironmentVariable("SIMULADORES_URL") ?? "http://localhost:5080";

// Timeout curto para o passo de e-mail ficar visível no Monitor quando o simulador
// estiver no modo "timeout".
builder.Services.AddHttpClient("simuladores", client =>
{
    client.BaseAddress = new Uri(simuladores);
    client.Timeout = TimeSpan.FromSeconds(5);
});

builder.UseSkail();

await builder.Build().RunAsync();
