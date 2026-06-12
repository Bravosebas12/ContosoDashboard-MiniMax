# Agentes Personalizados

Este directorio contiene agentes personalizados para GitHub Copilot en este workspace.

## 📋 Agentes disponibles

### `/code-quality` — Validación de Calidad de Código .NET

Agente especializado en análisis de calidad de código para proyectos .NET (Blazor/ASP.NET Core). Combina tres dimensiones en un único reporte Markdown:

| Dimensión | Herramientas |
|-----------|--------------|
| 🧪 **Cobertura de código** | `dotnet test` + `dotnet-coverage` (OpenCover) + `reportgenerator` |
| 🐛 **Incidencias de código** | `dotnet build` (Roslyn analyzers) + `dotnet list package --vulnerable` |
| 📑 **Duplicación de código** | `jscpd` (multi-lenguaje, soporta C#) |

**Salida:** [`code-quality-report.md`](../../code-quality-report.md) en la raíz del proyecto, más artefactos en [`quality-reports/`](../../quality-reports/).

#### Uso

En el chat de GitHub Copilot:

```
/code-quality
/code-quality --scope ContosoDashboard --threshold 3
/code-quality --no-tests
```

#### Archivos relacionados

- [`.github/agents/code-quality.agent.md`](code-quality.agent.md) — definición del agente
- [`.github/scripts/code-quality-report.ps1`](../scripts/code-quality-report.ps1) — orquestador PowerShell
- [`.github/scripts/install-quality-tools.ps1`](../scripts/install-quality-tools.ps1) — instalador de herramientas
- [`coverage.runsettings`](../../coverage.runsettings) — config de cobertura XPlat
- [`.config/dotnet-tools.json`](../../.config/dotnet-tools.json) — manifest de tools locales
- [`.jscpd.json`](../../.jscpd.json) — config de jscpd
- [`.agents/skills/crap-analysis/SKILL.md`](../../.agents/skills/crap-analysis/SKILL.md) — skill CRAP de aaronontheweb (1K⭐)

#### Instalación rápida

```powershell
# Una sola vez
.\.github\scripts\install-quality-tools.ps1

# Cada vez que se quiera analizar
.\.github\scripts\code-quality-report.ps1
```

#### Personalizar

| Parámetro | Default | Descripción |
|-----------|---------|-------------|
| `-Scope` | `ContosoDashboard` | Proyecto o solución a analizar |
| `-Output` | `code-quality-report.md` | Ruta del reporte |
| `-SkipTests` | `false` | Omitir cobertura |
| `-SkipDuplication` | `false` | Omitir duplicación |
| `-CoverageThreshold` | `80` | % para marcar cobertura verde |
| `-DuplicationThreshold` | `5` | % para marcar duplicación amarilla |
