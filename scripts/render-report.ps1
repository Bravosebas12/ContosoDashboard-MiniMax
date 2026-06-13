#requires -Version 7.0
<#
.SYNOPSIS
    Renderiza el reporte Markdown a partir de los artefactos generados
    por run-code-quality.ps1 y la información del repositorio.

.DESCRIPTION
    Lee ./quality-reports/issues/summary.json y genera el reporte
    final code-quality-report.md respetando los umbrales de la
    spec del agente /code-quality.

.PARAMETER OutputReport
    Ruta del reporte Markdown a generar. Por defecto: code-quality-report.md

.PARAMETER Thresholds
    Hashtable con los umbrales. Default coincide con la spec.
#>
[CmdletBinding()]
param(
    [string] $OutputReport = "code-quality-report.md",
    [hashtable] $Thresholds = @{
        CoverageGreen   = 80
        CoverageYellow  = 60
        DupGreen        = 3
        DupYellow       = 7
    }
)

$root = Resolve-Path (Join-Path $PSScriptRoot "..")
Set-Location $root
$summaryPath = Join-Path $root "quality-reports/issues/summary.json"
if (-not (Test-Path $summaryPath)) {
    throw "No se encontro $summaryPath. Ejecute primero scripts/run-code-quality.ps1"
}
$summary = Get-Content $summaryPath -Raw | ConvertFrom-Json

function Get-Status {
    param([double]$Value, [double]$Green, [double]$Yellow, [string]$Mode = "higher")  # Mode: higher|lower
    if ($null -eq $Value) { return "N/D" }
    if ($Mode -eq "higher") {
        if ($Value -ge $Green) { return "Verde" }
        if ($Value -ge $Yellow) { return "Amarillo" }
        return "Rojo"
    } else {
        if ($Value -le $Green) { return "Verde" }
        if ($Value -le $Yellow) { return "Amarillo" }
        return "Rojo"
    }
}

function Get-Emoji($status) {
    switch ($status) {
        "Verde"    { "🟢" }
        "Amarillo" { "🟡" }
        "Rojo"     { "🔴" }
        default    { "⚪" }
    }
}

$cov     = $summary.coverage
$dup     = $summary.duplication
$issues  = $summary.issues
$branch  = if ($summary.branch) { $summary.branch } else { "(no-git)" }
$commit  = if ($summary.commit) { $summary.commit } else { "(no-git)" }
$date    = $summary.generatedAt

# Cobertura
$lineCov   = if ($cov.linePct)   { [math]::Round($cov.linePct, 2) }   else { $null }
$branchCov = if ($cov.branchPct) { [math]::Round($cov.branchPct, 2) } else { $null }
# methodPct puede llegar como numero escalar o como array [lineCount, pct] si la
# regex del orquestador matchea varias lineas (p.ej. "Method coverage" y
# "Full method coverage"). Tomamos siempre el primer escalar.
if ($cov.methodPct) {
    $mp = @($cov.methodPct) | Where-Object { $_ -is [double] -or $_ -is [int] -or $_ -is [single] } | Select-Object -First 1
    $methodCov = if ($null -ne $mp) { [math]::Round([double]$mp, 2) } else { $null }
} else {
    $methodCov = $null
}

$lineStatus   = Get-Status -Value $lineCov   -Green $Thresholds.CoverageGreen -Yellow $Thresholds.CoverageYellow
$branchStatus = Get-Status -Value $branchCov -Green $Thresholds.CoverageGreen -Yellow $Thresholds.CoverageYellow
$dupPct       = if ($dup.pct)      { [math]::Round($dup.pct, 2) }      else { $null }
$dupStatus    = Get-Status -Value $dupPct    -Green $Thresholds.DupGreen      -Yellow $Thresholds.DupYellow -Mode "lower"

$errorsCount = if ($issues.buildErrors) { ($issues.buildErrors | Measure-Object).Count } else { 0 }
$warnCount   = (($issues.buildWarnings | Measure-Object).Count) + (($issues.analyzer | Measure-Object).Count) + (($issues.style | Measure-Object).Count)
$vulnCount   = if ($issues.vulnerable) { @($issues.vulnerable).Count } else { 0 }
$outCount    = if ($issues.outdated)   { @($issues.outdated).Count   } else { 0 }

$errStatus  = if ($errorsCount -eq 0) { "Verde" } else { "Rojo" }
$warnStatus = Get-Status -Value $warnCount -Green 20 -Yellow 50 -Mode "lower"
$vulnStatus = if ($vulnCount -eq 0) { "Verde" } elseif ($vulnCount -le 2) { "Amarillo" } else { "Rojo" }

# Veredicto
$verdict =
    if (($lineStatus -eq "Rojo") -or ($errStatus -eq "Rojo") -or ($vulnStatus -eq "Rojo")) { "Rechazado" }
    elseif (($lineStatus -eq "Amarillo") -or ($warnStatus -ne "Verde") -or ($dupStatus -ne "Verde")) { "Aprobado con observaciones" }
    else { "Aprobado" }

$verdictEmoji = switch ($verdict) {
    "Aprobado"                    { "✅" }
    "Aprobado con observaciones"  { "⚠️" }
    default                       { "❌" }
}

# ---------------------------------------------------------------------
# Render markdown
# ---------------------------------------------------------------------

$md = @"
# Reporte de Calidad de Código - ContosoDashboard

> **Fecha de generación**: $date
> **Versión del proyecto (commit)**: $commit
> **Branch**: $branch
> **Scope analizado**: ``$($summary.scope)``

## Resumen Ejecutivo

| Métrica | Valor | Estado |
|---------|-------|--------|
| Cobertura de líneas | $(if ($lineCov) { "$lineCov%" } else { "N/D" }) | $(Get-Emoji $lineStatus) $lineStatus |
| Cobertura de branches | $(if ($branchCov) { "$branchCov%" } else { "N/D" }) | $(Get-Emoji $branchStatus) $branchStatus |
| Duplicación de código | $(if ($dupPct) { "$dupPct%" } else { "N/D" }) | $(Get-Emoji $dupStatus) $dupStatus |
| Errores de compilación | $errorsCount | $(Get-Emoji $errStatus) $errStatus |
| Warnings críticos | $warnCount | $(Get-Emoji $warnStatus) $warnStatus |
| Vulnerabilidades NuGet | $vulnCount | $(Get-Emoji $vulnStatus) $vulnStatus |

Umbrales: 🟢 ≥80% cobertura, ≤3% duplicación, 0 errores, 0 críticos, 0 vulnerabilidades
Cobertura media 🟡 60-79% · Duplicación media 🟡 3-7%

**Veredicto**: $verdictEmoji **$verdict**

---

## 1. Cobertura de Código

### 1.1 Métricas Generales
- Cobertura de líneas: **$(if ($lineCov) { "$lineCov%" } else { "N/D" })**
- Cobertura de branches: **$(if ($branchCov) { "$branchCov%" } else { "N/D" })**
- Cobertura de métodos: **$(if ($methodCov) { "$methodCov%" } else { "N/D" })**

> Artefacto HTML: ``quality-reports/coverage/index.html``

### 1.2 Top 10 archivos con menor cobertura
> _Origen: ``quality-reports/coverage/Cobertura.xml`` (parseado por ``scripts/_enrich-and-render.ps1``)._

$(
if ($summary.coverageDistribution) {
    $d = $summary.coverageDistribution
    "**Distribución**: $($d.SinTests) archivos sin tests · $($d.Parcial0a80) parcialmente cubiertos · $($d.Cubiertos80a100) ≥80% (de un total de $($d.TotalArchivos) archivos analizados).`n`n"
} else { "" }
)
$(
if ($summary.coverageTopFiles -and @($summary.coverageTopFiles).Count -gt 0) {
    $rows = $summary.coverageTopFiles | ForEach-Object {
        "| $($_.File) | $($_.CoveredLines) | $($_.TotalLines) | $([math]::Round([double]$_.CoveragePct, 1))% |"
    }
    "| Archivo | Líneas Cubiertas | Total | % |`n|---------|------------------|-------|---|`n$($rows -join "`n")"
} else {
    "_No se encontraron datos de cobertura por archivo (ejecuta ``scripts/_enrich-and-render.ps1``)._"
}
)

### 1.3 Áreas críticas sin cobertura
- ``Services/`` (lógica de negocio) — _analizar_
- ``Pages/`` (Blazor) — _excluido por convención, se cubre con Tests.E2E.UI/Components_
- ``Data/`` (acceso a datos) — _analizar_
- ``Components/`` — _analizar_

---

## 2. Duplicación de Código

> Artefacto: ``quality-reports/duplication/jscpd-report.html`` · ``jscpd-report.json``

### 2.1 Resumen
- Total de líneas: **$(if ($dup.totalLines) { $dup.totalLines } else { "N/D" })**
- Líneas duplicadas: **$(if ($dup.duplicatedLines) { $dup.duplicatedLines } else { "N/D" })**
- Porcentaje de duplicación: **$(if ($dupPct) { "$dupPct%" } else { "N/D" })**
- Bloques duplicados: **$(if ($dup.blocks) { $dup.blocks } else { "N/D" })**
- Archivos involucrados: **$(if ($dup.files) { $dup.files } else { "N/D" })**

### 2.2 Top 5 duplicaciones
> _Origen: ``quality-reports/duplication/jscpd-report.json``._

$(
if ($summary.duplicationTop -and @($summary.duplicationTop).Count -gt 0) {
    $rows = $summary.duplicationTop | ForEach-Object {
        $fileA = "$($_.FileA):$($_.StartA)-$($_.EndA)"
        $fileB = "$($_.FileB):$($_.StartB)-$($_.EndB)"
        "| ``$fileA`` | $($_.Lines) | ``$fileB`` | $($_.Lines) | $($_.Format) |"
    }
    "| Archivo A | Líneas | Archivo B | Líneas | Tipo |`n|-----------|--------|-----------|--------|------|`n$($rows -join "`n")"
} else {
    "_Sin duplicaciones que reportar._"
}
)

### 2.3 Recomendaciones de refactor
- Extraer métodos/helpers compartidos cuando se repitan en ≥3 lugares.
- Generalizar servicios de aplicación con interfaces base.
- Considerar generadores de código fuente (T4 / Source Generators) para CRUD repetitivo.

---

## 3. Incidencias de Código (Roslyn + Analyzers)

### 3.1 Resumen por severidad
- Errores de compilación (CS): **$errorsCount**
- Warnings de compilación (CS): **$(if ($issues.buildWarnings) { ($issues.buildWarnings | Measure-Object).Count } else { 0 })**
- Warnings de análisis (CA): **$(if ($issues.analyzer) { ($issues.analyzer | Measure-Object).Count } else { 0 })**
- Warnings de estilo (IDE): **$(if ($issues.style) { ($issues.style | Measure-Object).Count } else { 0 })**
- Warnings de NuGet (NU): **$(if ($issues.nuget) { ($issues.nuget | Measure-Object).Count } else { 0 })**
- Vulnerabilidades conocidas: **$vulnCount**
- Paquetes desactualizados: **$outCount**

### 3.2 Top incidencias críticas
> _Origen: ``quality-reports/issues/build.log`` (primeras 10)._

$(
if ($summary.issuesTop -and @($summary.issuesTop).Count -gt 0) {
    $rows = $summary.issuesTop | ForEach-Object {
        $msg = $_.Message -replace '\|', '\|' -replace "`r", '' -replace "`n", ' '
        "| ``$($_.Code)`` | $msg | $($_.File):$($_.Line) | $($_.Severity) |"
    }
    "| Código | Descripción | Ubicación | Gravedad |`n|--------|-------------|-----------|----------|`n$($rows -join "`n")"
} else {
    "_Sin incidencias críticas para reportar._"
}
)

### 3.3 Sugerencias de aplicación
- Aplicar ``dotnet format`` para correcciones automáticas de estilo.
- Activar ``<EnforceCodeStyleInBuild>true</EnforceCodeStyleInBuild>`` en CI.
- Revisar manualmente las reglas CA con severidad ≥ "warning".

---

## 4. Auditoría de Dependencias NuGet

### 4.1 Vulnerabilidades conocidas
> _Origen: ``quality-reports/issues/vulnerable.txt`` (parseado)._

$(
if ($summary.vulnerableTop -and @($summary.vulnerableTop).Count -gt 0) {
    $rows = $summary.vulnerableTop | ForEach-Object {
        $url = if ($_.Url) { "[link]($($_.Url))" } else { "-" }
        # dotnet list no muestra la version segura -> recomendar actualizar a la ultima
        $fix = "Actualizar a la última estable"
        "| ``$($_.Package)`` | $($_.Version) | $url | $($_.Severity) | $fix |"
    }
    "| Paquete | Versión actual | CVE | Severidad | Fix recomendado |`n|---------|----------------|-----|-----------|-----------------|`n$($rows -join "`n")"
} else {
    "✅ No se detectaron vulnerabilidades conocidas."
}
)

### 4.2 Paquetes desactualizados
> _Top 10 desde ``quality-reports/issues/outdated.txt`` (parseado)._

$(
if ($summary.outdatedTop -and @($summary.outdatedTop).Count -gt 0) {
    $rows = $summary.outdatedTop | ForEach-Object {
        $fix = if ($_.Resolved) { $_.Resolved } else { "-" }
        "| ``$($_.Package)`` | $($_.Version) | $fix |"
    }
    "| Paquete | Versión actual | Última estable |`n|---------|----------------|----------------|`n$($rows -join "`n")"
} else {
    "✅ Todos los paquetes están al día."
}
)

---

## 5. Recomendaciones y Plan de Acción

### Prioridad Alta (esta semana)
1. Corregir **$errorsCount** errores de compilación si los hay (build rojo).
2. Resolver **$vulnCount** vulnerabilidades detectadas (actualizar paquetes afectados).
3. Subir cobertura por debajo del 60% si aplica.

### Prioridad Media (este sprint)
1. Reducir duplicación a <3% (extraer métodos compartidos).
2. Resolver warnings de análisis (CA) más frecuentes.
3. Actualizar paquetes NuGet no críticos.

### Prioridad Baja (backlog)
1. Endurecer reglas de stylecop/analzers.
2. Incorporar property-based testing en Services/.
3. Añadir benchmarks para Paths críticos en ``Tests.Performance``.

---

## 6. Comandos Reproducibles

```powershell
# Análisis completo (recomendado)
pwsh ./scripts/run-code-quality.ps1

# Paso 1 - Cobertura
dotnet test ContosoDashboard.slnx `
  --settings coverage.runsettings `
  --collect:"XPlat Code Coverage" `
  --results-directory ./TestResults `
  --nologo

reportgenerator `
  -reports:"./TestResults/**/coverage.cobertura.xml" `
  -targetdir:"./quality-reports/coverage" `
  -reporttypes:"Html_Dark;Html_Light;Badges;TextSummary;MarkdownSummaryGithub;Cobertura" `
  -assemblyfilters:"+ContosoDashboard*" `
  -classfilters:"-ContosoDashboard.Migrations.*;-ContosoDashboard.Pages.*;-ContosoDashboard.Shared.*"

# Paso 2 - Duplicación
npx jscpd ContosoDashboard `
  --output quality-reports/duplication `
  --reporters "html,json,markdown" `
  --min-lines 5 --min-tokens 50 `
  --ignore "**/bin/**,**/obj/**,**/Migrations/**,**/wwwroot/**"

# Paso 3 - Incidencias
dotnet build ContosoDashboard.slnx -c Release `
  -p:TreatWarningsAsErrors=false `
  -p:EnforceCodeStyleInBuild=true `
  --no-incremental

dotnet list ContosoDashboard/ContosoDashboard.csproj package --vulnerable --include-transitive
dotnet list ContosoDashboard.slnx package --outdated

# Renderizar reporte
pwsh ./scripts/render-report.ps1 -OutputReport code-quality-report.md
```

---

## Artefactos generados

- ``quality-reports/coverage/index.html`` — Reporte HTML de cobertura
- ``quality-reports/coverage/Summary.txt`` — Resumen legible
- ``quality-reports/duplication/jscpd-report.html`` — Duplicación visual
- ``quality-reports/duplication/jscpd-report.json`` — Datos crudos
- ``quality-reports/issues/build.log`` — Salida de ``dotnet build``
- ``quality-reports/issues/vulnerable.txt`` — Vulnerabilidades NuGet
- ``quality-reports/issues/outdated.txt`` — Paquetes desactualizados
- ``quality-reports/issues/summary.json`` — Resumen estructurado

---

> Generado por el agente ``/code-quality`` — versión orquestador PowerShell.
> Script de análisis: ``scripts/run-code-quality.ps1``
> Script de renderizado: ``scripts/render-report.ps1``
"@

$md | Out-File -FilePath $OutputReport -Encoding utf8 -NoNewline
Write-Host "Reporte generado en: $OutputReport" -ForegroundColor Green
