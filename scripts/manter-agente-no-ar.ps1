# Sobe o agente e sobe de novo sempre que o processo cair (bloco 3, versão de infraestrutura).
. "$PSScriptRoot\_comum.ps1"
$env:DOTNET_ROLL_FORWARD = "Major"
Set-Location (Split-Path -Parent $PSScriptRoot)
dotnet build src/AgenteDePedidos | Out-Null
while ($true) {
    Write-Host "$(Get-Date -Format HH:mm:ss) agente subindo"
    dotnet run --no-build --project src/AgenteDePedidos
    Write-Host "$(Get-Date -Format HH:mm:ss) agente caiu, subindo de novo em 3 s"
    Start-Sleep -Seconds 3
}
