# Uso (na pasta Portal Coopadap):  powershell -ExecutionPolicy Bypass -File banco\gerar-migracao-inicial.ps1
$ErrorActionPreference = "Stop"
Set-Location (Split-Path $PSScriptRoot -Parent)

dotnet tool restore
dotnet ef migrations add Inicial `
    --project src/AgroCert.Infrastructure `
    --startup-project src/AgroCert.Portal.Teste `
    --output-dir Persistencia/Migrations
