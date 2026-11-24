#!/usr/bin/env bash
# Bloco 2: o LLM devolve um SKU que não existe (88412-X).
# ProcessarPedidoRetryPadrao lança exceção e repete até cair na dead letter queue.
# ProcessarPedido devolve o erro como resultado e fica aguardando PEDIDO_REVISADO.
source "$(dirname "$0")/_comum.sh"
modo llm sku-invalido
disparar ProcessarPedidoRetryPadrao PED-2201
disparar ProcessarPedido PED-2202
echo
echo "Para concluir o PED-2202 com os itens corrigidos (depois volte o LLM ao normal):"
echo "  ./scripts/revisar.sh PED-2202 '[{\"Sku\":\"10021\",\"Quantidade\":100},{\"Sku\":\"10035\",\"Quantidade\":40}]'"
