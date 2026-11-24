# Bloco 1: o provedor do LLM cai. Dispara 5 pedidos em cada versão do agente.
. "$PSScriptRoot\_comum.ps1"
Modo llm fora
1..5 | ForEach-Object {
    Disparar ProcessarPedidoRetryPadrao "PED-210$_"
    Disparar ProcessarPedido "PED-215$_"
}
Write-Host ""
Write-Host "Quando as execuções RetryPadrao estiverem na Dead letter queue:"
Write-Host "  .\scripts\modo.ps1 llm normal"
Write-Host "  e use 'Retentar tudo que casa com o filtro' filtrando por ProcessarPedidoRetryPadrao."
