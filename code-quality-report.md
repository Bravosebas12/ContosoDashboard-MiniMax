# Reporte de Validacion de Codigo

> **Proyecto**: ContosoDashboard
> **Scope analizado**: ` ContosoDashboard `
> **Fecha**: 2026-06-12 10:52
> **Herramientas**: dotnet 10.0.301 | dotnet-coverage 18.8.0 | reportgenerator 5.5.10 | jscpd cpd 5.0.8

---

## Resumen Ejecutivo

| Dimension | Estado | Metrica | Umbral |
|-----------|:------:|---------|--------|
| Cobertura de codigo        | [--]  | N/A | >= 80% verde / >= 40% amarillo |
| Incidencias (errores)      | [OK]  | 0 errores       | 0 verde / <=5 amarillo |
| Incidencias (warnings)     | [OK] | 6 warnings           | <=20 verde / <=50 amarillo |
| Vulnerabilidades NuGet     | [FAIL] | 8 vulns | 0 verde / <=2 amarillo |
| Duplicacion de codigo      | [OK]  | 1,4% | <3% verde / <5% amarillo |

**Veredicto final**: [FAIL] ACTION REQUIRED

---

## 1. Cobertura de Codigo

_Generado por ReportGenerator + dotnet-coverage (formato OpenCover)._

> [!WARNING] **No disponible.** No se detectaron proyectos de tests.
> Para habilitarla, cree un proyecto *.Tests.csproj dentro de $Scope.

---

## 2. Incidencias de Codigo (Roslyn / NuGet)

_Generado por dotnet build + dotnet list package --vulnerable --outdated._

### 2.1 Resumen por Severidad

| Severidad | Cantidad |
|-----------|---------:|
| Errores de compilacion (CS) | 0 |
| Warnings de compilador      | 6 |
| Warnings de NuGet (NU)      | 8 |
| Vulnerabilidades conocidas  | 8 |
| Paquetes desactualizados    | 0 |

### 2.2 Top 10 - Incidencias Mas Frecuentes

| # | Codigo | Ocurrencias | Gravedad |
|---|--------|------------:|----------|
| 1 | `warning NU` | 8 | [WARN] |
| 2 | `warning CS` | 6 | [WARN] |

### 2.3 Vulnerabilidades Detectadas

| # | Detalle |
|---|--------|
| 1 | `> Azure.Identity 1.7.0 Moderate https://github.com/advisories/GHSA-m5vv-6r4h-3vj9` |
| 2 | `> Microsoft.Data.SqlClient 5.1.1 High https://github.com/advisories/GHSA-98g6-xh36-x2p7` |
| 3 | `> Microsoft.Extensions.Caching.Memory 8.0.0 High https://github.com/advisories/GHSA-qj66-m88j-hmgj` |
| 4 | `> Microsoft.Identity.Client 4.56.0 Low https://github.com/advisories/GHSA-x674-v45j-fwxw` |
| 5 | `> Microsoft.IdentityModel.JsonWebTokens 7.0.3 Moderate https://github.com/advisories/GHSA-59j7-ghrg-fj52` |
| 6 | `> System.Formats.Asn1 8.0.0-rc.1.23419.4 High https://github.com/advisories/GHSA-447r-wph3-92pm` |
| 7 | `> System.IdentityModel.Tokens.Jwt 7.0.3 Moderate https://github.com/advisories/GHSA-59j7-ghrg-fj52` |
| 8 | `> System.Text.Json 8.0.0 High https://github.com/advisories/GHSA-hh2w-p6rv-4g7w` |

---

## 3. Duplicacion de Codigo

_Generado por jscpd (umbral minimo: 5 lineas, 50 tokens)._

### 3.1 Resumen de Duplicacion

| Metrica | Valor |
|---------|------:|
| Total de lineas | 1465 |
| Lineas duplicadas | 21 |
| Porcentaje de duplicacion | 1,4% |
| Bloques duplicados | 3 |
| Archivos involucrados | 17 |

Reporte completo: `quality-reports/duplication/jscpd-report.md`

---

## 4. Recomendaciones Priorizadas

| # | Prioridad | Accion | Dimension | Impacto |
|---|-----------|--------|-----------|---------|
| 1 | ALTA    | Resolver 0 errores de compilacion   | Incidencias | Bloqueante |
| 2 | MEDIA   | Reducir warnings a < 20                                      | Incidencias | Mantenibilidad |
| 3 | MEDIA   | Crear tests unitarios (sin cobertura actual)                  | Cobertura   | Riesgo de regresion |
| 4 | MEDIA   | Auditar 8 vulnerabilidades NuGet | Seguridad   | CVE expuesto |
| 5 | BAJA    | Refactorizar bloques duplicados                              | Duplicacion | Mantenibilidad |

---

## 5. Comandos Reproducibles

```powershell
# Re-ejecutar este analisis completo
.\.github\scripts\code-quality-report.ps1 -Scope "ContosoDashboard" -Output "code-quality-report.md"

# Analisis individuales
dotnet test ContosoDashboard --settings ./coverage.runsettings --collect:"XPlat Code Coverage"
reportgenerator "-reports:./TestResults/**/coverage.opencover.xml" "-targetdir:./quality-reports/coverage" "-reporttypes:Html;MarkdownSummaryGithub"
dotnet build ContosoDashboard --no-incremental --verbosity normal
dotnet list ContosoDashboard package --vulnerable --include-transitive
jscpd ContosoDashboard --config ./.jscpd.json
```

---

> Generado por el agente /code-quality -> .github/agents/code-quality.agent.md
> Artefactos en: quality-reports/  (cobertura, duplicacion, logs)