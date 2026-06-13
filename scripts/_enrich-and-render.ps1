#requires -Version 7.0
<#
.SYNOPSIS
    Enriquece el reporte Markdown code-quality-report.md con tablas detalladas
    a partir de los artefactos generados por run-code-quality.ps1:
      - Top 10 archivos con menor cobertura (parsea Cobertura.xml)
      - Top 5 duplicaciones (parsea jscpd-report.json)
      - Top 10 incidencias (parsea build.log)
      - Top 10 vulnerabilidades y paquetes desactualizados (parsea los .txt)
    Ademas reescribe summary.json con los datos enriquecidos.
#>
$ErrorActionPreference = "Stop"
$root = Resolve-Path (Join-Path $PSScriptRoot "..")
Set-Location $root

$issuesDir   = Join-Path $root "quality-reports/issues"
$coverageDir = Join-Path $root "quality-reports/coverage"
$dupDir      = Join-Path $root "quality-reports/duplication"
$summaryPath = Join-Path $issuesDir "summary.json"
$reportPath  = Join-Path $root "code-quality-report.md"

# --- 1. Corregir methodPct parseando el raw de Summary.txt ----------
$summary = Get-Content $summaryPath -Raw | ConvertFrom-Json
$summaryTxt = Join-Path $coverageDir "Summary.txt"

# Reconstruir coverage como PSCustomObject con Set-Member para poder anyadir props
function Set-Property($obj, $name, $value) {
    if ($obj.PSObject.Properties[$name]) {
        $obj.$name = $value
    } else {
        $obj | Add-Member -NotePropertyName $name -NotePropertyValue $value
    }
}

if (Test-Path $summaryTxt) {
    $txt = Get-Content $summaryTxt -Raw
    # "Method coverage: 56% (37 of 66)"  ->  56
    if ($txt -match "(?m)^\s*Method coverage\s*:\s*(\d+(?:\.\d+)?)\s*%") {
        Set-Property $summary.coverage 'methodPct' ([double]$Matches[1])
    }
    if ($txt -match "(?m)^\s*Full method coverage\s*:\s*(\d+(?:\.\d+)?)\s*%") {
        Set-Property $summary.coverage 'fullMethodPct' ([double]$Matches[1])
    }
    if ($txt -match "(?m)^\s*Covered lines\s*:\s*(\d+)") {
        Set-Property $summary.coverage 'coveredLines' ([int]$Matches[1])
    }
    if ($txt -match "(?m)^\s*Coverable lines\s*:\s*(\d+)") {
        Set-Property $summary.coverage 'coverableLines' ([int]$Matches[1])
    }
    if ($txt -match "(?m)^\s*Total lines\s*:\s*(\d+)") {
        Set-Property $summary.coverage 'totalLines' ([int]$Matches[1])
    }
    if ($txt -match "(?m)^\s*Branch coverage\s*:\s*(\d+(?:\.\d+)?)\s*%") {
        Set-Property $summary.coverage 'branchPctFromRaw' ([double]$Matches[1])
    }
}

# --- 2. Top 10 archivos con menor cobertura desde Cobertura.xml -------
$covXml = Join-Path $coverageDir "Cobertura.xml"
if (Test-Path $covXml) {
    [xml]$cov = Get-Content $covXml -Raw
    $pkgs = $cov.coverage.packages.package
    $files = @()
    foreach ($pkg in $pkgs) {
        foreach ($cls in $pkg.classes.class) {
            # En Cobertura, line-rate es un ratio 0..1 a nivel de clase.
            # Las lineas cubiertas/totales se cuentan a partir de los elementos <line>.
            $covered = 0; $total = 0
            if ($cls.methods -and $cls.methods.method) {
                foreach ($m in @($cls.methods.method)) {
                    if ($m.lines -and $m.lines.line) {
                        foreach ($l in @($m.lines.line)) {
                            $total++
                            if ([int]$l.hits -gt 0) { $covered++ }
                        }
                    }
                }
            }
            $pct = if ($total -gt 0) { [math]::Round(($covered / $total) * 100, 1) } else { 0 }
            $files += [pscustomobject]@{
                File          = $cls.filename
                CoveredLines  = $covered
                TotalLines    = $total
                CoveragePct   = $pct
            }
        }
    }
    # Agrupar por archivo (puede haber duplicados por varios <package>) y sumar
    $byFile = $files | Group-Object File | ForEach-Object {
        $grp = $_.Group
        $totalC = ($grp | Measure-Object -Property CoveredLines -Sum).Sum
        $totalT = ($grp | Measure-Object -Property TotalLines   -Sum).Sum
        $pct = if ($totalT -gt 0) { [math]::Round(($totalC / $totalT) * 100, 1) } else { 0 }
        [pscustomobject]@{
            File         = $_.Name
            CoveredLines = $totalC
            TotalLines   = $totalT
            CoveragePct  = $pct
        }
    }
    $summary | Add-Member -NotePropertyName 'coverageTopFiles' -NotePropertyValue (@($byFile | Where-Object { $_.TotalLines -gt 0 } | Sort-Object CoveragePct, TotalLines | Select-Object -First 10 | ForEach-Object {
        $rel = $_.File
        if ($rel -and $root) {
            try { $rel = (Resolve-Path -LiteralPath $rel -Relative) -replace '^\.\\', '' } catch {}
        }
        [pscustomobject]@{
            File         = $rel
            CoveredLines = $_.CoveredLines
            TotalLines   = $_.TotalLines
            CoveragePct  = $_.CoveragePct
        }
    })) -Force
}

# Resumen agregado de cobertura: archivos sin tests, parcialmente cubiertos, bien cubiertos
if ($byFile) {
    $sinTests  = @($byFile | Where-Object { $_.CoveragePct -eq 0 }).Count
    $parcial   = @($byFile | Where-Object { $_.CoveragePct -gt 0 -and $_.CoveragePct -lt 80 }).Count
    $cubiertos = @($byFile | Where-Object { $_.CoveragePct -ge 80 }).Count
    $summary | Add-Member -NotePropertyName 'coverageDistribution' -NotePropertyValue ([ordered]@{
        SinTests       = $sinTests
        Parcial0a80    = $parcial
        Cubiertos80a100= $cubiertos
        TotalArchivos  = $byFile.Count
    }) -Force
}

# --- 3. Top 5 duplicaciones ------------------------------------------
$dupJson = Join-Path $dupDir "jscpd-report.json"
if (Test-Path $dupJson) {
    $j = Get-Content $dupJson -Raw | ConvertFrom-Json
    $summary | Add-Member -NotePropertyName 'duplicationTop' -NotePropertyValue (@($j.duplicates | Select-Object -First 5 | ForEach-Object {
        [pscustomobject]@{
            Lines     = $_.lines
            Tokens    = $_.tokens
            Format    = $_.format
            Fragment  = ($_.fragment -split "`n" | Select-Object -First 3) -join " | "
            FileA     = $_.firstFile.name
            StartA    = $_.firstFile.start
            EndA      = $_.firstFile.end
            FileB     = $_.secondFile.name
            StartB    = $_.secondFile.start
            EndB      = $_.secondFile.end
        }
    })) -Force
}

# --- 4. Top 10 incidencias desde build.log ----------------------------
$buildLog = Join-Path $issuesDir "build.log"
if (Test-Path $buildLog) {
    $logLines = Get-Content $buildLog
    $incidences = @()
    foreach ($line in $logLines) {
        # Formato tipico: "  ContosoDashboard\Services\Foo.cs(123,45): warning CS8602: Possible null reference."
        if ($line -match "^\s*([^\s].*?)\((\d+),(\d+)\):\s*(warning|error)\s+([A-Z]+\d+)\s*:\s*(.+?)\s*$") {
            $incidences += [pscustomobject]@{
                File     = $Matches[1].Trim()
                Line     = [int]$Matches[2]
                Col      = [int]$Matches[3]
                Severity = $Matches[4]
                Code     = $Matches[5]
                Message  = $Matches[6].Trim()
            }
        }
    }
    $summary | Add-Member -NotePropertyName 'issuesTop' -NotePropertyValue (@($incidences | Select-Object -First 10)) -Force
}

# --- 5. Top vulnerables y outdated (parseo real) ---------------------
function Parse-NuGetList($path) {
    if (-not (Test-Path $path)) { return @() }
    $lines = Get-Content $path
    $items = @()
    foreach ($line in $lines) {
        # Formato vulnerable: "   > Microsoft.Data.SqlClient    5.1.1              High   https://..."
        # Formato outdated:   "   > Microsoft.Data.SqlClient    5.1.1    5.1.1   5.1.2"
        if ($line -match "^\s*>\s+(\S+)\s+(\S+)(?:\s+(\S+))?(?:\s+(High|Moderate|Low|Critical))?\s*(https?://\S+)?\s*$") {
            $items += [pscustomobject]@{
                Package  = $Matches[1]
                Version  = $Matches[2]
                Resolved = if ($Matches[3]) { $Matches[3] } else { "" }
                Severity = if ($Matches[4]) { $Matches[4] } else { "" }
                Url      = if ($Matches[5]) { $Matches[5] } else { "" }
            }
        }
    }
    return $items
}
$summary | Add-Member -NotePropertyName 'vulnerableTop' -NotePropertyValue (@(Parse-NuGetList (Join-Path $issuesDir "vulnerable.txt") | Select-Object -First 10)) -Force
$summary | Add-Member -NotePropertyName 'outdatedTop'   -NotePropertyValue (@(Parse-NuGetList (Join-Path $issuesDir "outdated.txt")   | Select-Object -First 10)) -Force

# --- Persistir summary enriquecido -----------------------------------
$summary | ConvertTo-Json -Depth 20 | Out-File -FilePath $summaryPath -Encoding utf8
Write-Host "[OK] summary.json enriquecido" -ForegroundColor Green
Write-Host "  methodPct: $($summary.coverage.methodPct)"
Write-Host "  Top 10 archivos cobertura: $($summary.coverageTopFiles.Count)"
Write-Host "  Top 5 duplicaciones: $($summary.duplicationTop.Count)"
Write-Host "  Top incidencias: $($summary.issuesTop.Count)"
Write-Host "  Vulnerables parseados: $($summary.vulnerableTop.Count)"
Write-Host "  Outdated parseados: $($summary.outdatedTop.Count)"

# --- Re-renderizar el reporte ----------------------------------------
& "$PSScriptRoot/render-report.ps1" -OutputReport $reportPath
