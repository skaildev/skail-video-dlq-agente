#!/usr/bin/env bash
# Uso: ./scripts/modo.sh llm fora | ./scripts/modo.sh email timeout | ./scripts/modo.sh llm normal
# LLM: normal, fora (503), sobrecarga (529), limite (429 com retry-after), sku-invalido, quantidade-errada
# E-mail: normal, timeout
source "$(dirname "$0")/_comum.sh"
modo "${1:?llm ou email}" "${2:?modo}"
