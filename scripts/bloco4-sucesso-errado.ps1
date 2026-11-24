# Bloco 4: o LLM lê 100 caixas como 1.000.
. "$PSScriptRoot\_comum.ps1"
Modo llm quantidade-errada
Disparar ProcessarPedidoRetryPadrao PED-2401
Disparar ProcessarPedido PED-2402
Write-Host ""
Write-Host "Pedidos no ERP: $env:SIMULADORES_URL/erp/pedidos"
Write-Host "Para aprovar o PED-2402 com a quantidade certa:"
Write-Host "  .\scripts\revisar.ps1 PED-2402 '[{`"Sku`":`"10021`",`"Quantidade`":100},{`"Sku`":`"10035`",`"Quantidade`":40}]'"
