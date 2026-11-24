#!/usr/bin/env bash
# Sobe o agente e sobe de novo sempre que o processo cair (bloco 3, versão de infraestrutura).
RAIZ="$(cd "$(dirname "$0")/.." && pwd)"
cd "$RAIZ"
set -a; [[ -f .env ]] && source <(tr -d '\r' < .env); set +a
export DOTNET_ROLL_FORWARD=Major
dotnet build src/AgenteDePedidos >/dev/null
while true; do
  echo "$(date +%T) agente subindo"
  dotnet run --no-build --project src/AgenteDePedidos
  echo "$(date +%T) agente caiu, subindo de novo em 3 s"
  sleep 3
done
