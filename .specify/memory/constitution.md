<!--
SYNC IMPACT REPORT
==================
Version change:    (none) -> 1.0.0
Version bump:      MAJOR (initial ratification - defines principles for the first time)
Modified principles:
  - [PRINCIPLE_1_NAME] -> I. Estándares Tecnológicos (Technological Standards)
  - [PRINCIPLE_2_NAME] -> II. Requisitos de Seguridad (Security Requirements)
  - [PRINCIPLE_3_NAME] -> III. Rendimiento y Escalabilidad (Performance and Scalability)
  - [PRINCIPLE_4_NAME] -> IV. Estándares y Directrices de Código (Code Standards and Guidelines)
  - [PRINCIPLE_5_NAME] -> V. Test-First (NON-NEGOTIABLE)  [derived, supports Principle IV]
Added sections:
  - "Restricciones Adicionales" (Additional Constraints)
  - "Flujo de Desarrollo" (Development Workflow)
  - "Gobernanza" (Governance) - populated with concrete rules
Removed sections:
  - All [SECTION_2_NAME], [SECTION_3_NAME] placeholders (replaced with concrete content)
Templates requiring updates:
  - .specify/templates/constitution-template.md     ✅ unchanged (this file is regenerated from it)
  - .specify/templates/plan-template.md             ✅ unchanged (uses [Gates determined based on constitution file] - dynamic)
  - .specify/templates/spec-template.md             ✅ unchanged (no direct constitution refs)
  - .specify/templates/tasks-template.md            ✅ unchanged (no direct constitution refs)
  - .specify/templates/checklist-template.md        ✅ unchanged (no direct constitution refs)
  - .specify/templates/agent-file-template.md       ✅ unchanged
Deferred items:
  - None. All placeholders resolved.
Skills consulted:
  - sigat-security-owasp  (OWASP Top 10 -> Security Requirements section)
  - crap-analysis          (CRAP score thresholds, coverage gates -> Code Standards section)
  - sqlserver-dba          (DB performance and indexing -> Performance section)
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

### V. Test-First (NON-NEGOTIABLE)

Para toda funcionalidad de dominio (no UI), el ciclo **ROJO → VERDE → REFACTOR** es obligatorio:

1. **Escribir** el test que cubre el comportamiento deseado.
2. **Verificar** que el test falla (rojo).
3. **Implementar** el código mínimo para que pase (verde).
4. **Refactorizar** sin romper tests.

**Pirámide de tests**

- **Unitarios** (>= 80% de cobertura de líneas): servicios de dominio, helpers, extensiones.
- **Integración** (>= 50%): endpoints HTTP, repositorios EF Core con `InMemory` provider o Testcontainers.
- **E2E / UI** (smoke): flujos críticos con Playwright o bUnit para componentes Blazor.

**Cobertura**: ver tabla en Principio IV. Cobertura < 40% en líneas **bloquea** el merge.

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

**Quality gates antes de fusionar**

1. **Build**: `dotnet build` sin errores.
2. **Tests**: `dotnet test` con todos los tests pasando.
3. **Cobertura**: >= 40% mínimo (>= 80% target).
4. **Estático**: 0 errores Roslyn, <= 20 warnings, 0 vulnerabilidades High/Critical NuGet.
5. **Duplicación**: < 5% según `jscpd`.
6. **Code review**: al menos 1 aprobación humana usando esta constitución como checklist.

Las gates 3–5 las ejecuta automáticamente el agente `/code-quality` (`.github/agents/code-quality.agent.md`). El reporte se publica en `code-quality-report.md` y se adjunta al PR.

**CI / CD**

- Pipeline ejecuta: `dotnet restore` → `dotnet build` → `dotnet test` → `/code-quality` → build de artefactos.
- Si cualquier gate falla, el PR NO se puede fusionar.

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

**Version**: 1.0.0 | **Ratified**: 2026-06-12 | **Last Amended**: 2026-06-12
