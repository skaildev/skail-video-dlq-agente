# Bloco 3: o pedido é criado no ERP e o e-mail para o cliente estoura o timeout.
. "$PSScriptRoot\_comum.ps1"
Modo llm normal
Modo email timeout
Disparar ProcessarPedido PED-2301
Write-Host ""
Write-Host "Quando a execução chegar na Dead letter queue:"
Write-Host "  .\scripts\modo.ps1 email normal"
Write-Host "  e clique em Retentar na execução. Confira em $env:SIMULADORES_URL/erp/pedidos que só existe um pedido para PED-2301."
