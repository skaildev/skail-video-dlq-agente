#!/usr/bin/env bash
# Bloco 3, versão de infraestrutura: a aplicação cai logo depois de gravar o pedido no ERP.
# O simulador de e-mail no modo "derrubar" faz o agente encerrar o próprio processo no
# ResponderCliente. Suba o agente com scripts/manter-agente-no-ar.sh para ele voltar
# depois de cada queda. Depois de algumas entregas sem resposta, a execução vai para a
# Dead letter queue. Para retomar: ./scripts/modo.sh email normal e Retentar na DLQ.
source "$(dirname "$0")/_comum.sh"
modo llm normal
modo email derrubar
disparar ProcessarPedido PED-3401
