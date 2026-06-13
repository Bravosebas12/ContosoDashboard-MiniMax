#requires -Version 7.0
<#
.SYNOPSIS
    Orquestador del análisis de calidad de código del proyecto ContosoDashboard.

.DESCRIPTION
    Ejecuta los tres análisis requeridos por el agente /code-quality:
      1) Cobertura de código (coverlet + ReportGenerator)
      2) Duplicación de código (jscpd)
      3) Incidencias de código (Roslyn, analyzers, NuGet)

    Genera los artefactos en ./quality-reports/ y deja un resumen
    estructurado en ./quality-reports/issues/summary.json que el
    reporte markdown consume para renderizar las tablas finales.

.PARAMETER Scope
    Ruta a la solución o proyecto a analizar. Por defecto: ContosoDashboard.slnx

.PARAMETER SkipCoverage
    Omite el paso de cobertura de código.

.PARAMETER SkipDuplication
    Omite el paso de detección de duplicación.

.PARAMETER SkipBuild
    Omite el build incremental (no afecta a cobertura ni a duplicación).

.PARAMETER Threshold
    Umbral (%) de duplicación a partir del cual se marca como rojo.

.PARAMETER OutputReport
    Ruta del reporte Markdown a generar/sobrescribir.

.EXAMPLE
    .\scripts\run-code-quality.ps1
    .\scripts\run-code-quality.ps1 -SkipDuplication
    .\scripts\run-code-quality.ps1 -Scope "ContosoDashboard.slnx" -OutputReport "docs/code-quality-report.md"
#>
[CmdletBinding()]
param(
    [string] $Scope = "ContosoDashboard.slnx",
    [switch] $SkipCoverage,
    [switch] $SkipDuplication,
    [switch] $SkipBuild,
    [double] $Threshold = 5.0,
    [string] $OutputReport = "code-quality-report.md"
)

$ErrorActionPreference = "Continue"
$root = Resolve-Path (Join-Path $PSScriptRoot "..")
Set-Location $root

$ts = Get-Date -Format "yyyy-MM-dd HH:mm"
$reportDir = Join-Path $root "quality-reports"
$coverageDir = Join-Path $reportDir "coverage"
$dupDir = Join-Path $reportDir "duplication"
$issuesDir = Join-Path $reportDir "issues"
$testResultsDir = Join-Path $root "TestResults"

New-Item -ItemType Directory -Force -Path $reportDir, $coverageDir, $dupDir, $issuesDir, $testResultsDir | Out-Null

# Banner ---------------------------------------------------------------
Write-Host ""
Write-Host "================================================================" -ForegroundColor Cyan
Write-Host " ContosoDashboard - Reporte de Calidad de Codigo" -ForegroundColor Cyan
Write-Host " Fecha: $ts" -ForegroundColor Cyan
Write-Host " Scope: $Scope" -ForegroundColor Cyan
Write-Host "================================================================" -ForegroundColor Cyan
Write-Host ""

$summary = [ordered]@{
    generatedAt        = $ts
    scope              = $Scope
    branch             = (git rev-parse --abbrev-ref HEAD 2>$null)
    commit             = (git rev-parse --short HEAD 2>$null)
    coverage           = $null
    duplication        = $null
    issues             = $null
    thresholdsPassed   = $false
}

# 0) Pre-flight --------------------------------------------------------
function Test-Command($name) {
    return [bool](Get-Command $name -ErrorAction SilentlyContinue)
}

$tools = [ordered]@{
    dotnet           = (Test-Command "dotnet")
    reportgenerator  = (Test-Command "reportgenerator")
    jscpd            = (Test-Command "jscpd")
    npx              = (Test-Command "npx")
}

Write-Host "[Pre-flight] Herramientas disponibles:" -ForegroundColor Yellow
$tools.GetEnumerator() | ForEach-Object {
    $color = if ($_.Value) { "Green" } else { "Red" }
    Write-Host ("  {0,-16} {1}" -f $_.Key, $(if ($_.Value) { "OK" } else { "NO INSTALADO" })) -ForegroundColor $color
}
Write-Host ""

# Asegurar reportgenerator ----------------------------------------------------
if (-not $tools.reportgenerator -and $tools.dotnet) {
    Write-Host "[Setup] Instalando dotnet-reportgenerator-globaltool..." -ForegroundColor Yellow
    try {
        dotnet tool install --global dotnet-reportgenerator-globaltool | Out-Null
        $tools.reportgenerator = $true
    } catch {
        Write-Warning "No se pudo instalar reportgenerator: $($_.Exception.Message)"
    }
}

# 1) Cobertura --------------------------------------------------------
if (-not $SkipCoverage) {
    Write-Host "[1/3] Cobertura de codigo" -ForegroundColor Cyan
    if (-not (Test-Path "coverage.runsettings")) {
        Write-Warning "No se encontro coverage.runsettings en la raiz; usando defaults de coverlet."
    }

    $testArgs = @(
        $Scope,
        "--settings", "coverage.runsettings",
        "--collect", "XPlat Code Coverage",
        "--results-directory", "./TestResults",
        "--nologo"
    )

    Write-Host "  > dotnet test $($testArgs -join ' ')"
    dotnet test @testArgs 2>&1 | Tee-Object -FilePath (Join-Path $issuesDir "test.log") | Out-Null

    $coberturaFiles = Get-ChildItem -Path "./TestResults" -Recurse -Filter "coverage.cobertura.xml" -ErrorAction SilentlyContinue
    if ($coberturaFiles -and $tools.reportgenerator) {
        Write-Host "  > reportgenerator..." -ForegroundColor Yellow
        $rgArgs = @(
            "-reports:`"./TestResults/**/coverage.cobertura.xml`"",
            "-targetdir:`"$coverageDir`"",
            "-reporttypes:Html_Dark;Html_Light;Badges;TextSummary;MarkdownSummaryGithub;Cobertura",
            "-assemblyfilters:+ContosoDashboard*",
            "-classfilters:-ContosoDashboard.Migrations.*;-ContosoDashboard.Pages.*;-ContosoDashboard.Shared.*"
        )
        reportgenerator @rgArgs 2>&1 | Tee-Object -FilePath (Join-Path $issuesDir "reportgenerator.log") | Out-Null

        # Extraer metricas del TextSummary.
        # OJO: "Method coverage" matchea tambien "Full method coverage"; usamos
        # un regex con dos puntos y ancla al inicio para evitar duplicados.
        $summaryFile = Join-Path $coverageDir "Summary.txt"
        if (Test-Path $summaryFile) {
            $lines = Get-Content $summaryFile
            # Extraer SOLO el porcentaje (no la linea completa).
            # Si hacemos -replace sobre la linea, todos los numeros se concatenan
            # (ej "62% (62 of 100)" -> "6262100"). Usamos un regex con grupo de captura.
            $linePct   = ($lines | Where-Object { $_ -match '^\s*Line coverage\s*:\s*(\d+(?:[.,]\d+)?)\s*%' }   | ForEach-Object { [double]($Matches[1] -replace ',', '.') } | Select-Object -First 1)
            $branchPct = ($lines | Where-Object { $_ -match '^\s*Branch coverage\s*:\s*(\d+(?:[.,]\d+)?)\s*%' } | ForEach-Object { [double]($Matches[1] -replace ',', '.') } | Select-Object -First 1)
            $methodPct = ($lines | Where-Object { $_ -match '^\s*Method coverage\s*:\s*(\d+(?:[.,]\d+)?)\s*%' } | ForEach-Object { [double]($Matches[1] -replace ',', '.') } | Select-Object -First 1)
            $summary.coverage = [ordered]@{
                linePct   = $linePct
                branchPct = $branchPct
                methodPct = $methodPct
                raw       = ($lines -join "`n")
            }
        } else {
            $summary.coverage = @{ error = "Summary.txt no generado" }
        }
    } else {
        $summary.coverage = @{ error = "Sin cobertura (tests no se ejecutaron o reportgenerator ausente)" }
        Write-Warning "No se encontro coverage.cobertura.xml; se omitira el reporte de cobertura."
    }
} else {
    $summary.coverage = @{ skipped = $true }
    Write-Host "[1/3] Cobertura omitida (-SkipCoverage)" -ForegroundColor DarkGray
}

# 2) Duplicacion ------------------------------------------------------
if (-not $SkipDuplication) {
    Write-Host ""
    Write-Host "[2/3] Duplicacion de codigo" -ForegroundColor Cyan

    $jscpd = $tools.jscpd
    if (-not $jscpd -and $tools.npx) {
        Write-Host "  > usando npx jscpd (sin instalacion global)" -ForegroundColor Yellow
        $jscpd = "npx jscpd"
    }

    if ($jscpd) {
        # Limpiar salida previa (ruta con espacios: usar Join-Path + wildcard expandido)
        $dupWild = Join-Path $dupDir '*'
        if (Test-Path $dupWild) { Remove-Item -Recurse -Force $dupWild -ErrorAction SilentlyContinue }

        # Llamada directa (NO Invoke-Expression) para que -ErrorAction y exit codes
        # se propaguen correctamente y el JSON se genere de forma fiable.
        $jscpdExe = if ($tools.jscpd) { "jscpd" } else { "npx" }
        $jscpdArgs = @()
        if (-not $tools.jscpd) { $jscpdArgs += "jscpd" }
        $jscpdArgs += @(
            "ContosoDashboard",
            "--output", $dupDir,
            "--reporters", "html,json,markdown",
            "--min-lines", "5",
            "--min-tokens", "50",
            "--ignore", "**/bin/**,**/obj/**,**/Migrations/**,**/wwwroot/**"
        )
        Write-Host "  > $jscpdExe $($jscpdArgs -join ' ')"
        & $jscpdExe @jscpdArgs 2>&1 | Tee-Object -FilePath (Join-Path $issuesDir "jscpd.log") | Out-Null

        $jscpdJson = Join-Path $dupDir "jscpd-report.json"
        if (Test-Path $jscpdJson) {
            try {
                $j = Get-Content $jscpdJson -Raw | ConvertFrom-Json
                # jscpd >= 4.x expone: statistics.total.{lines, duplicatedLines, percentage, clones, sources}
                # Ojo: el JSON puede llegar con coma decimal (cultura es-ES) -> normalizar.
                $rawPct = "$($j.statistics.total.percentage)"
                $pctNum = if ($rawPct) { [double]($rawPct -replace ',', '.') } else { 0 }
                $summary.duplication = [ordered]@{
                    totalLines      = [int]$j.statistics.total.lines
                    duplicatedLines = [int]$j.statistics.total.duplicatedLines
                    pct             = [math]::Round($pctNum, 2)
                    blocks          = [int]$j.statistics.total.clones
                    files           = [int]$j.statistics.total.sources
                    top             = @($j.duplicates | Select-Object -First 5)
                }
            } catch {
                $summary.duplication = @{ error = "JSON no parseable: $($_.Exception.Message)" }
            }
        } else {
            $summary.duplication = @{ error = "jscpd-report.json no encontrado" }
        }
    } else {
        $summary.duplication = @{ error = "jscpd/npx no disponibles" }
        Write-Warning "Sin jscpd ni npx; omitiendo duplicacion."
    }
} else {
    $summary.duplication = @{ skipped = $true }
    Write-Host "[2/3] Duplicacion omitida (-SkipDuplication)" -ForegroundColor DarkGray
}

# 3) Incidencias ------------------------------------------------------
Write-Host ""
Write-Host "[3/3] Incidencias de codigo" -ForegroundColor Cyan

# Build
$buildLog = Join-Path $issuesDir "build.log"
if (-not $SkipBuild) {
    $buildArgs = @($Scope, "-c", "Release", "-p:TreatWarningsAsErrors=false", "-p:EnforceCodeStyleInBuild=true", "--no-incremental")
    Write-Host "  > dotnet build $($buildArgs -join ' ')"
    dotnet build @buildArgs 2>&1 | Tee-Object -FilePath $buildLog | Out-Null
}

# Vulnerabilidades
$vulnLog = Join-Path $issuesDir "vulnerable.txt"
Write-Host "  > dotnet list package --vulnerable --include-transitive"
dotnet list $Scope package --vulnerable --include-transitive 2>&1 | Out-File -FilePath $vulnLog -Encoding utf8

# Outdated
$outLog = Join-Path $issuesDir "outdated.txt"
Write-Host "  > dotnet list package --outdated"
dotnet list $Scope package --outdated 2>&1 | Out-File -FilePath $outLog -Encoding utf8

# Parseo del build.log
$csErrors  = @()
$csWarn    = @()
$caWarn    = @()
$ideWarn   = @()
$nuWarn    = @()
if (Test-Path $buildLog) {
    Get-Content $buildLog | ForEach-Object {
        $line = $_
        if ($line -match "error\s+(CS\d+):") { $csErrors += $Matches[1] }
        elseif ($line -match "warning\s+(CS\d+):") { $csWarn += $Matches[1] }
        elseif ($line -match "warning\s+(CA\d+):") { $caWarn += $Matches[1] }
        elseif ($line -match "warning\s+(IDE\d+):") { $ideWarn += $Matches[1] }
        elseif ($line -match "warning\s+(NU\d+):") { $nuWarn += $Matches[1] }
    }
}

$summary.issues = [ordered]@{
    buildErrors   = ($csErrors  | Group-Object | Sort-Object Count -Descending | Select-Object Name, Count)
    buildWarnings = ($csWarn    | Group-Object | Sort-Object Count -Descending | Select-Object Name, Count)
    analyzer      = ($caWarn    | Group-Object | Sort-Object Count -Descending | Select-Object Name, Count)
    style         = ($ideWarn   | Group-Object | Sort-Object Count -Descending | Select-Object Name, Count)
    nuget         = ($nuWarn    | Group-Object | Sort-Object Count -Descending | Select-Object Name, Count)
    vulnerable    = (Select-String -Path $vulnLog -Pattern ">" -SimpleMatch:$false | ForEach-Object { $_.Line })
    outdated      = (Select-String -Path $outLog -Pattern ">" -SimpleMatch:$false | ForEach-Object { $_.Line })
}

# Persistir summary.json
$summaryPath = Join-Path $issuesDir "summary.json"
$summary | ConvertTo-Json -Depth 10 | Out-File -FilePath $summaryPath -Encoding utf8

Write-Host ""
Write-Host "[OK] summary.json escrito en: $summaryPath" -ForegroundColor Green
Write-Host ""
Write-Host "Para regenerar code-quality-report.md ejecuta el agente /code-quality," -ForegroundColor Yellow
Write-Host "o invoca: pwsh ./scripts/render-report.ps1" -ForegroundColor Yellow
