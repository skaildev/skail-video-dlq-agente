# Funções usadas pelos outros scripts. Lê as variáveis do arquivo .env na raiz do repositório.
$ErrorActionPreference = "Stop"
$Raiz = Split-Path -Parent $PSScriptRoot
$ArquivoEnv = Join-Path $Raiz ".env"
if (Test-Path $ArquivoEnv) {
    Get-Content $ArquivoEnv | Where-Object { $_ -match '^\s*[^#].*=' } | ForEach-Object {
        $nome, $valor = $_ -split '=', 2
        Set-Item -Path "env:$($nome.Trim())" -Value $valor.Trim()
    }
}
function PrecisaSkail {
    foreach ($v in "SKAIL_SIDECAR", "SKAIL_NAMESPACE", "SKAIL_KEY") {
        if (-not (Get-Item "env:$v" -ErrorAction SilentlyContinue)) { throw "Defina $v no .env" }
    }
}
if (-not $env:SKAIL_WORKLOAD) { $env:SKAIL_WORKLOAD = "dlqagente:v1.0.0" }
if (-not $env:SIMULADORES_URL) { $env:SIMULADORES_URL = "http://localhost:5080" }
$WorkloadNome, $WorkloadVersao = $env:SKAIL_WORKLOAD -split ':', 2
$Cabecalhos = @{
    "Authorization"   = "Bearer $env:SKAIL_KEY"
    "skail-key"       = $env:SKAIL_KEY
    "Skail-namespace" = $env:SKAIL_NAMESPACE
}

function Disparar([string]$Funcao, [string]$Pedido) {
    PrecisaSkail
    $id = [guid]::NewGuid().ToString()
    Invoke-RestMethod -Method Post -Uri "$env:SKAIL_SIDECAR/api/v1/trigger/$WorkloadNome/$WorkloadVersao/$Funcao/$id" `
        -ContentType "application/json" -Headers $Cabecalhos -Body "[`"$Pedido`"]" | Out-Null
    Write-Host "$Funcao $Pedido -> TaskId $id"
}

function Modo([string]$Servico, [string]$NovoModo) {
    Invoke-RestMethod -Method Post -Uri "$env:SIMULADORES_URL/controle/$Servico/$NovoModo" | ConvertTo-Json -Compress
}

function Revisar([string]$Pedido, [string]$Payload) {
    PrecisaSkail
    Invoke-RestMethod -Method Post -Uri "$env:SKAIL_SIDECAR/api/v1/fire/PEDIDO_REVISADO/$Pedido" `
        -ContentType "application/json" -Headers $Cabecalhos -Body $Payload | Out-Null
    Write-Host "PEDIDO_REVISADO disparado para $Pedido"
}
