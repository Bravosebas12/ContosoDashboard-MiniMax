#Requires -Version 5.1
<#
.SYNOPSIS
    Orquestador del analisis de calidad de codigo para proyectos .NET.

.DESCRIPTION
    Ejecuta cobertura, incidencias y duplicacion. Genera code-quality-report.md.
#>
[CmdletBinding()]
param(
    [string]$Scope = 'ContosoDashboard',
    [string]$Output = 'code-quality-report.md',
    [switch]$SkipTests,
    [switch]$SkipDuplication,
    [int]$DuplicationThreshold = 5,
    [int]$CoverageThreshold = 80
)

$ErrorActionPreference = 'Continue'
$ProgressPreference    = 'SilentlyContinue'

# ─── Helpers ────────────────────────────────────────────────────────────────
function Write-Section([string]$Message, [string]$Color = 'Cyan') {
    Write-Host ""
    Write-Host "================================================================" -ForegroundColor $Color
    Write-Host " $Message" -ForegroundColor $Color
    Write-Host "================================================================" -ForegroundColor $Color
}
function Test-Command([string]$Name) {
    return [bool](Get-Command $Name -ErrorAction SilentlyContinue)
}
function Fmt-Pct($v) {
    if ($null -eq $v -or $v -eq '') { return 'N/A' }
    return ('{0:N1}%' -f [double]$v)
}
function Get-Verdict([int]$s) {
    if ($s -eq 0) { 'OK' } elseif ($s -eq 1) { 'WARN' } else { 'FAIL' }
}
function Get-VerdictIcon([int]$s) {
    if ($s -eq 0) { '[OK]' } elseif ($s -eq 1) { '[WARN]' } else { '[FAIL]' }
}

# ─── Inicializacion ─────────────────────────────────────────────────────────
$timestamp      = Get-Date -Format 'yyyy-MM-dd HH:mm'
$repoRoot       = (Resolve-Path "$PSScriptRoot/../..").Path
$reportDir      = Join-Path $repoRoot 'quality-reports'
$coverageDir    = Join-Path $reportDir 'coverage'
$duplicationDir = Join-Path $reportDir 'duplication'
$testResults    = Join-Path $repoRoot 'TestResults'
$buildLog       = Join-Path $reportDir 'build.log'
$reportPath     = Join-Path $repoRoot $Output

New-Item -ItemType Directory -Force -Path $reportDir, $coverageDir, $duplicationDir, $testResults | Out-Null

$scopePath    = Resolve-Path $Scope -ErrorAction SilentlyContinue
if (-not $scopePath) { Write-Warning "Scope no encontrado: $Scope"; $scopePath = Get-Location }

$testProjects = Get-ChildItem -Path $Scope -Recurse -Filter '*.Tests.csproj' -ErrorAction SilentlyContinue
$hasTests     = $testProjects.Count -gt 0

$dotnetVer    = (& dotnet --version) 2>$null
$coverageVer  = (& dotnet-coverage --version 2>$null) -replace '\+.*$',''
# reportgenerator no tiene --version, obtenemos de la lista de dotnet tools
$reportGenVer = (& dotnet tool list -g 2>$null | Where-Object { $_ -match 'reportgenerator' } | ForEach-Object { ($_ -split '\s+')[1] } | Select-Object -First 1)
if (-not $reportGenVer) { $reportGenVer = 'N/A' }
$jscpdVer     = (& jscpd --version 2>$null)
$toolsFooter  = "dotnet $dotnetVer | dotnet-coverage $coverageVer | reportgenerator $reportGenVer | jscpd $jscpdVer"

Write-Section "Code Quality Validation Report" "Magenta"
Write-Host "  Scope:   $scopePath" -ForegroundColor Gray
Write-Host "  Output:  $Output"   -ForegroundColor Gray
Write-Host "  Tests:   $(if($hasTests){'detected'}else{'none'})" -ForegroundColor Gray
Write-Host "  jscpd:   $(if(Test-Command 'jscpd'){'installed'}else{'NOT INSTALLED'})" -ForegroundColor Gray

# ─── 1) COBERTURA ────────────────────────────────────────────────────────────
$coverageData = @{
    Available = $hasTests -and -not $SkipTests -and (Test-Command 'dotnet-coverage')
    HasReport = $false
    LinePct   = $null
    BranchPct = $null
    MethodPct = $null
}

if ($hasTests -and -not $SkipTests) {
    Write-Section "1/3 - Code Coverage" "Cyan"
    if (-not (Test-Command 'dotnet-coverage')) {
        Write-Warning "dotnet-coverage no instalado. Saltando cobertura."
        $coverageData.Available = $false
    } else {
        Remove-Item -Recurse -Force -ErrorAction SilentlyContinue $testResults
        Write-Host "  Ejecutando dotnet test con cobertura..." -ForegroundColor Gray

        $runsettings = Resolve-Path (Join-Path $repoRoot 'coverage.runsettings') -ErrorAction SilentlyContinue
        $rsArgs = @()
        if ($runsettings) { $rsArgs = @('--settings', "`"$runsettings`"") }

        $testLog = Join-Path $reportDir 'tests.log'
        & dotnet test $Scope @rsArgs `
            --collect:"XPlat Code Coverage" `
            --results-directory $testResults `
            --nologo *>&1 | Out-File -FilePath $testLog -Encoding utf8

        if (Test-Command 'reportgenerator') {
            Write-Host "  Generando ReportGenerator..." -ForegroundColor Gray
            & reportgenerator `
                "-reports:$testResults/**/coverage.opencover.xml" `
                "-targetdir:$coverageDir" `
                "-reporttypes:Html;TextSummary;MarkdownSummaryGithub;Cobertura" `
                *>&1 | Out-Null

            $sumFile = Join-Path $coverageDir 'SummaryGithub.md'
            if (Test-Path $sumFile) {
                $coverageData.HasReport = $true
                foreach ($l in (Get-Content $sumFile)) {
                    if ($l -match 'Line coverage:\s*\*\*([\d\.]+)%')   { $coverageData.LinePct   = [double]$Matches[1] }
                    if ($l -match 'Branch coverage:\s*\*\*([\d\.]+)%') { $coverageData.BranchPct = [double]$Matches[1] }
                    if ($l -match 'Method coverage:\s*\*\*([\d\.]+)%') { $coverageData.MethodPct = [double]$Matches[1] }
                }
            }
        } else {
            Write-Warning "reportgenerator no instalado."
        }
    }
} else {
    Write-Section "1/3 - Code Coverage (skipped)" "DarkGray"
}

# ─── 2) INCIDENCIAS ─────────────────────────────────────────────────────────
Write-Section "2/3 - Code Issues (Roslyn + NuGet)" "Yellow"

$issues = @{
    CompileErrors   = 0
    Warnings        = 0
    NuGetWarnings   = 0
    Vulnerabilities = @()
    Outdated        = 0
    TopCodes        = @()
    BuildOk         = $true
}

Write-Host "  Ejecutando dotnet build..." -ForegroundColor Gray
$buildOutput = & dotnet build $Scope --no-incremental --verbosity normal 2>&1
$buildOutput | Out-File -FilePath $buildLog -Encoding utf8
$buildText = ($buildOutput -join "`n")

$issues.CompileErrors = ([regex]::Matches($buildText, 'error CS\d+')).Count
$issues.Warnings      = ([regex]::Matches($buildText, 'warning CS\d+')).Count
$issues.NuGetWarnings = ([regex]::Matches($buildText, 'warning NU\d+')).Count
$issues.BuildOk       = ($issues.CompileErrors -eq 0)

$topCodes = @{}
foreach ($m in [regex]::Matches($buildText, '(error|warning)\s+(CS|NU|CA)\d+')) {
    $key = "$($m.Groups[1].Value) $($m.Groups[2].Value)"
    $topCodes[$key] = ($topCodes[$key] + 1)
}
$issues.TopCodes = @($topCodes.GetEnumerator() | Sort-Object Value -Descending | Select-Object -First 10)

Write-Host "  Auditando vulnerabilidades NuGet..." -ForegroundColor Gray
if (Test-Command 'dotnet') {
    $vulnOut = & dotnet list $Scope package --vulnerable --include-transitive 2>&1
    $vulnText = $vulnOut -join "`n"
    $vulnLines = $vulnText -split "`n" | Where-Object { $_ -match '^\s*>\s+' }
    foreach ($l in $vulnLines) { $issues.Vulnerabilities += ($l.Trim() -replace '\s+', ' ') }
}

Write-Host "  Listando paquetes desactualizados..." -ForegroundColor Gray
if (Test-Command 'dotnet') {
    $outOut = & dotnet list $Scope package --outdated 2>&1
    $outText = $outOut -join "`n"
    $issues.Outdated = ([regex]::Matches($outText, 'has the following newer versions')).Count
}

# ─── 3) DUPLICACION ─────────────────────────────────────────────────────────
$dupData = @{
    Available       = $false
    Percent         = $null
    TotalLines      = $null
    DuplicatedLines = $null
    Blocks          = $null
    Files           = $null
    ReportPath      = $null
    Error           = $null
}

if (-not $SkipDuplication) {
    Write-Section "3/3 - Code Duplication" "Green"
    if (-not (Test-Command 'jscpd')) {
        Write-Warning "jscpd no instalado. Saltando duplicacion."
        $dupData.Error = "jscpd not installed"
    } else {
        Write-Host "  Ejecutando jscpd..." -ForegroundColor Gray
        Get-ChildItem -Path $duplicationDir -ErrorAction SilentlyContinue | Remove-Item -Recurse -Force -ErrorAction SilentlyContinue

        $jscpdConfig = Join-Path $repoRoot '.jscpd.json'
        $jscpdArgs = @($Scope)
        if (Test-Path $jscpdConfig) { $jscpdArgs += @('--config', "`"$jscpdConfig`"") }

        $jscpdLog = Join-Path $reportDir 'jscpd.log'
        & jscpd @jscpdArgs 2>&1 | Out-File -FilePath $jscpdLog -Encoding utf8

        $jscpdMd = Get-ChildItem -Path $duplicationDir -Recurse -Filter 'jscpd-report.md' -ErrorAction SilentlyContinue | Select-Object -First 1
        if (-not $jscpdMd) { $jscpdMd = Get-ChildItem -Path $duplicationDir -Recurse -Filter '*.md' -ErrorAction SilentlyContinue | Select-Object -First 1 }
        if ($jscpdMd) {
            $dupData.Available  = $true
            $dupData.ReportPath = $jscpdMd.FullName
            $jscpdText = Get-Content $jscpdMd.FullName -Raw
            # Formato real: "Found N exact clones with N(X.XX%) duplicated lines in N (1 formats) files."
            if ($jscpdText -match 'Found\s+(\d+)\s+exact\s+clones\s+with\s+(\d+)\(([\d\.]+)%\)') {
                $dupData.Blocks          = $Matches[1]
                $dupData.DuplicatedLines = $Matches[2]
                $dupData.Percent         = [double]$Matches[3]
            }
            if ($jscpdText -match 'in\s+(\d+)\s+\(\d+\s+formats?\)\s+files') { $dupData.Files = $Matches[1] }
            # Tabla: "csharp | 17 | 1465 | 7601 | 3 | 21 (1.43%) | 207 (2.72%)"
            if ($jscpdText -match 'csharp\s*\|\s*(\d+)\s*\|\s*(\d+)\s*\|\s*(\d+)\s*\|\s*\d+\s*\|\s*\d+') {
                if (-not $dupData.Files)      { $dupData.Files      = $Matches[1] }
                $dupData.TotalLines = $Matches[2]
            }
        } else {
            Write-Warning "No se encontro el reporte markdown de jscpd"
            $dupData.Error = "Report not generated"
        }
    }
} else {
    Write-Section "3/3 - Code Duplication (skipped)" "DarkGray"
}

# ─── 4) VEREDICTOS ──────────────────────────────────────────────────────────
$covScore = 0
if ($coverageData.Available -and $coverageData.HasReport) {
    if     ($coverageData.LinePct -ge $CoverageThreshold) { $covScore = 0 }
    elseif ($coverageData.LinePct -ge ($CoverageThreshold/2)) { $covScore = 1 }
    else { $covScore = 2 }
}
$errScore  = if ($issues.CompileErrors -eq 0) { 0 } elseif ($issues.CompileErrors -le 5) { 1 } else { 2 }
$warnScore = if ($issues.Warnings -le 20) { 0 } elseif ($issues.Warnings -le 50) { 1 } else { 2 }
$vulnScore = if ($issues.Vulnerabilities.Count -eq 0) { 0 } elseif ($issues.Vulnerabilities.Count -le 2) { 1 } else { 2 }
$dupScore  = if (-not $dupData.Available) { 0 }
             elseif ($dupData.Percent -lt 3) { 0 }
             elseif ($dupData.Percent -lt $DuplicationThreshold) { 1 } else { 2 }
$worstScore = [Math]::Max([Math]::Max($covScore, $errScore), [Math]::Max($warnScore, [Math]::Max($vulnScore, $dupScore)))
$verdict = switch ($worstScore) {
    0 { '[OK] PASSED' }
    1 { '[WARN] PASSED WITH OBSERVATIONS' }
    2 { '[FAIL] ACTION REQUIRED' }
    default { '[?] INDETERMINATE' }
}

# ─── 5) GENERAR .MD ─────────────────────────────────────────────────────────
Write-Section "Generating Markdown Report" "Magenta"

$covCell  = if ($coverageData.Available -and $coverageData.HasReport) { "$(Fmt-Pct $coverageData.LinePct) (line)" } else { 'N/A' }
$covIcon  = if ($coverageData.Available -and $coverageData.HasReport) { Get-VerdictIcon $covScore } else { '[--]' }
$dupCell  = if ($dupData.Available) { (Fmt-Pct $dupData.Percent) } else { 'N/A' }
$dupIcon  = if ($dupData.Available) { Get-VerdictIcon $dupScore } else { '[--]' }

# Tabla de incidencias - construir como array, no como strings concatenados
$issuesRows = @()
if ($issues.TopCodes.Count -eq 0) {
    $issuesRows += '| -- | _Sin incidencias_ | 0 | [OK] |'
} else {
    $i = 0
    foreach ($c in $issues.TopCodes) {
        $i++
        $sev = if ($c.Key -like 'error*') { '[FAIL]' } else { '[WARN]' }
        # usar Format con placeholders sin '|'
        $issuesRows += ('| {0} | `{1}` | {2} | {3} |' -f $i, $c.Key, $c.Value, $sev)
    }
}
$issuesTable = ($issuesRows -join "`n")

# Tabla de vulnerabilidades
if ($issues.Vulnerabilities.Count -eq 0) {
    $vulnTable = "`n[OK] No se detectaron vulnerabilidades conocidas.`n"
} else {
    $vulnRows = @()
    $j = 0
    foreach ($v in $issues.Vulnerabilities) {
        $j++
        $vulnRows += ('| {0} | `{1}` |' -f $j, $v)
    }
    $vulnTable = "`n| # | Detalle |`n|---|--------|`n" + ($vulnRows -join "`n") + "`n"
}

# Seccion de cobertura
if ($coverageData.Available -and $coverageData.HasReport) {
    $coverageSection = @"

### 1.1 Resumen de Cobertura

| Metrica | Porcentaje |
|---------|-----------:|
| Lineas  | $(Fmt-Pct $coverageData.LinePct) |
| Branches | $(Fmt-Pct $coverageData.BranchPct) |
| Metodos | $(Fmt-Pct $coverageData.MethodPct) |

Reporte HTML completo: `quality-reports/coverage/index.html`

"@
} else {
    $coverageSection = @"

> [!WARNING] **No disponible.** No se detectaron proyectos de tests.
> Para habilitarla, cree un proyecto `*.Tests.csproj` dentro de `$Scope`.

"@
}

# Seccion de duplicacion
if ($dupData.Available) {
    $relPath = ($dupData.ReportPath -replace [regex]::Escape($repoRoot), '').TrimStart('\','/') -replace '\\','/'
    $duplicationSection = @"

### 3.1 Resumen de Duplicacion

| Metrica | Valor |
|---------|------:|
| Total de lineas | $($dupData.TotalLines) |
| Lineas duplicadas | $($dupData.DuplicatedLines) |
| Porcentaje de duplicacion | $(Fmt-Pct $dupData.Percent) |
| Bloques duplicados | $($dupData.Blocks) |
| Archivos involucrados | $($dupData.Files) |

Reporte completo: ``$relPath``

"@
} else {
    $duplicationSection = @"

> [!WARNING] **No disponible.** jscpd no se ejecuto correctamente.
> Instalar con: `npm install -g jscpd cpd-windows-x64-msvc`
> $($dupData.Error)

"@
}

# ─── Construir reporte completo (todo dentro de here-strings) ───────────────
$md = @"
# Reporte de Validacion de Codigo

> **Proyecto**: ContosoDashboard
> **Scope analizado**: `` $Scope ``
> **Fecha**: $timestamp
> **Herramientas**: $toolsFooter

---

## Resumen Ejecutivo

| Dimension | Estado | Metrica | Umbral |
|-----------|:------:|---------|--------|
| Cobertura de codigo        | $covIcon  | $covCell | >= $CoverageThreshold% verde / >= $($CoverageThreshold/2)% amarillo |
| Incidencias (errores)      | $(Get-VerdictIcon $errScore)  | $($issues.CompileErrors) errores       | 0 verde / <=5 amarillo |
| Incidencias (warnings)     | $(Get-VerdictIcon $warnScore) | $($issues.Warnings) warnings           | <=20 verde / <=50 amarillo |
| Vulnerabilidades NuGet     | $(Get-VerdictIcon $vulnScore) | $($issues.Vulnerabilities.Count) vulns | 0 verde / <=2 amarillo |
| Duplicacion de codigo      | $dupIcon  | $dupCell | <3% verde / <$DuplicationThreshold% amarillo |

**Veredicto final**: $verdict

---

## 1. Cobertura de Codigo

_Generado por ReportGenerator + dotnet-coverage (formato OpenCover)._
$coverageSection
---

## 2. Incidencias de Codigo (Roslyn / NuGet)

_Generado por `dotnet build` + `dotnet list package --vulnerable --outdated`._

### 2.1 Resumen por Severidad

| Severidad | Cantidad |
|-----------|---------:|
| Errores de compilacion (CS) | $($issues.CompileErrors) |
| Warnings de compilador      | $($issues.Warnings) |
| Warnings de NuGet (NU)      | $($issues.NuGetWarnings) |
| Vulnerabilidades conocidas  | $($issues.Vulnerabilities.Count) |
| Paquetes desactualizados    | $($issues.Outdated) |

### 2.2 Top 10 - Incidencias Mas Frecuentes

| # | Codigo | Ocurrencias | Gravedad |
|---|--------|------------:|----------|
$issuesTable

### 2.3 Vulnerabilidades Detectadas
$vulnTable
---

## 3. Duplicacion de Codigo

_Generado por jscpd (umbral minimo: 5 lineas, 50 tokens)._
$duplicationSection
---

## 4. Recomendaciones Priorizadas

| # | Prioridad | Accion | Dimension | Impacto |
|---|-----------|--------|-----------|---------|
| 1 | ALTA    | Resolver $($issues.CompileErrors) errores de compilacion   | Incidencias | Bloqueante |
| 2 | MEDIA   | Reducir warnings a < 20                                      | Incidencias | Mantenibilidad |
| 3 | MEDIA   | Crear tests unitarios (sin cobertura actual)                  | Cobertura   | Riesgo de regresion |
| 4 | MEDIA   | Auditar $($issues.Vulnerabilities.Count) vulnerabilidades NuGet | Seguridad   | CVE expuesto |
| 5 | BAJA    | Refactorizar bloques duplicados                              | Duplicacion | Mantenibilidad |

---

## 5. Comandos Reproducibles

``````powershell
# Re-ejecutar este analisis completo
.\.github\scripts\code-quality-report.ps1 -Scope "$Scope" -Output "$Output"

# Analisis individuales
dotnet test $Scope --settings ./coverage.runsettings --collect:"XPlat Code Coverage"
reportgenerator "-reports:./TestResults/**/coverage.opencover.xml" "-targetdir:./quality-reports/coverage" "-reporttypes:Html;MarkdownSummaryGithub"
dotnet build $Scope --no-incremental --verbosity normal
dotnet list $Scope package --vulnerable --include-transitive
jscpd $Scope --config ./.jscpd.json
``````

---

> Generado por el agente `/code-quality` -> `.github/agents/code-quality.agent.md`
> Artefactos en: `quality-reports/`  (cobertura, duplicacion, logs)
"@

# Guardar reporte con UTF-8 BOM para preservar caracteres especiales en Windows
$utf8Bom = New-Object System.Text.UTF8Encoding $true
[System.IO.File]::WriteAllText($reportPath, $md, $utf8Bom)

Write-Section "Reporte generado" "Green"
Write-Host "  Archivo: $reportPath" -ForegroundColor White
Write-Host ""
Write-Host "  Veredicto: $verdict" -ForegroundColor $(if($worstScore -eq 0){'Green'}elseif($worstScore -eq 1){'Yellow'}else{'Red'})
Write-Host ""
Write-Host "  Metricas clave:" -ForegroundColor Cyan
Write-Host ("    - Cobertura (lineas):     {0}" -f $covCell)
Write-Host ("    - Errores de compilacion: {0}" -f $issues.CompileErrors)
Write-Host ("    - Warnings:               {0}" -f $issues.Warnings)
Write-Host ("    - Vulnerabilidades NuGet: {0}" -f $issues.Vulnerabilities.Count)
Write-Host ("    - Duplicacion:            {0}" -f $dupCell)
Write-Host ""
