#!/usr/bin/env bash
# Zera os simuladores (ERP, e-mails, revisões e modos) entre uma tomada e outra.
source "$(dirname "$0")/_comum.sh"
curl -sS -f -X POST "$SIMULADORES_URL/controle/reset" && echo "Simuladores zerados"
