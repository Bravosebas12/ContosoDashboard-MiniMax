#requires -Version 7.0
$ErrorActionPreference = "Stop"
$root = Resolve-Path (Join-Path $PSScriptRoot "..")
Set-Location $root

$summaryPath = Join-Path $root "quality-reports/issues/summary.json"
$dupJsonPath = Join-Path $root "quality-reports/duplication/jscpd-report.json"

if (-not (Test-Path $dupJsonPath)) {
    throw "No se encontro jscpd-report.json en $dupJsonPath"
}

$j = Get-Content $dupJsonPath -Raw | ConvertFrom-Json
$summary = Get-Content $summaryPath -Raw | ConvertFrom-Json

# jscpd >= 4.x expone: statistics.total.{lines, duplicatedLines, percentage, clones, sources}
# Ojo: el JSON puede llegar con coma decimal (cultura es-ES) -> normalizar.
$rawPct = "$($j.statistics.total.percentage)"
$pctNum = if ($rawPct) { [double]($rawPct -replace ',', '.') } else { 0 }

# Reemplazar la seccion de duplicacion
$summary.duplication = [ordered]@{
    totalLines      = [int]$j.statistics.total.lines
    duplicatedLines = [int]$j.statistics.total.duplicatedLines
    pct             = [math]::Round($pctNum, 2)
    blocks          = [int]$j.statistics.total.clones
    files           = [int]$j.statistics.total.sources
    top             = @($j.duplicates | Select-Object -First 5)
}

# Limpiar lineas de NU1603 que contaminan vulnerables/outdated
if ($summary.issues.vulnerable) {
    $summary.issues.vulnerable = @($summary.issues.vulnerable | Where-Object { $_ -notmatch 'NU1603' })
}
if ($summary.issues.outdated) {
    $summary.issues.outdated = @($summary.issues.outdated | Where-Object { $_ -notmatch 'NU1603' })
}

$summary | ConvertTo-Json -Depth 10 | Out-File -FilePath $summaryPath -Encoding utf8

Write-Host "[OK] summary.json actualizado" -ForegroundColor Green
Write-Host "  Duplicacion: pct=$($summary.duplication.pct)% bloques=$($summary.duplication.blocks) archivos=$($summary.duplication.files)"
Write-Host "  Vulnerables limpios: $(@($summary.issues.vulnerable).Count) entradas"
Write-Host "  Outdated limpios:    $(@($summary.issues.outdated).Count) entradas"
