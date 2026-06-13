#requires -Version 7.0
<#
.SYNOPSIS
    Limpia artefactos previos del agente /code-quality y deja la carpeta lista
    para una corrida limpia.

.DESCRIPTION
    Borra el reporte Markdown previo y los artefactos generados, conservando
    la estructura de carpetas y los scripts del agente. Pensado para ejecutarse
    antes de run-code-quality.ps1 cuando se quiere garantizar idempotencia.

.PARAMETER KeepScripts
    Conserva los scripts de orquestación. Por defecto: true.
#>
[CmdletBinding()]
param(
    [switch] $KeepScripts = $true
)

$root = Resolve-Path (Join-Path $PSScriptRoot "..")
Set-Location $root

Write-Host "Limpiando artefactos previos de /code-quality en: $root" -ForegroundColor Yellow

$targets = @(
    (Join-Path $root "code-quality-report.md"),
    (Join-Path $root "TestResults"),
    (Join-Path $root "quality-reports/coverage/*"),
    (Join-Path $root "quality-reports/duplication/*"),
    (Join-Path $root "quality-reports/issues/*")
)

foreach ($t in $targets) {
    if (Test-Path $t) {
        Remove-Item -Recurse -Force -ErrorAction SilentlyContinue $t
        Write-Host "  - eliminado: $t" -ForegroundColor DarkGray
    }
}

# Recrear carpetas vacias
$dirs = @(
    (Join-Path $root "TestResults"),
    (Join-Path $root "quality-reports/coverage"),
    (Join-Path $root "quality-reports/duplication"),
    (Join-Path $root "quality-reports/issues")
)
foreach ($d in $dirs) {
    if (-not (Test-Path $d)) {
        New-Item -ItemType Directory -Force -Path $d | Out-Null
    }
}

Write-Host "Listo. Ahora puede ejecutar:" -ForegroundColor Green
Write-Host "  pwsh ./scripts/run-code-quality.ps1" -ForegroundColor Cyan
Write-Host "  pwsh ./scripts/render-report.ps1" -ForegroundColor Cyan
