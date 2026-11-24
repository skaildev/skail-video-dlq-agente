#!/usr/bin/env bash
# Bloco 1: o provedor do LLM cai. Dispara 5 pedidos em cada versão do agente.
# ProcessarPedidoRetryPadrao gasta as tentativas e vai para a dead letter queue.
# ProcessarPedido fica aguardando o delay e conclui quando o LLM volta.
source "$(dirname "$0")/_comum.sh"
modo llm fora
for n in 1 2 3 4 5; do
  disparar ProcessarPedidoRetryPadrao "PED-210$n"
  disparar ProcessarPedido "PED-215$n"
done
echo
echo "Acompanhe no Monitor. Quando as execuções RetryPadrao estiverem na Dead letter queue:"
echo "  ./scripts/modo.sh llm normal"
echo "  e use 'Retentar tudo que casa com o filtro' filtrando por ProcessarPedidoRetryPadrao."
