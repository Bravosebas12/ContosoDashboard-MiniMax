#Requires -Version 5.1
<#
.SYNOPSIS
    Instala las herramientas necesarias para el análisis de calidad de código.

.DESCRIPTION
    Instala: dotnet-coverage, reportgenerator (global tools) y jscpd + cpd-windows-x64-msvc (npm global).
    Idempotente: detecta lo que ya está instalado y solo instala lo faltante.
#>
[CmdletBinding()]
param()

$ErrorActionPreference = 'Continue'

function Write-Step($msg) { Write-Host "`n▶ $msg" -ForegroundColor Cyan }

Write-Step "Verificando dotnet SDK..."
$dotnetVer = (& dotnet --version) 2>$null
if (-not $dotnetVer) {
    Write-Error "dotnet SDK no encontrado. Instalar desde https://dot.net"
    exit 1
}
Write-Host "  dotnet $dotnetVer ✓" -ForegroundColor Green

Write-Step "Instalando dotnet-coverage..."
if (Get-Command dotnet-coverage -ErrorAction SilentlyContinue) {
    Write-Host "  ya instalado ✓" -ForegroundColor Green
} else {
    & dotnet tool install --global dotnet-coverage
}

Write-Step "Instalando dotnet-reportgenerator-globaltool..."
if (Get-Command reportgenerator -ErrorAction SilentlyContinue) {
    Write-Host "  ya instalado ✓" -ForegroundColor Green
} else {
    & dotnet tool install --global dotnet-reportgenerator-globaltool
}

Write-Step "Instalando jscpd (npm)..."
if (Get-Command jscpd -ErrorAction SilentlyContinue) {
    Write-Host "  ya instalado ✓" -ForegroundColor Green
} else {
    & npm install -g jscpd
    & npm install -g cpd-windows-x64-msvc
}

Write-Step "Verificando skills.sh CLI (opcional)..."
if (Get-Command skills -ErrorAction SilentlyContinue) {
    Write-Host "  ya instalado ✓" -ForegroundColor Green
} else {
    Write-Host "  no instalado (opcional). Instalar con: npm install -g skills" -ForegroundColor Yellow
}

Write-Host "`n✅ Instalación completada." -ForegroundColor Green
Write-Host "  Siguiente paso: ejecutar el análisis con: .\.github\scripts\code-quality-report.ps1" -ForegroundColor Cyan
