# Uso: .\scripts\modo.ps1 llm fora | .\scripts\modo.ps1 email timeout | .\scripts\modo.ps1 llm normal
# LLM: normal, fora (503), sobrecarga (529), limite (429 com retry-after), sku-invalido, quantidade-errada
# E-mail: normal, timeout
param([Parameter(Mandatory)][string]$Servico, [Parameter(Mandatory)][string]$NovoModo)
. "$PSScriptRoot\_comum.ps1"
Modo $Servico $NovoModo
