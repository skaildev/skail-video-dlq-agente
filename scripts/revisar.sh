#!/usr/bin/env bash
# Aprova o pedido com os itens corrigidos:
#   ./scripts/revisar.sh PED-2402 '[{"Sku":"10021","Quantidade":100},{"Sku":"10035","Quantidade":40}]'
# Recusa o pedido:
#   ./scripts/revisar.sh PED-2402 recusar
source "$(dirname "$0")/_comum.sh"
pedido="${1:?Id do pedido}"
if [[ "${2:-}" == "recusar" ]]; then
  revisar "$pedido" '{"Aprovado":false,"Itens":null,"Revisor":"maria.souza"}'
else
  revisar "$pedido" "{\"Aprovado\":true,\"Itens\":${2:?Itens em JSON},\"Revisor\":\"maria.souza\"}"
fi
