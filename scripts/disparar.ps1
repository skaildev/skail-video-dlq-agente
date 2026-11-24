# Uso: .\scripts\disparar.ps1 ProcessarPedido PED-2001
param([Parameter(Mandatory)][string]$Funcao, [Parameter(Mandatory)][string]$Pedido)
. "$PSScriptRoot\_comum.ps1"
Disparar $Funcao $Pedido
