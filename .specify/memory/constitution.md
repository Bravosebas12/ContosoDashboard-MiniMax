<!--
SYNC IMPACT REPORT
==================
Version change:    1.0.0 -> 1.1.0
Version bump:      MINOR (material expansion of Principle V: TDD Hard + complete test pyramid)
Modified principles:
  - V. Test-First (NON-NEGOTIABLE) -> V. TDD Hard + Pirámide de Pruebas Completa
      * Añadidas: pruebas de mutación (Stryker.NET)
      * Añadidas: pruebas de componentes (bUnit)
      * Añadidas: pruebas de contrato (PactNet)
      * Añadidas: pruebas E2E separadas (API y UI)
      * Añadidas: pruebas de rendimiento con pirámide aplicada a todos los niveles
      * Refinadas: unitarias divididas en funcionales y técnicas
      * Sub-requisito: ciclo ROJO -> VERDE -> REFACTOR es bloqueante
Added sections:
  - Quality gates ampliados en "Flujo de Desarrollo" con 8 tipos de test
Removed sections:
  - None
Templates requiring updates:
  - .specify/templates/plan-template.md      ⚠ review "Testing Strategy" section if present
  - .specify/templates/tasks-template.md     ✅ unchanged (no specific test type required)
  - .specify/templates/spec-template.md      ✅ unchanged
  - .specify/templates/checklist-template.md ⚠ review if pre-release checklist enumerates tests
  - .specify/templates/agent-file-template.md✅ unchanged
Deferred items:
  - None. All requirements in this version resolvable with existing .NET tooling.
Skills consulted:
  - sigat-security-owasp  (carried over from v1.0.0)
  - crap-analysis          (carried over from v1.0.0; now also anchors mutation/coverage gates)
  - sqlserver-dba          (carried over from v1.0.0)
  - unit-testing-vitest    (test pyramid structure inspiration; principles transferred to .NET)
  - find-skills            (CLI for skill discovery, not for content)
================================================================
-->

# ContosoDashboard Constitution

> Documento de gobernanza técnica del proyecto **ContosoDashboard** (ASP.NET Core 8.0 + Blazor Server).
> Esta constitución es **la fuente única de verdad** para estándares, seguridad, rendimiento y calidad de código, y **prevalece** sobre cualquier otra práctica, guía o convención local.

---

## Principios Fundamentales (Core Principles)

### I. Estándares Tecnológicos (Technological Standards)

Todo el código, infraestructura y dependencias del proyecto **DEBEN** alinearse con el siguiente stack canónico. Cualquier desviación requiere aprobación explícita documentada en `specs/[###-feature]/plan.md` (sección "Architecture") y en este archivo mediante una Pull Request de tipo `chore(constitution)`.

**Runtime y frameworks**

- **Lenguaje**: C# 12 sobre .NET 8.0 (LTS) — `<TargetFramework>net8.0</TargetFramework>`, `<Nullable>enable</Nullable>`, `<ImplicitUsings>enable</ImplicitUsings>`.
- **Framework web**: ASP.NET Core 8.0 con **Blazor Server** (Razor Components interactivos) — NO migrar a Blazor WebAssembly ni a MVC clásico sin aprobación.
- **ORM y acceso a datos**: Entity Framework Core 8.0 (`Microsoft.EntityFrameworkCore.SqlServer`, `Microsoft.EntityFrameworkCore.Tools`).
- **Base de datos**: SQL Server LocalDB en desarrollo; cualquier otra edición requiere plan de migración.
- **UI / estilos**: Bootstrap 5.3 + Bootstrap Icons vía CDN (`cdn.jsdelivr.net`).
- **Autenticación**: cookie-based con `Microsoft.AspNetCore.Authentication.Cookies` + `Microsoft.Identity.Web` 2.15.0 (preparado para Azure AD/Entra ID).

**Gestión de dependencias y herramientas**

- **Versiones pineadas**: todas las dependencias en `.csproj` y `.config/dotnet-tools.json` DEBEN tener versión explícita; **PROHIBIDO** usar `*` o `latest`.
- **LTS únicamente**: solo se admiten versiones LTS de .NET y Major versions soportadas de paquetes NuGet.
- **Dotnet tools locales** preferidos sobre globales cuando aplique al proyecto (registradas en `.config/dotnet-tools.json`).
- **Manifest de skills bloqueado**: `skills-lock.json` se commitea; las actualizaciones siguen revisión de PR.

**Razón**: la convergencia a un stack estable reduce superficie de ataque, simplifica el reclutamiento, y permite aplicar reglas de Roslyn analyzers, cobertura y duplicación de forma homogénea en todos los módulos.

---

### II. Requisitos de Seguridad (Security Requirements) — basados en OWASP Top 10

Este proyecto implementa las mitigaciones correspondientes a **OWASP Top 10 (2021)**, adaptadas al stack Blazor Server + EF Core. Cada requisito **DEBE** estar implementado antes de fusionar a `main`.

| OWASP | Requisito NO-NEGOCIABLE | Implementación de referencia |
|-------|--------------------------|------------------------------|
| **A01 — Broken Access Control** | Toda página Razor DEBE usar `[Authorize]` o directiva de política; todo servicio DEBE revalidar ownership antes de devolver datos (defense in depth). | `[Authorize(Policy = "Employee")]` en páginas + `await _userService.GetByIdAsync(id, currentUserId)` en servicios. |
| **A02 — Cryptographic Failures** | HSTS habilitado; cookies `HttpOnly` + `SameSite=Lax`; nunca persistir secretos en `appsettings.json` (usar user-secrets o variables de entorno). | `app.UseHsts()` + `AddCookie(o => o.Cookie.HttpOnly = true)` + `IOptions<T>` para secretos. |
| **A03 — Injection** | Todas las consultas DEBEN usar LINQ de EF Core (parametrizadas por defecto). **PROHIBIDO** `FromSqlRaw` o `ExecuteSqlRaw` con strings que contengan entrada del usuario. | `_db.Tasks.Where(t => t.UserId == userId).ToListAsync()` |
| **A04 — Insecure Design** | Las máquinas de estado (Task Status, Project Status) DEBEN validarse server-side; el cliente NO puede mutar el estado directamente vía API. | Enum + método de extensión `CanTransitionTo(...)` en el dominio. |
| **A05 — Security Misconfiguration** | Cabeceras de seguridad obligatorias: `X-Content-Type-Options: nosniff`, `X-Frame-Options: DENY`, `X-XSS-Protection: 1; mode=block`, `Referrer-Policy: strict-origin-when-cross-origin`, CSP estricta (sin `'unsafe-inline'` salvo para Blazor). | Middleware `app.Use(...)` configurado en `Program.cs`. |
| **A06 — Vulnerable Components** | El agente `/code-quality` DEBE ejecutarse en CI y fallar el build si detecta vulnerabilidades NuGet de severidad **High** o **Critical**. | `dotnet list package --vulnerable --include-transitive`. |
| **A07 — Authentication Failures** | Auth cookie con `ExpireTimeSpan` ≤ 8h, `SlidingExpiration = true`; mensajes de error genéricos que NO filtren existencia de cuentas. | `AddCookie(o => { o.ExpireTimeSpan = TimeSpan.FromHours(8); })`. |
| **A08 — Data Integrity** | Toda entrada de usuario en Blazor DEBE validarse con DataAnnotations o equivalente server-side; NO confiar en validación client-side. | `[Required]`, `[StringLength]`, `[Range]` en DTOs. |
| **A09 — Logging Failures** | Eventos de seguridad (login fallido, 403, IDOR bloqueado) DEBEN loguearse con `ILogger<T>` y nivel `Warning`/`Error`; **PROHIBIDO** loguear contraseñas o claims sensibles. | `ILogger.LogWarning("Forbidden access to {Resource} for user {UserId}", ...)`. |
| **A10 — SSRF** | Ningún endpoint DEBE aceptar una URL y hacer `HttpClient.GetAsync` en su nombre; si se requiere integración externa, usar allowlist explícito. | N/A en este proyecto; documentado para futuro. |

**Verificación automática**: el agente `/code-quality` (definido en `.github/agents/code-quality.agent.md`) audita A06 y A09 automáticamente. El resto se valida por code review contra esta tabla.

**Skill de referencia**: `sigat-security-owasp` (v1.0.0, OWASP Top 10).

---

### III. Rendimiento y Escalabilidad (Performance and Scalability)

Toda decisión de diseño **DEBE** considerar el impacto en latencia, memoria y concurrencia. Blazor Server mantiene **stateful circuits** — un mal uso de Entity Framework degrada la experiencia de todos los usuarios conectados.

**Entity Framework Core**

- Usar `AsNoTracking()` en consultas de solo lectura (listas, dashboards, endpoints `GET` que no mutan).
- Evitar **N+1 queries**: usar `.Include(...)` o `.ProjectTo<T>()` con perfiles de Mapster/Manual explícitos.
- Paginar SIEMPRE las listas de más de 50 elementos (`Skip(n).Take(pageSize)` o `PagedResult<T>`).
- Preferir `IAsyncEnumerable<T>` sobre materialización síncrona en pipelines largos.

**Async / Await**

- Toda operación I/O-bound DEBE usar `async`/`await` hasta el borde del handler; **PROHIBIDO** `.Result` o `.Wait()`.
- `CancellationToken` propagado en todas las firmas de métodos públicos; UI lo pasa desde el `CancellationToken` de Blazor.

**Blazor Server**

- `RenderMode` por defecto es `Server`; usar `ServerPrerendered` solo cuando mejore la primera carga percibida.
- Minimizar el tamaño de los componentes: lógica pesada en servicios, no en `.razor`.
- Usar `StateHasChanged()` explícito solo cuando se modifica estado fuera de un `@bind`; evitar `@onclick` síncronos.

**Caché**

- `IMemoryCache` para datos read-heavy con TTL explícito (no más de 5 min por defecto).
- `IDistributedCache` cuando se escale horizontalmente (futuro).
- Invalidación por evento (ej. SignalR) cuando los datos cambian.

**Base de datos**

- Índices en TODAS las foreign keys y columnas usadas en `WHERE`/`ORDER BY` frecuentes.
- `EXPLAIN`/`SHOWPLAN` revisado antes de fusionar queries que tocan > 10k filas.
- Connection pool de EF Core con valores por defecto del provider; ajustar `Max Pool Size` solo tras benchmarking.

**Skill de referencia**: `sqlserver-dba` para patrones de indexación, query tuning y mantenimiento.

---

### IV. Estándares y Directrices de Código (Code Standards and Guidelines)

El código **DEBE** ser legible, testeable y mantenible. Las siguientes reglas son **no negociables** y se verifican automáticamente con el agente `/code-quality` antes de fusionar.

**C# moderno y .NET 8**

- Preferir `record`/`record struct` para DTOs inmutables y `sealed class` para clases con identidad.
- **PROHIBIDO** AutoMapper: usar **Mapster** o métodos de extensión explícitos.
- **Nullable reference types** siempre activos; tratar warnings como errores en código de producción.
- `async`/`await` con `ConfigureAwait(false)` solo en librerías; en ASP.NET es innecesario y confuso.
- Inyección de dependencias por constructor; **PROHIBIDO** `IServiceProvider` directo (service locator anti-pattern).

**Calidad de código — umbrales verificados por `/code-quality`**

| Métrica | Umbral verde | Umbral amarillo | Acción si rojo |
|---------|--------------|------------------|-----------------|
| Cobertura de líneas | >= 80% | >= 40% | Bloqueante |
| Cobertura de branches | >= 75% | >= 35% | Bloqueante |
| **CRAP score por método** (complejidad × (1 − cobertura)²) | <= 5 | <= 30 | **> 30 requiere tests O refactor** |
| Errores de compilación (CS####) | 0 | <= 5 | Bloqueante |
| Warnings de compilador (CS####) | <= 20 | <= 50 | Bloqueante |
| Vulnerabilidades NuGet (NU####) | 0 | <= 2 | Bloqueante |
| **Duplicación de código (jscpd)** | < 3% | < 5% | >= 5% requiere refactor |
| Métodos con complejidad ciclomática > 15 | 0 | — | **NO-NEGOCIABLE** refactorizar |

**Análisis estático**

- Roslyn analyzers DEBEN estar habilitados en cada `.csproj` (`<EnableNETAnalyzers>true</EnableNETAnalyzers>`).
- Reglas mínimas: `CA1050`, `CA1062`, `CA1822`, `CA2007`, `CA2016` (async), `CA2234` (URI).
- `TreatWarningsAsErrors=true` en proyectos de dominio; en proyectos de tests se permite warnings.

**Skill de referencia**: `crap-analysis` (aaronontheweb/dotnet-skills, 305 installs, 1K⭐) para Risk Hotspots y CRAP score.

---

### V. TDD Hard + Pirámide de Pruebas Completa (NON-NEGOTIABLE)

Toda nueva funcionalidad de dominio **DEBE** seguir el ciclo TDD **estricto** antes de fusionar a `main`. Este principio es **NO NEGOCIABLE**: ningún PR con código de producción sin tests correspondientes es aceptado. La pirámide se aplica **también a las pruebas de rendimiento** a todos los niveles.

#### V.1 — Ciclo TDD Hard (bloqueante)

Para cada historia de usuario, historia técnica o corrección de bug, el flujo **DEBE** ser:

1. **ROJO**: escribir PRIMERO los tests que expresan el comportamiento esperado; el PR **DEBE** abrirse con tests que fallen demostrablemente.
2. **VERDE**: implementar el código de producción mínimo que haga pasar los tests. Sin tests, no hay código de producción.
3. **REFACTOR**: mejorar el diseño sin romper tests, manteniendo la cobertura y la calidad.
4. **REVISIÓN**: code review verifica que el ciclo se siguió (diff de tests precede a diff de producción).

**Excepción documentada**: hot-fixes de seguridad críticos (CVE activo) pueden saltarse el ROJO, **DEBEN** incluir tests de regresión en el mismo PR y dejar ticket de seguimiento.

#### V.2 — Pirámide de Pruebas Completa (8 niveles)

Cada nivel tiene **propósito, herramienta, scope y umbral** definidos. Ningún nivel es opcional.

| # | Nivel | Propósito | Herramienta .NET | Scope | Umbral mínimo |
|---|-------|-----------|------------------|-------|----------------|
| 1 | **Unit funcionales** | Comportamiento observable del dominio (casos de uso, reglas de negocio) | **xUnit** + **NSubstitute** o **Moq** | Una clase/método aislado, sin I/O real | >= 80% cobertura de líneas en `Services/` y `Domain/` |
| 2 | **Unit técnicas** | Edge cases, casos de error, boundaries, mutaciones internas | **xUnit** + **FluentAssertions** | Helpers, extensiones, validadores, mappers | >= 70% en `Utils/`, `Extensions/`, `Mappers/` |
| 3 | **Componentes** | Renderizado e interacción de componentes Blazor aislados | **bUnit** | Un componente `.razor` con sus dependencias inyectadas | >= 60% de los componentes con interacción |
| 4 | **Integración** | Contratos entre módulos reales (DB, HTTP, servicios externos) | `WebApplicationFactory<Program>` + **Testcontainers** (SQL Server) | Repositorios EF, endpoints, mensajes | >= 50% cobertura de ramas en `Data/` y endpoints HTTP |
| 5 | **Contrato** | Compatibilidad API consumidor↔productor (consumer-driven) | **PactNet** + Pact Broker | Endpoints públicos y DTOs compartidos | 100% de los endpoints públicos con pacto verificado |
| 6 | **E2E API** | Flujos completos a través de la API HTTP | **RestSharp** / `HttpClient` con `WebApplicationFactory` | Flujos críticos de negocio (login, CRUD) | >= 100% de los happy paths |
| 7 | **E2E UI** | Flujos completos a través del navegador | **Microsoft Playwright** (con bUnit para assertions Blazor) | User journeys críticos end-to-end | Smoke + 1 happy path por feature principal |
| 8 | **Rendimiento** | Latencia, throughput, estabilidad bajo carga | **NBomber** / **k6** / **BenchmarkDotNet** (ver V.4) | Ver pirámide de rendimiento V.4 | Ver umbrales V.4 |

**Cobertura de código** (verificable por el agente `/code-quality`):

- Líneas: **>= 80%** (target), **>= 40%** (mínimo absoluto).
- Branches: **>= 75%** (target), **>= 35%** (mínimo absoluto).
- Métodos: 100% de los métodos públicos **DEBEN** estar cubiertos (mínimo 1 test por método).
- `<ExcludeFromCodeCoverageAttribute>` **PROHIBIDO** salvo justificación aprobada en PR.

#### V.3 — Pruebas de Mutación (Mutation Testing)

La cobertura de líneas/branches **NO** es suficiente: un test que ejecuta código sin verificar su comportamiento tiene 100% de cobertura y 0% de valor. Las **pruebas de mutación** validan la calidad de los tests.

- **Herramienta**: **Stryker.NET** (mutaciones a nivel de statements, branches, y strings).
- **Alcance**: todo el código de `Domain/`, `Services/`, validadores, y parsers.
- **Mutation score mínimo**: **>= 70%** (target **>= 80%**).
- **Frecuencia**: en CI, en cada PR; en local, antes de pedir review.
- **Mutantes sobrevivientes**: deben justificarse explícitamente (equivalente a `Stryker.NET` ignore) **O** eliminarse (test que sí detecte la mutación).
- **Stubs y AutoMapper excluidos** del scope de mutación (mutaciones equivalentes sin valor).

```bash
# Comando local
dotnet stryker --project "src/ContosoDashboard.Services" --threshold-break 70 --threshold-high 80 --threshold-low 60
```

#### V.4 — Pirámide de Pruebas de Rendimiento (aplicada a todos los niveles)

El rendimiento **NO** se valida solo en producción ni solo con un único test de carga global. Se aplica la **misma pirámide** que a las pruebas funcionales, en cada nivel.

| Nivel de pirámide de rendimiento | Tipo de test | Herramienta | Métrica objetivo |
|----------------------------------|--------------|-------------|-------------------|
| **Micro / nano** (nivel unit) | Benchmark de un método aislado, comparativo entre implementaciones | **BenchmarkDotNet** | ns/op, allocs, GC pressure |
| **Componente** | Latencia de un endpoint individual en aislamiento (sin DB compartida, sin concurrencia) | **NBomber** scenarios simples, **k6** simple | p50, p95, p99 por endpoint |
| **Integración** | Throughput de servicio-a-servicio con dependencias reales (DB, cache, downstream HTTP) | **NBomber** + **Testcontainers** (SQL Server) | RPS sostenible, error rate < 0.1% |
| **Contrato** | Latencia y SLO por contrato API individual (cada endpoint público) | **NBomber** contract suite, OpenAPI-driven | p95 < SLO contractual documentado |
| **E2E sistema** | Carga realista simulando journeys completos de usuarios | **k6** / **JMeter** | Concurrencia objetivo (N usuarios), throughput, TTI p95 |
| **Resiliencia** | Stress (romper límites), spike (picos súbitos), soak (carga sostenida 24h+) | **k6** stress profile, **NBomber** soak | Sin degradación > 10% en p95 después de 1h; sin memory leaks |

**Perfiles de carga requeridos** (en nivel E2E sistema):

- **Smoke**: 1 usuario virtual, 1 minuto — verifica que el escenario corre sin errores.
- **Load**: N usuarios (target de diseño), 10 minutos — capacidad nominal.
- **Stress**: 2×N usuarios, 5 minutos — punto de quiebre.
- **Spike**: 0 → 5×N usuarios en 30s, mantener 2 min — elasticidad.
- **Soak**: N usuarios, 24h+ — estabilidad y leaks.

**Umbrales de aceptación** (gate de merge y release):

- p95 de endpoints críticos: **< 500 ms** (web), **< 200 ms** (API internas).
- Error rate bajo carga nominal: **< 0.1%**.
- Memory growth en soak 24h: **< 10%** sobre baseline.
- CPU saturación bajo load: **< 75%** promedio, **< 90%** p95.

**Cuándo aplicar**:

- **Micro (BenchmarkDotNet)**: en cada PR que toque un método en `hot path` identificado.
- **Componente e Integración**: en cada PR que modifique un endpoint o consulta a DB.
- **Contrato y E2E sistema**: en cada release candidate (no en cada PR por costo).
- **Resiliencia (soak/stress/spike)**: mensualmente o antes de release mayor.

#### V.5 — Cobertura de código y mutación — gates consolidados

| Gate | Verde | Amarillo | Rojo (bloqueante) |
|------|:-----:|:--------:|:------------------:|
| Cobertura de líneas | >= 80% | >= 40% | < 40% |
| Cobertura de branches | >= 75% | >= 35% | < 35% |
| Métodos públicos cubiertos | 100% | >= 90% | < 90% |
| **Mutation score (Stryker.NET)** | >= 80% | >= 70% | < 70% |
| E2E API happy paths | 100% | >= 90% | < 90% |
| E2E UI smoke | 100% | >= 80% | < 80% |
| Contratos Pact verificados | 100% | >= 90% | < 90% |
| p95 latencia (carga nominal) | < SLO | < 1.2× SLO | >= 1.2× SLO |
| Error rate bajo load | < 0.1% | < 0.5% | >= 0.5% |

**Skills de referencia**: `crap-analysis` (cobertura + CRAP), `unit-testing-vitest` (estructura de pirámide transferida a .NET).

---

## Restricciones Adicionales (Additional Constraints)

- **Propósito de entrenamiento**: este proyecto **NO DEBE** desplegarse en producción. La autenticación es mock (selección de usuario desde dropdown, sin contraseña) y la separación de entornos no está endurecida.
- **Localización**: la interfaz de usuario (textos visibles) DEBE estar en español; los identificadores técnicos (clases, métodos, variables) en inglés.
- **Privacidad**: este proyecto contiene datos simulados; no incluir PII real ni credenciales reales.
- **Documentación**:
  - `README.md` DEBE actualizarse cuando se agreguen features o cambien flujos.
  - `StakeholderDocs/` contiene los requisitos de negocio; mantener sincronizado.
  - El reporte `code-quality-report.md` se regenera en cada análisis (artefacto, no fuente).
- **Compatibilidad**: el proyecto **NO** soporta Internet Explorer ni navegadores sin `ES2020`.

---

## Flujo de Desarrollo (Development Workflow)

**Branching y commits**

- Branch principal: `main` (protegida).
- Features: `NNN-feature-name` con numeración secuencial (gestionada por `speckit.git.feature`).
- Commits en español o inglés técnico; el agente `speckit.git.commit` aplica formato y (opcional) auto-commit por hook.

**Quality gates antes de fusionar (alineados con Principio V)**

1. **Build**: `dotnet build` sin errores.
2. **Tests unitarios (funcionales + técnicas)**: `dotnet test` con todos los tests pasando en `*.Tests.Unit`.
3. **Tests de componentes Blazor**: `dotnet test` con `bUnit` en `*.Tests.Components`.
4. **Tests de integración**: `dotnet test` con `WebApplicationFactory` + Testcontainers en `*.Tests.Integration`.
5. **Tests de contrato (PactNet)**: `pact-broker can-i-deploy` debe devolver **success** contra el Pact Broker del entorno.
6. **Tests E2E API**: suite `*.Tests.E2E.Api` pasando.
7. **Tests E2E UI (Playwright)**: smoke + happy paths pasando.
8. **Tests de mutación (Stryker.NET)**: mutation score **>= 70%** (gate bloqueante) en módulos de dominio y servicios.
9. **Cobertura**: líneas >= 40% (mínimo), branches >= 35%, métodos públicos 100%.
10. **Estático**: 0 errores Roslyn, <= 20 warnings, 0 vulnerabilidades High/Critical NuGet.
11. **Duplicación**: < 5% según `jscpd`.
12. **Rendimiento (cuando aplique)**: p95 < SLO contractual y error rate < 0.1% en la suite `*.Tests.Performance.Component` o superior.
13. **Code review**: al menos 1 aprobación humana usando esta constitución como checklist.

Las gates 9–11 las ejecuta automáticamente el agente `/code-quality` (`.github/agents/code-quality.agent.md`). El reporte se publica en `code-quality-report.md` y se adjunta al PR.

**CI / CD**

- Pipeline por PR ejecuta: `dotnet restore` → `dotnet build` → unit + componentes + integración + E2E API + E2E UI + mutación → `/code-quality` → build de artefactos.
- Pipeline de release candidate agrega: contratos Pact (verificación con `pact-broker verify`) → performance (k6/NBomber) → build de artefactos.
- Si cualquier gate falla, el PR NO se puede fusionar.
- Hot-fixes de seguridad documentan excepción (ver V.1) y DEBEN añadir tests de regresión en el mismo PR.

---

## Gobernanza (Governance)

- **Precedencia**: esta constitución **prevalece** sobre cualquier guía, README local, convención de equipo o decisión ad-hoc.
- **Enmiendas**: requieren Pull Request de tipo `chore(constitution)` con:
  1. Justificación documentada.
  2. Análisis de impacto en features existentes.
  3. Plan de migración si el cambio rompe compatibilidad.
  4. Aprobación de al menos 1 revisor con conocimiento del stack.
- **Versionado semántico**:
  - **MAJOR** (X.0.0): eliminación o redefinición incompatible de un principio.
  - **MINOR** (1.X.0): adición de un principio, sección o requisito material.
  - **PATCH** (1.0.X): clarificación, corrección tipográfica, refinamiento no-semántico.
- **Revisión de cumplimiento**:
  - **Trimestral**: el equipo audita el repositorio contra esta constitución.
  - **Continuo**: el agente `/code-quality` audita gates automatizables en cada PR.
  - **Anual**: revisión completa de la constitución para reflejar evolución del stack.
- **Skills vinculantes**: `crap-analysis` (cobertura + CRAP), `sigat-security-owasp` (seguridad), `sqlserver-dba` (DB performance), `find-skills` (descubrimiento de nuevas skills aplicables).
- **Excepciones**: cualquier desviación se documenta en `specs/[###-feature]/plan.md` (sección "Complexity Tracking" o "Architecture") y se aprueba vía PR.

---

**Version**: 1.1.0 | **Ratified**: 2026-06-12 | **Last Amended**: 2026-06-12
