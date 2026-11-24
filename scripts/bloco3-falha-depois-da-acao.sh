#!/usr/bin/env bash
# Bloco 3: o pedido é criado no ERP e o e-mail para o cliente estoura o timeout.
source "$(dirname "$0")/_comum.sh"
modo llm normal
modo email timeout
disparar ProcessarPedido PED-2301
echo
echo "Quando a execução chegar na Dead letter queue:"
echo "  ./scripts/modo.sh email normal"
echo "  e clique em Retentar na execução. Confira em $SIMULADORES_URL/erp/pedidos que só existe um pedido para PED-2301."
