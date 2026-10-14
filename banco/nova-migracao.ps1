# Uso: powershell -ExecutionPolicy Bypass -File banco\nova-migracao.ps1 NomeDaMudanca
param([Parameter(Mandatory = $true)][string]$Nome)
$ErrorActionPreference = "Stop"
Set-Location (Split-Path $PSScriptRoot -Parent)

dotnet tool restore
dotnet ef migrations add $Nome `
    --project src/AgroCert.Infrastructure `
    --startup-project src/AgroCert.Portal.Teste `
    --output-dir Persistencia/Migrations
