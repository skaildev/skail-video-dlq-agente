#!/usr/bin/env bash
# Bloco 4: o LLM lê 100 caixas como 1.000.
# ProcessarPedidoRetryPadrao grava 1.000 no ERP e nada aparece na dead letter queue.
# ProcessarPedido barra na regra de negócio e fica aguardando PEDIDO_REVISADO.
source "$(dirname "$0")/_comum.sh"
modo llm quantidade-errada
disparar ProcessarPedidoRetryPadrao PED-2401
disparar ProcessarPedido PED-2402
echo
echo "Pedidos no ERP: curl $SIMULADORES_URL/erp/pedidos"
echo "Para aprovar o PED-2402 com a quantidade certa:"
echo "  ./scripts/revisar.sh PED-2402 '[{\"Sku\":\"10021\",\"Quantidade\":100},{\"Sku\":\"10035\",\"Quantidade\":40}]'"
