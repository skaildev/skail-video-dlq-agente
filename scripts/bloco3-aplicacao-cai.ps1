# Bloco 3, versão de infraestrutura: a aplicação cai logo depois de gravar o pedido no ERP.
# Suba o agente com scripts\manter-agente-no-ar.ps1 para ele voltar depois de cada queda.
. "$PSScriptRoot\_comum.ps1"
Modo llm normal
Modo email derrubar
Disparar ProcessarPedido PED-3401
