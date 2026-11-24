#!/usr/bin/env bash
# Uso: ./scripts/disparar.sh ProcessarPedido PED-2001
source "$(dirname "$0")/_comum.sh"
disparar "${1:?Função}" "${2:?Id do pedido}"
