#!/usr/bin/env bash
# Funções usadas pelos outros scripts. Lê as variáveis do arquivo .env na raiz do repositório.
set -euo pipefail
RAIZ="$(cd "$(dirname "${BASH_SOURCE[0]}")/.." && pwd)"
if [[ -f "$RAIZ/.env" ]]; then set -a; source <(tr -d '\r' < "$RAIZ/.env"); set +a; fi
SKAIL_WORKLOAD="${SKAIL_WORKLOAD:-dlqagente:v1.0.0}"
SKAIL_SIDECAR="${SKAIL_SIDECAR:-}"; SKAIL_NAMESPACE="${SKAIL_NAMESPACE:-}"; SKAIL_KEY="${SKAIL_KEY:-}"
SIMULADORES_URL="${SIMULADORES_URL:-http://localhost:5080}"
WORKLOAD_NOME="${SKAIL_WORKLOAD%%:*}"
WORKLOAD_VERSAO="${SKAIL_WORKLOAD##*:}"

novo_id() { cat /proc/sys/kernel/random/uuid 2>/dev/null || uuidgen | tr '[:upper:]' '[:lower:]'; }

# disparar <Funcao> <pedidoId>: inicia uma execução e mostra o TaskId
precisa_skail() {
  : "${SKAIL_SIDECAR:?Defina SKAIL_SIDECAR no .env}"
  : "${SKAIL_NAMESPACE:?Defina SKAIL_NAMESPACE no .env}"
  : "${SKAIL_KEY:?Defina SKAIL_KEY no .env}"
}

disparar() {
  precisa_skail
  local funcao="$1" pedido="$2" id
  id="$(novo_id)"
  curl -sS -f -X POST "$SKAIL_SIDECAR/api/v1/trigger/$WORKLOAD_NOME/$WORKLOAD_VERSAO/$funcao/$id" \
    -H "Content-Type: application/json" \
    -H "Authorization: Bearer $SKAIL_KEY" \
    -H "skail-key: $SKAIL_KEY" \
    -H "Skail-namespace: $SKAIL_NAMESPACE" \
    -d "[\"$pedido\"]" >/dev/null
  echo "$funcao $pedido -> TaskId $id"
}

# modo <llm|email> <modo>: liga um modo de falha nos simuladores
modo() {
  curl -sS -f -X POST "$SIMULADORES_URL/controle/$1/$2"; echo
}

# revisar <pedidoId> <json do payload>: dispara o evento PEDIDO_REVISADO
revisar() {
  precisa_skail
  curl -sS -f -X POST "$SKAIL_SIDECAR/api/v1/fire/PEDIDO_REVISADO/$1" \
    -H "Content-Type: application/json" \
    -H "Authorization: Bearer $SKAIL_KEY" \
    -H "skail-key: $SKAIL_KEY" \
    -H "Skail-namespace: $SKAIL_NAMESPACE" \
    -d "$2" >/dev/null
  echo "PEDIDO_REVISADO disparado para $1"
}
