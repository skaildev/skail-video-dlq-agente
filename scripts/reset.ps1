# Zera os simuladores (ERP, e-mails, revisões e modos) entre uma tomada e outra.
. "$PSScriptRoot\_comum.ps1"
Invoke-RestMethod -Method Post -Uri "$env:SIMULADORES_URL/controle/reset" | Out-Null
Write-Host "Simuladores zerados"
