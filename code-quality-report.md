# Reporte de Calidad de Código - ContosoDashboard

> **Fecha de generación**: 2026-06-13 06:05
> **Versión del proyecto (commit)**: 5cf4f42
> **Branch**: 001-documents-management
> **Scope analizado**: `ContosoDashboard.slnx`

## Resumen Ejecutivo

| Métrica | Valor | Estado |
|---------|-------|--------|
| Cobertura de líneas | 76.8% | 🟡 Amarillo |
| Cobertura de branches | 62% | 🟡 Amarillo |
| Duplicación de código | 2.15% | 🟢 Verde |
| Errores de compilación | 0 | 🟢 Verde |
| Warnings críticos | 13 | 🟢 Verde |
| Vulnerabilidades NuGet | 78 | 🔴 Rojo |

Umbrales: 🟢 ≥80% cobertura, ≤3% duplicación, 0 errores, 0 críticos, 0 vulnerabilidades
Cobertura media 🟡 60-79% · Duplicación media 🟡 3-7%

**Veredicto**: ❌ **Rechazado**

---

## 1. Cobertura de Código

### 1.1 Métricas Generales
- Cobertura de líneas: **76.8%**
- Cobertura de branches: **62%**
- Cobertura de métodos: **58.5%**

> Artefacto HTML: `quality-reports/coverage/index.html`

### 1.2 Top 10 archivos con menor cobertura
> _Origen: `quality-reports/coverage/Cobertura.xml` (parseado por `scripts/_enrich-and-render.ps1`)._

**Distribución**: 11 archivos sin tests · 2 parcialmente cubiertos · 11 ≥80% (de un total de 24 archivos analizados).


| Archivo | Líneas Cubiertas | Total | % |
|---------|------------------|-------|---|
| ContosoDashboard\Services\UserService.cs | 0 | 3 | 0% |
| ContosoDashboard\Services\ProjectService.cs | 0 | 3 | 0% |
| ContosoDashboard\Services\ActivityLogCleanupService.cs | 0 | 4 | 0% |
| ContosoDashboard\Services\TaskService.cs | 0 | 4 | 0% |
| ContosoDashboard\Models\Project.cs | 0 | 5 | 0% |
| ContosoDashboard\Services\CustomAuthenticationStateProvider.cs | 0 | 7 | 0% |
| ContosoDashboard\Services\ActivityLogCleanupBackgroundService.cs | 0 | 9 | 0% |
| ContosoDashboard\Services\Documents\ActivityLogBackgroundService.cs | 0 | 9 | 0% |
| ContosoDashboard\Services\NotificationService.cs | 0 | 15 | 0% |
| ContosoDashboard\Services\NotificationQueue.cs | 0 | 23 | 0% |

### 1.3 Áreas críticas sin cobertura
- `Services/` (lógica de negocio) — _analizar_
- `Pages/` (Blazor) — _excluido por convención, se cubre con Tests.E2E.UI/Components_
- `Data/` (acceso a datos) — _analizar_
- `Components/` — _analizar_

---

## 2. Duplicación de Código

> Artefacto: `quality-reports/duplication/jscpd-report.html` · `jscpd-report.json`

### 2.1 Resumen
- Total de líneas: **4372**
- Líneas duplicadas: **94**
- Porcentaje de duplicación: **2.15%**
- Bloques duplicados: **10**
- Archivos involucrados: **44**

### 2.2 Top 5 duplicaciones
> _Origen: `quality-reports/duplication/jscpd-report.json`._

| Archivo A | Líneas | Archivo B | Líneas | Tipo |
|-----------|--------|-----------|--------|------|
| `ContosoDashboard\Pages\DocumentFiles\Download.cshtml.cs:1-10` | 10 | `ContosoDashboard\Pages\DocumentFiles\Preview.cshtml.cs:2-11` | 10 | csharp |
| `ContosoDashboard\Pages\DocumentFiles\Download.cshtml.cs:14-27` | 14 | `ContosoDashboard\Pages\DocumentFiles\Preview.cshtml.cs:15-28` | 14 | csharp |
| `ContosoDashboard\Services\DashboardService.cs:130-136` | 7 | `ContosoDashboard\Services\DashboardService.cs:168-174` | 7 | csharp |
| `ContosoDashboard\Services\Documents\ActivityLogQueue.cs:64-71` | 8 | `ContosoDashboard\Services\NotificationQueue.cs:57-64` | 8 | csharp |
| `ContosoDashboard\Services\Documents\DocumentService.cs:59-71` | 13 | `ContosoDashboard\Services\Documents\IDocumentService.cs:118-130` | 13 | csharp |

### 2.3 Recomendaciones de refactor
- Extraer métodos/helpers compartidos cuando se repitan en ≥3 lugares.
- Generalizar servicios de aplicación con interfaces base.
- Considerar generadores de código fuente (T4 / Source Generators) para CRUD repetitivo.

---

## 3. Incidencias de Código (Roslyn + Analyzers)

### 3.1 Resumen por severidad
- Errores de compilación (CS): **0**
- Warnings de compilación (CS): **3**
- Warnings de análisis (CA): **10**
- Warnings de estilo (IDE): **0**
- Warnings de NuGet (NU): **2**
- Vulnerabilidades conocidas: **78**
- Paquetes desactualizados: **76**

### 3.2 Top incidencias críticas
> _Origen: `quality-reports/issues/build.log` (primeras 10)._

| Código | Descripción | Ubicación | Gravedad |
|--------|-------------|-----------|----------|
| `CS8602` | Desreferencia de una referencia posiblemente NULL. [C:\Speck kit\ContosoDashboard\ContosoDashboard-MiniMax\ContosoDashboard\ContosoDashboard.csproj] | C:\Speck kit\ContosoDashboard\ContosoDashboard-MiniMax\ContosoDashboard\Shared\EditMetadataModal.razor:41 | warning |
| `CS8602` | Desreferencia de una referencia posiblemente NULL. [C:\Speck kit\ContosoDashboard\ContosoDashboard-MiniMax\ContosoDashboard\ContosoDashboard.csproj] | C:\Speck kit\ContosoDashboard\ContosoDashboard-MiniMax\ContosoDashboard\Services\TaskService.cs:70 | warning |
| `CS8602` | Desreferencia de una referencia posiblemente NULL. [C:\Speck kit\ContosoDashboard\ContosoDashboard-MiniMax\ContosoDashboard\ContosoDashboard.csproj] | C:\Speck kit\ContosoDashboard\ContosoDashboard-MiniMax\ContosoDashboard\Services\TaskService.cs:116 | warning |
| `CS8602` | Desreferencia de una referencia posiblemente NULL. [C:\Speck kit\ContosoDashboard\ContosoDashboard-MiniMax\ContosoDashboard\ContosoDashboard.csproj] | C:\Speck kit\ContosoDashboard\ContosoDashboard-MiniMax\ContosoDashboard\Services\TaskService.cs:190 | warning |
| `CS8604` | Posible argumento de referencia nulo para el par├ímetro "fileStream" en "Task<string?> IMimeTypeValidator.ValidateAndDetectAsync(Stream fileStream, string declaredExtension, CancellationToken ct = default(CancellationToken))". [C:\Speck kit\ContosoDashboard\ContosoDashboard-MiniMax\ContosoDashboard\ContosoDashboard.csproj] | C:\Speck kit\ContosoDashboard\ContosoDashboard-MiniMax\ContosoDashboard\Services\Documents\DocumentService.cs:112 | warning |
| `CA2007` | Puede llamar a ConfigureAwait en la tarea esperada (https://learn.microsoft.com/dotnet/fundamentals/code-analysis/quality-rules/ca2007) [C:\Speck kit\ContosoDashboard\ContosoDashboard-MiniMax\ContosoDashboard\ContosoDashboard.csproj] | C:\Speck kit\ContosoDashboard\ContosoDashboard-MiniMax\ContosoDashboard\Services\UserService.cs:29 | warning |
| `CA1304` | El comportamiento de "string.ToLower()" podr├¡a variar dependiendo de la configuraci├│n regional del usuario local. Reemplace esta llamada en "UserService.GetUserByEmailAsync(string)" por una llamada a "string.ToLower(CultureInfo)". (https://learn.microsoft.com/dotnet/fundamentals/code-analysis/quality-rules/ca1304) [C:\Speck kit\ContosoDashboard\ContosoDashboard-MiniMax\ContosoDashboard\ContosoDashboard.csproj] | C:\Speck kit\ContosoDashboard\ContosoDashboard-MiniMax\ContosoDashboard\Services\UserService.cs:35 | warning |
| `CA1304` | El comportamiento de "string.ToLower()" podr├¡a variar dependiendo de la configuraci├│n regional del usuario local. Reemplace esta llamada en "UserService.GetUserByEmailAsync(string)" por una llamada a "string.ToLower(CultureInfo)". (https://learn.microsoft.com/dotnet/fundamentals/code-analysis/quality-rules/ca1304) [C:\Speck kit\ContosoDashboard\ContosoDashboard-MiniMax\ContosoDashboard\ContosoDashboard.csproj] | C:\Speck kit\ContosoDashboard\ContosoDashboard-MiniMax\ContosoDashboard\Services\UserService.cs:35 | warning |
| `CA2007` | Puede llamar a ConfigureAwait en la tarea esperada (https://learn.microsoft.com/dotnet/fundamentals/code-analysis/quality-rules/ca2007) [C:\Speck kit\ContosoDashboard\ContosoDashboard-MiniMax\ContosoDashboard\ContosoDashboard.csproj] | C:\Speck kit\ContosoDashboard\ContosoDashboard-MiniMax\ContosoDashboard\Services\UserService.cs:34 | warning |
| `CA1308` | En el m├®todo "OnGetAsync", reemplace la llamada a "ToLowerInvariant" por "ToUpperInvariant". (https://learn.microsoft.com/dotnet/fundamentals/code-analysis/quality-rules/ca1308) [C:\Speck kit\ContosoDashboard\ContosoDashboard-MiniMax\ContosoDashboard\ContosoDashboard.csproj] | C:\Speck kit\ContosoDashboard\ContosoDashboard-MiniMax\ContosoDashboard\Pages\AdminReports\Documents.cshtml.cs:32 | warning |

### 3.3 Sugerencias de aplicación
- Aplicar `dotnet format` para correcciones automáticas de estilo.
- Activar `<EnforceCodeStyleInBuild>true</EnforceCodeStyleInBuild>` en CI.
- Revisar manualmente las reglas CA con severidad ≥ "warning".

---

## 4. Auditoría de Dependencias NuGet

### 4.1 Vulnerabilidades conocidas
> _Origen: `quality-reports/issues/vulnerable.txt` (parseado)._

| Paquete | Versión actual | CVE | Severidad | Fix recomendado |
|---------|----------------|-----|-----------|-----------------|
| `Microsoft.Extensions.Caching.Memory` | 8.0.0 | [link](https://github.com/advisories/GHSA-qj66-m88j-hmgj) | High | Actualizar a la última estable |
| `Azure.Identity` | 1.7.0 | [link](https://github.com/advisories/GHSA-m5vv-6r4h-3vj9) |  | Actualizar a la última estable |
| `Microsoft.Data.SqlClient` | 5.1.1 | [link](https://github.com/advisories/GHSA-98g6-xh36-x2p7) |  | Actualizar a la última estable |
| `Microsoft.Identity.Client` | 4.56.0 | [link](https://github.com/advisories/GHSA-x674-v45j-fwxw) |  | Actualizar a la última estable |
| `Microsoft.IdentityModel.JsonWebTokens` | 7.0.3 | [link](https://github.com/advisories/GHSA-59j7-ghrg-fj52) |  | Actualizar a la última estable |
| `System.Formats.Asn1` | 8.0.0-rc.1.23419.4 | [link](https://github.com/advisories/GHSA-447r-wph3-92pm) |  | Actualizar a la última estable |
| `System.IdentityModel.Tokens.Jwt` | 7.0.3 | [link](https://github.com/advisories/GHSA-59j7-ghrg-fj52) |  | Actualizar a la última estable |
| `System.Text.Json` | 8.0.0 | [link](https://github.com/advisories/GHSA-hh2w-p6rv-4g7w) |  | Actualizar a la última estable |
| `Azure.Identity` | 1.7.0 | [link](https://github.com/advisories/GHSA-m5vv-6r4h-3vj9) |  | Actualizar a la última estable |
| `Microsoft.Data.SqlClient` | 5.1.1 | [link](https://github.com/advisories/GHSA-98g6-xh36-x2p7) |  | Actualizar a la última estable |

### 4.2 Paquetes desactualizados
> _Top 10 desde `quality-reports/issues/outdated.txt` (parseado)._

✅ Todos los paquetes están al día.

---

## 5. Recomendaciones y Plan de Acción

### Prioridad Alta (esta semana)
1. Corregir **0** errores de compilación si los hay (build rojo).
2. Resolver **78** vulnerabilidades detectadas (actualizar paquetes afectados).
3. Subir cobertura por debajo del 60% si aplica.

### Prioridad Media (este sprint)
1. Reducir duplicación a <3% (extraer métodos compartidos).
2. Resolver warnings de análisis (CA) más frecuentes.
3. Actualizar paquetes NuGet no críticos.

### Prioridad Baja (backlog)
1. Endurecer reglas de stylecop/analzers.
2. Incorporar property-based testing en Services/.
3. Añadir benchmarks para Paths críticos en `Tests.Performance`.

---

## 6. Comandos Reproducibles

`powershell
# Análisis completo (recomendado)
pwsh ./scripts/run-code-quality.ps1

# Paso 1 - Cobertura
dotnet test ContosoDashboard.slnx 
  --settings coverage.runsettings 
  --collect:"XPlat Code Coverage" 
  --results-directory ./TestResults 
  --nologo

reportgenerator 
  -reports:"./TestResults/**/coverage.cobertura.xml" 
  -targetdir:"./quality-reports/coverage" 
  -reporttypes:"Html_Dark;Html_Light;Badges;TextSummary;MarkdownSummaryGithub;Cobertura" 
  -assemblyfilters:"+ContosoDashboard*" 
  -classfilters:"-ContosoDashboard.Migrations.*;-ContosoDashboard.Pages.*;-ContosoDashboard.Shared.*"

# Paso 2 - Duplicación
npx jscpd ContosoDashboard 
  --output quality-reports/duplication 
  --reporters "html,json,markdown" 
  --min-lines 5 --min-tokens 50 
  --ignore "**/bin/**,**/obj/**,**/Migrations/**,**/wwwroot/**"

# Paso 3 - Incidencias
dotnet build ContosoDashboard.slnx -c Release 
  -p:TreatWarningsAsErrors=false 
  -p:EnforceCodeStyleInBuild=true 
  --no-incremental

dotnet list ContosoDashboard/ContosoDashboard.csproj package --vulnerable --include-transitive
dotnet list ContosoDashboard.slnx package --outdated

# Renderizar reporte
pwsh ./scripts/render-report.ps1 -OutputReport code-quality-report.md
`

---

## Artefactos generados

- `quality-reports/coverage/index.html` — Reporte HTML de cobertura
- `quality-reports/coverage/Summary.txt` — Resumen legible
- `quality-reports/duplication/jscpd-report.html` — Duplicación visual
- `quality-reports/duplication/jscpd-report.json` — Datos crudos
- `quality-reports/issues/build.log` — Salida de `dotnet build`
- `quality-reports/issues/vulnerable.txt` — Vulnerabilidades NuGet
- `quality-reports/issues/outdated.txt` — Paquetes desactualizados
- `quality-reports/issues/summary.json` — Resumen estructurado

---

> Generado por el agente `/code-quality` — versión orquestador PowerShell.
> Script de análisis: `scripts/run-code-quality.ps1`
> Script de renderizado: `scripts/render-report.ps1`