# Bloco 2: o LLM devolve um SKU que não existe (88412-X).
. "$PSScriptRoot\_comum.ps1"
Modo llm sku-invalido
Disparar ProcessarPedidoRetryPadrao PED-2201
Disparar ProcessarPedido PED-2202
Write-Host ""
Write-Host "Para concluir o PED-2202 com os itens corrigidos (depois volte o LLM ao normal):"
Write-Host "  .\scripts\revisar.ps1 PED-2202 '[{`"Sku`":`"10021`",`"Quantidade`":100},{`"Sku`":`"10035`",`"Quantidade`":40}]'"
