# Aprova o pedido com os itens corrigidos:
#   .\scripts\revisar.ps1 PED-2402 '[{"Sku":"10021","Quantidade":100},{"Sku":"10035","Quantidade":40}]'
# Recusa o pedido:
#   .\scripts\revisar.ps1 PED-2402 recusar
param([Parameter(Mandatory)][string]$Pedido, [Parameter(Mandatory)][string]$Itens)
. "$PSScriptRoot\_comum.ps1"
if ($Itens -eq "recusar") {
    Revisar $Pedido '{"Aprovado":false,"Itens":null,"Revisor":"maria.souza"}'
} else {
    Revisar $Pedido "{`"Aprovado`":true,`"Itens`":$Itens,`"Revisor`":`"maria.souza`"}"
}
