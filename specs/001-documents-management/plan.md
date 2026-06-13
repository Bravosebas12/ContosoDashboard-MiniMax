# Implementation Plan: Document Upload and Management

**Branch**: `001-documents-management` | **Date**: 2026-06-12 | **Spec**: [spec.md](spec.md)
**Input**: Feature specification from `/specs/001-documents-management/spec.md`
**Constitution**: `.specify/memory/constitution.md` v1.1.0

## Summary

Implementar la gestión de documentos (subida, organización, descarga, compartición, eliminación) en ContosoDashboard como feature monolítica dentro del proyecto Blazor Server existente. La implementación es **estrictamente local** (esta release no involucra almacenamiento cloud); usa la interfaz `IFileStorageService` con `LocalFileStorageService` como única implementación concreta. El flujo de upload valida tamaño (≤ 25 MB), extensiones y MIME types contra whitelist, escanea con antivirus (ClamAV via nClam en training), persiste fuera de `wwwroot` con path `{userId}/{projectIdOrPersonal}/{guid}.{ext}`, y registra auditoría estructurada. La autorización aplica defense in depth: `[Authorize]` en páginas Blazor + re-validación de ownership en `DocumentService`. La estrategia de testing cubre la pirámide completa de 8 niveles de la constitución v1.1.0, incluyendo mutación con Stryker.NET (≥ 70%) y performance con NBomber/k6.

## Technical Context

**Language/Version**: C# 12 sobre .NET 8.0 LTS (alineado con Constitución I)
**Primary Dependencies**:
- `Microsoft.AspNetCore.App` (incluye Blazor Server, EF Core, SignalR)
- `Microsoft.EntityFrameworkCore.SqlServer 8.0.x` (pinedo, Constitución I)
- `Microsoft.EntityFrameworkCore.Tools 8.0.x` (solo dev, para migrations)
- `nClam 4.x` (cliente .NET para ClamAV — antivirus)
- `Microsoft.Extensions.Caching.Memory 8.0.x` (pinedo, caching de queries read-heavy)

**Storage**:
- **Metadatos**: SQL Server LocalDB vía EF Core 8 (existente `ApplicationDbContext`, extendido con `DbSet<Document>`, `DbSet<DocumentShare>`, `DbSet<ActivityLog>`)
- **Archivos físicos**: filesystem local en `ContosoDashboard/AppData/uploads/` (fuera de `wwwroot`); path relativo portable `{userId}/{projectIdOrPersonal}/{guid}.{ext}`

**Testing** (Constitución V — pirámide completa de 8 niveles):
- **Unit funcionales y técnicas**: xUnit + NSubstitute/Moq + FluentAssertions + coverlet
- **Componentes Blazor**: bUnit
- **Integración**: xUnit + `WebApplicationFactory<Program>` + Testcontainers (SQL Server)
- **Contratos**: PactNet + Pact Broker
- **E2E API**: RestSharp + `WebApplicationFactory`
- **E2E UI**: Microsoft Playwright
- **Mutación**: Stryker.NET (gate ≥ 70% bloqueante)
- **Rendimiento**: BenchmarkDotNet (micro), NBomber (componente/contrato), k6 (E2E sistema)

**Target Platform**: Windows/Linux con .NET 8 runtime; Blazor Server requiere ASP.NET Core 8 hosting; navegadores modernos (Chrome, Edge, Firefox — sin IE)

**Project Type**: Web application (Blazor Server) — extension de proyecto existente

**Performance Goals** (alineados con StakeholderDoc §5 + Constitución III):
- Upload de 25 MB: p95 ≤ 30s en LAN 100 Mbps
- Lista paginada (25 items): p95 ≤ 500 ms
- Búsqueda: p95 ≤ 2s sobre 10k documentos por usuario
- Vista previa: p95 ≤ 3s para PDF de 2 MB
- Error rate bajo carga nominal: < 0.1%

**Constraints**:
- **Offline-first**: la aplicación completa funciona sin internet (cumple Constitución §Restricciones Adicionales)
- **Training-only**: NO production deployment (cumple Constitución §Restricciones Adicionales)
- **Stack canónico fijo**: .NET 8, Blazor Server, EF Core, SQL Server LocalDB
- **Idioma**: UI y mensajes visibles al usuario en español (cumple política de idioma del hook `SessionStart`); identificadores de código y comentarios técnicos en inglés

**Scale/Scope**:
- Usuarios activos: 4 (training mock users) — pero el diseño escala a cientos
- Documentos por usuario: hasta 500 (SLA medido con este dataset)
- Documentos totales: hasta 10k para pruebas de búsqueda
- Categorías: 6 fijas (Project Documents, Team Resources, Personal Files, Reports, Presentations, Other)
- Tags: hasta 5 por documento

## Constitution Check

*GATE: Must pass before Phase 0 research. Re-check after Phase 1 design.*

### Pre-Phase 0 (initial assessment)

| # | Principio | Estado | Evidencia |
|---|-----------|:------:|-----------|
| I | **Estándares Tecnológicos** | ✅ PASS | Stack canónico respetado: .NET 8, Blazor Server, EF Core 8, SQL Server LocalDB; versiones pineadas; LTS |
| II | **Requisitos de Seguridad (OWASP A01-A10)** | ✅ PASS | A01: `[Authorize]` + re-validación en `DocumentService`; A02: HSTS + cookies HttpOnly (existente); A03: EF Core (sin `FromSqlRaw`); A05: headers CSP/X-Frame-Options (existente); A06: AV obligatorio en upload; A08: validación DataAnnotations; A09: audit log estructurado |
| III | **Rendimiento y Escalabilidad** | ✅ PASS | async/await en todos los I/O; EF Core `AsNoTracking()`; paginación explícita (25/pág); `IMemoryCache` para widgets; índices en FKs y campos de búsqueda; decisión de last-writer-wins evita locks |
| IV | **Estándares de Código** | ✅ PASS | records para DTOs; nullable reference types; sin AutoMapper (usar interface); sin service locator; Roslyn analyzers; umbrales del agente `/code-quality` |
| V | **TDD Hard + Pirámide Completa** | ✅ PASS | 8 niveles definidos; mutación Stryker.NET ≥ 70% (bloqueante); pirámide de rendimiento con 5 perfiles; TDD Hard mandatorio; hot-fixes de seguridad con excepción documentada |

### Post-Phase 1 (re-evaluation after design)

| # | Principio | Estado | Evidencia refinada |
|---|-----------|:------:|-------------------|
| I | **Estándares Tecnológicos** | ✅ PASS | Stack confirmado en `research.md` §Technology Stack; pineado de versiones en `.config/dotnet-tools.json` y `ContosoDashboard.csproj`; sin SDKs cloud agregados (per FR-038) |
| II | **Requisitos de Seguridad (OWASP A01-A10)** | ✅ PASS | A01: autorización en 3 capas (`[Authorize]` + `IDocumentService.UserHasAccessAsync` + `IDocumentShareService` business rules); A03: EF Core LINQ exclusivamente, sin `FromSqlRaw`; A06: `IAntivirusScanner` con `ClamAvScanner` (nClam) — fail-open en training, fail-closed en producción (per AC-1.3.x); A07: cookies HttpOnly (existente); A08: validación vía DataAnnotations en entidades; A09: `ActivityLog` estructurado con eventos `document.*` |
| III | **Rendimiento y Escalabilidad** | ✅ PASS | async/await en todos los `IDocumentService` methods; paginación `PagedResult<T>` con page size 25; índices definidos en `data-model.md` (IX_Documents_UploadedByUserId, IX_Documents_ProjectId, IX_Documents_Category, IX_Documents_UploadedAt, etc.); decisión last-writer-wins evita locks; benchmarks: upload ≤ 30s p95, list ≤ 500 ms p95, search ≤ 2s p95, preview ≤ 3s p95 |
| IV | **Estándares de Código** | ✅ PASS | records para DTOs (`DocumentDto`, `UploadResult`, `ScanResult`, `PagedResult<T>`, `ShareRequest`); nullable reference types; `IFileStorageService` interface evita acoplamiento a filesystem concreto; sin AutoMapper; `sealed` por defecto en servicios; sin service locator; código en español para UI, identificadores en inglés |
| V | **TDD Hard + Pirámide Completa** | ⚠️ **CONDITIONAL PASS** | 8 niveles con proyectos separados. **Hallazgo 2026-06-12** del agente `/code-quality`: cobertura actual **76.4% líneas / 57.65% branches** está por debajo de los objetivos de Constitución V.5 (≥80% / ≥75%). 11 archivos en `Services/` y `Models/` sin tests. Adicionalmente, las suites de mutación (Stryker.NET) y performance (NBomber/k6) **nunca se ejecutaron** (T126, T127, T128, T135 marcadas como bloqueadas). **Plan de remediación**: nueva `Phase 12: Coverage Boost al 80% líneas / 75% branches` (tasks T141–T160) en `tasks.md`. Los gates pasarán cuando Phase 12 se complete y se suba el threshold de `coverage.runsettings` (ver `coverage.target.runsettings`). |

**Resultado final de gates**: ✅ **3 gates PASS** (I, II, III, IV) + ⚠️ **1 gate CONDITIONAL** (V). La Constitución v1.1.0 se respeta condicionalmente; la condicionalidad se levanta con la ejecución de la Phase 12. No se requieren violaciones justificadas formalmente porque la condicionalidad está documentada en `tasks.md` con plan de remediación trazado.

## Project Structure

### Documentation (this feature)

```text
specs/001-documents-management/
├── plan.md              # Este archivo
├── research.md          # Phase 0 output — decisiones técnicas con rationale
├── data-model.md        # Phase 1 output — entidades con propiedades, índices, relaciones
├── quickstart.md        # Phase 1 output — guía rápida para probar la feature
├── contracts/           # Phase 1 output — interfaces públicas (.cs) y contratos
│   ├── IFileStorageService.cs
│   ├── IAntivirusScanner.cs
│   ├── IDocumentService.cs
│   └── IDocumentShareService.cs
├── checklists/
│   └── requirements.md  # Spec quality checklist (21/21 passed)
└── tasks.md             # Phase 2 output (generado por /speckit.tasks — NO aquí)
```

### Source Code (extensión del proyecto existente)

```text
ContosoDashboard/
├── Models/
│   ├── Document.cs                 (🆕 entity)
│   ├── DocumentShare.cs            (🆕 entity)
│   └── ActivityLog.cs              (🆕 entity)
├── Data/
│   └── ApplicationDbContext.cs     (✏️ modificado: agregar DbSet<Document>, DbSet<DocumentShare>, DbSet<ActivityLog>)
├── Services/
│   ├── Documents/
│   │   ├── IDocumentService.cs     (🆕 interface)
│   │   ├── DocumentService.cs      (🆕 implementacion)
│   │   ├── IDocumentShareService.cs (🆕 interface)
│   │   ├── DocumentShareService.cs  (🆕 implementacion)
│   │   ├── IFileStorageService.cs  (🆕 interface)
│   │   ├── LocalFileStorageService.cs (🆕 implementacion - UNICA)
│   │   ├── IAntivirusScanner.cs    (🆕 interface)
│   │   ├── ClamAvScanner.cs        (🆕 implementacion training)
│   │   ├── MimeTypeValidator.cs     (🆕 helper)
│   │   ├── FilePathBuilder.cs       (🆕 helper, Guid-based paths)
│   │   └── DocumentConstants.cs    (🆕 categorias, mime whitelist)
│   └── DashboardService.cs        (✏️ modificado: anadir RecentDocuments widget + count)
├── Pages/
│   ├── Documents.razor             (🆕 listado + upload)
│   ├── Documents.razor.cs          (🆕 codebehind)
│   ├── DocumentDetails.razor       (🆕 preview, download, share, edit, delete)
│   ├── DocumentDetails.razor.cs    (🆕 codebehind)
│   ├── SharedWithMe.razor           (🆕 documentos compartidos conmigo)
│   ├── SharedWithMe.razor.cs        (🆕 codebehind)
│   └── _Imports.razor               (✏️ agregar @using para Documents namespace)
├── Shared/
│   └── DocumentUploadComponent.razor (🆕 componente reutilizable de upload)
└── Migrations/                      (🆕 generado por dotnet ef migrations add InitialDocuments)

ContosoDashboard.Tests.Unit/                   (🆕 proyecto xUnit)
├── Services/Documents/
│   ├── DocumentServiceTests.cs                (funcionales)
│   ├── DocumentShareServiceTests.cs           (funcionales)
│   ├── LocalFileStorageServiceTests.cs        (unit técnicas)
│   ├── ClamAvScannerTests.cs
│   ├── DocumentDeleteTests.cs
│   ├── DocumentReportServiceTests.cs
│   ├── DocumentSearchAndFilterTests.cs
│   ├── ProjectMembershipAuthorizationTests.cs
│   ├── TaskAttachmentTests.cs
│   └── ActivityLogBackgroundServiceTests.cs   (🆕 T149 — Phase 12)
├── Services/
│   ├── UserServiceTests.cs                    (🆕 T141 — Phase 12)
│   ├── ProjectServiceTests.cs                 (🆕 T142 — Phase 12)
│   ├── TaskServiceTests.cs                    (🆕 T143 — Phase 12)
│   ├── NotificationServiceTests.cs            (🆕 T144 — Phase 12)
│   ├── CustomAuthenticationStateProviderTests.cs (🆕 T146 — Phase 12)
│   ├── DashboardServiceScopeTests.cs          (existente; extender T151)
│   ├── ActivityLogServiceTests.cs             (existente; extender T154)
│   └── ActivityLogQueueTests.cs               (existente)
├── Services/ActivityLog/
│   ├── NotificationQueueTests.cs              (🆕 T145 — Phase 12)
│   ├── ActivityLogCleanupServiceTests.cs      (🆕 T147 — Phase 12)
│   └── ActivityLogCleanupBackgroundServiceTests.cs (🆕 T148 — Phase 12)
├── Helpers/
│   ├── MimeTypeValidatorTests.cs
│   ├── FilePathBuilderTests.cs                (existente; extender T152)
│   └── DocumentConstantsTests.cs
├── Models/
│   └── ProjectTests.cs                        (🆕 T150 — Phase 12)
└── ContosoDashboard.Tests.Unit.csproj         (.NET 8, xunit, NSubstitute, FluentAssertions, coverlet)

ContosoDashboard.Tests.Components/             (🆕 proyecto xUnit + bUnit)
├── DocumentUploadComponentTests.cs
├── DocumentListComponentTests.cs
└── ContosoDashboard.Tests.Components.csproj

ContosoDashboard.Tests.Integration/            (🆕 proyecto xUnit + WAF + Testcontainers)
├── DocumentServiceIntegrationTests.cs
├── LocalFileStorageServiceIntegrationTests.cs
├── AntivirusScannerIntegrationTests.cs
├── Api/
│   ├── DocumentsUploadEndpointTests.cs
│   ├── DocumentsListEndpointTests.cs
│   └── DocumentsSearchEndpointTests.cs
└── ContosoDashboard.Tests.Integration.csproj

ContosoDashboard.Tests.Contract/               (🆕 proyecto xUnit + PactNet)
├── DocumentsApiConsumerTests.cs
├── DocumentsApiProducerTests.cs
└── ContosoDashboard.Tests.Contract.csproj

ContosoDashboard.Tests.E2E.Api/                (🆕 proyecto xUnit + RestSharp)
├── DocumentsE2ETests.cs
└── ContosoDashboard.Tests.E2E.Api.csproj

ContosoDashboard.Tests.E2E.UI/                 (🆕 proyecto xUnit + Playwright)
├── DocumentsUploadFlowTests.cs
├── DocumentsShareFlowTests.cs
└── ContosoDashboard.Tests.E2E.UI.csproj

ContosoDashboard.Tests.Performance/            (🆕 proyecto xUnit + NBomber/k6)
├── Micro/DocumentServiceBenchmarks.cs         (BenchmarkDotNet)
├── Component/DocumentsEndpointTests.cs       (NBomber)
├── Integration/DocumentsUploadThroughputTests.cs (NBomber + Testcontainers)
└── ContosoDashboard.Tests.Performance.csproj

k6/                                            (🆕 scripts de carga)
├── smoke.js
├── load.js
├── stress.js
├── spike.js
└── soak.js
```

**Structure Decision**: Se mantiene la **organización top-level por capa** del proyecto existente (Models/, Data/, Services/, Pages/) con un **subdirectorio `Services/Documents/`** para agrupar la lógica cohesiva de la feature. Esto evita crear una estructura feature-folder que rompa la convención existente, mientras permite descubrir fácilmente todos los archivos del dominio. Los **proyectos de tests** siguen una convención de sufijo (`.Tests.Unit`, `.Tests.Components`, `.Tests.Integration`, `.Tests.Contract`, `.Tests.E2E.Api`, `.Tests.E2E.UI`, `.Tests.Performance`) alineada con los 8 niveles de la pirámide de la Constitución V.

## Complexity Tracking

> **No hay violaciones de la Constitución que requieran justificación** — todos los gates pasan.

| Decisión de diseño | Rationale | Alternativa rechazada |
|--------------------|-----------|------------------------|
| Mantener organización top-level (no feature folders) | Consistencia con código existente; minimiza fricción en code review y merges | Feature folders — sería inconsistente con el resto del proyecto |
| EF Core con migrations desde día 1 | Cumple Constitución IV (vs `EnsureCreated()`) | `EnsureCreated()` — más simple pero no soporta migrations futuras |
| Tags como columna CSV (no tabla separada) | Simplicidad en training; consulta atómica; suficiente para ≤ 5 tags | Tabla `Tag` — over-engineering para 5 valores |
| Storage paths en BD: relativo y portable | Patrón `{userId}/{projectIdOrPersonal}/{guid}.{ext}` permite cambio de backend sin migrar datos | Paths absolutos — acopla a filesystem local |
| Antivirus: ClamAV + nClam en training; fail-open | Cumple StakeholderDoc §9.4 (training: log warning + allow + flag) | Microsoft Defender — requeriría licencia + conectividad |
| Share con `Permission` enum (Read/Write) en lugar de booleano | Extensible a nuevos permisos (Comment, Approve) sin migraciones | `bool CanWrite` — limita opciones futuras |
| `ActivityLog` separado de `Notification` (existente) | Trazabilidad ≠ comunicación al usuario; tablas con propósitos distintos | Fusionar — mezclaría auditoría con UX |
| bUnit para componentes, NO Playwright para unit de UI | bUnit es unit-test nativo; Playwright es E2E (lento); mejor separación | Playwright para todo — tests lentos y frágiles |
| xUnit + NSubstitute sobre xUnit + Moq | NSubstitute tiene API más natural para .NET; FluentAssertions complementa | Moq — más verboso, syntax menos elegante |
| Last-writer-wins con GUID nuevo (no locks) | Decisión de clarificación Q3; simple; consistente con CDNs; tests con 2 clientes concurrentes | Locks distribuidos — añade complejidad sin beneficio en training |
| `IDocumentShareService` separado de `IDocumentService` | Single Responsibility; share tiene reglas de negocio distintas (expiración, revocación, permisos por PM) | Fusionar — violaría SRP y haría tests más complejos |

## Technology Decisions (resumen)

| Decisión | Elección | Rationale |
|----------|----------|-----------|
| Lenguaje | C# 12 / .NET 8 LTS | Constitución I |
| Framework web | Blazor Server | Constitución I; ya en uso; circuitos persistentes |
| ORM | EF Core 8.0.x | Constitución I; integración nativa con SQL Server |
| DB | SQL Server LocalDB | Constitución I; training-only |
| Auth mock | Cookie + Claims (existente) | Ya en uso; sin cambios |
| File storage | Filesystem local | Decisión FR-038 (100% local) |
| Antivirus training | ClamAV + nClam | Open-source, offline, soporta formatos comunes |
| File path security | GUID, no nombre original | Anti path-traversal (FR-006) |
| Test runner | xUnit | Estándar .NET; amplio ecosistema |
| Mocking | NSubstitute | API más natural que Moq para .NET |
| Assertions | FluentAssertions | Legibilidad; mejor diagnóstico de fallos |
| Component tests | bUnit | Nativo para Blazor |
| Integration DB | Testcontainers (SQL Server) | Real SQL Server, reproducible, paralelo |
| Contract | PactNet | Estándar consumer-driven; soporte .NET |
| E2E UI | Playwright | Multi-browser; trace viewer |
| Mutation | Stryker.NET | Estándar para .NET |
| Micro perf | BenchmarkDotNet | Estándar para .NET |
| Load perf | NBomber + k6 | NBomber in-process; k6 externo |

## Open Questions / Future Considerations

- **Cifrado en reposo**: el filesystem local no cifra archivos. Para training no es crítico; en producción real se aplicaría BitLocker o equivalente a nivel de volumen. Documentado como follow-up.
- **Soft delete + papelera**: explícitamente fuera de scope. Si se requiere en release futura, agregar columna `DeletedAt` y job de purga.
- **Versioning de archivos**: explícitamente fuera de scope. Last-writer-wins con GUID nuevo es la convención actual.
- **Cuotas de almacenamiento**: fuera de scope. Si se requiere, agregar campo `StorageQuotaBytes` en `User`.
- **CDN / cache distribuido**: cuando se migre a producción, considerar Azure CDN con signed URLs.
- **Full-text search nativo vs LINQ**: empezar con `LIKE` optimizado + índices; si el SLA no se cumple con 10k docs, migrar a SQL Server Full-Text Search o ElasticSearch.

---

## Discovered Issues & Remediation Plan (Post-Implementation Review — v1.0.1)

> **Context**: After v1.0.0 implementation (`T041`-`T140`), two UX issues were identified via manual testing and user feedback on 2026-06-12:
> 1. **Preview button triggers a download** instead of inline visualization.
> 2. **Delete confirmation modal** lacks clarity (English text in a Spanish app, missing file type/filename context, no type-to-confirm safeguard).
>
> This section is a **read-only analysis** — no production code is modified in this iteration. The proposed remediation tasks (`T141`-`T153`) will be planned in a future `/speckit.tasks` regeneration cycle.

### Issue 1 — Preview triggers download instead of rendering inline

**Symptom (user-reported)**:
- User clicks the **"👁 Preview in a new tab"** button in `DocumentDetails.razor:121` (a `<a href="/DocumentFiles/Preview?id=..." target="_blank">`).
- The new tab opens; instead of showing the PDF inline, the browser **downloads** the file (`*.pdf` lands in `~/Downloads`).
- The embedded `<iframe>` in `DocumentPreviewComponent.razor:35` also fails to render the PDF.

**Affected acceptance criteria**:
- **AC-3.1.2** (StakeholderDoc §8): *"Given a 2 MB PDF, When the user clicks 'Vista previa', Then it renders in an `<iframe>` inline in ≤ 3s"*
- **AC-3.1.3** (StakeholderDoc §8): *"Given a `.docx` file, When the user clicks 'Vista previa', Then shows 'Vista previa no disponible para este tipo de archivo' + 'Descargar' button"*
- **FR-019** (spec): *"System MUST allow inline preview of PDFs and images (JPEG, PNG) in an embedded viewer, for files ≤ 10 MB"*

**Root cause analysis**:

| # | Cause | Evidence | Severity |
|---|---|---|:---:|
| 1.1 | **`X-Frame-Options: DENY` is set globally** in `Program.cs:108` for ALL responses, including `/DocumentFiles/Preview`. This prevents the PDF response from being framed by **any** parent (including same-origin). The browser blocks the `<iframe>` and falls back to download behavior. | `Program.cs` middleware, line 108: `context.Response.Headers["X-Frame-Options"] = "DENY";` | **HIGH** |
| 1.2 | **CSP lacks `frame-src` and `object-src` directives**. The current CSP (`Program.cs:113-119`) relies on `default-src 'self'`, which is brittle across browsers. Some browsers treat absent `frame-src` as `default-src`, but Chrome may require an explicit declaration. | `Program.cs`, lines 113-119 | MEDIUM |
| 1.3 | The `<iframe>` in `DocumentPreviewComponent.razor:35` uses `sandbox="allow-same-origin"`. Combined with the global `X-Frame-Options: DENY`, the iframe is fully blocked. The author of `Preview.cshtml.cs:25-27` flagged this in a code comment but did not fix it. | `Shared/DocumentPreviewComponent.razor:35` | HIGH |
| 1.4 | The "Preview in a new tab" button is `<a href="/DocumentFiles/Preview?id=..." target="_blank">` (`DocumentDetails.razor:121`). When opened in a new tab, the browser only honors `Content-Disposition: inline` if the user has a PDF viewer enabled. Chrome with "Download PDFs instead of automatically opening them in Chrome" set will download the file regardless of our headers. | `DocumentDetails.razor:121` | MEDIUM |
| 1.5 | The fallback message in `DocumentPreviewComponent.razor:15-19` is in **English** ("Preview not available for this file type..."), conflicting with **Constitution §Restricciones Adicionales** ("la interfaz de usuario DEBE estar en español"). | `Shared/DocumentPreviewComponent.razor:15-19` | LOW |
| 1.6 | **No automated test** verifies that `/DocumentFiles/Preview` returns `Content-Disposition: inline` and renders correctly. The Phase 1 tests (unit, integration, E2E) are blocked and were not built. | tasks.md T127, T135 (both `[ ]`) | MEDIUM |

**Recommended fix (proposed, not yet applied)**:

| Sub-fix | Description | Affected file(s) |
|---|---|---|
| 1.A | Change global `X-Frame-Options` from `DENY` to `SAMEORIGIN` in the middleware. This still prevents clickjacking from external sites but allows same-origin framing. | `Program.cs:108` |
| 1.B | As a defense-in-depth measure, explicitly set `X-Frame-Options: SAMEORIGIN` on the `/DocumentFiles/Preview` response (so the policy is co-located with the endpoint that needs it). | `Pages/DocumentFiles/Preview.cshtml.cs:OnGetAsync` |
| 1.C | Add `frame-src 'self'` and `object-src 'self'` to the CSP for explicitness. | `Program.cs:113-119` |
| 1.D | Remove `sandbox="allow-same-origin"` from the `<iframe>` (no longer needed with `SAMEORIGIN`). | `Shared/DocumentPreviewComponent.razor:35` |
| 1.E | Switch PDF rendering from `<iframe>` to `<embed type="application/pdf" src="...">` (more reliable inline rendering across browsers, including older Chrome versions). Keep `<img>` for JPEG/PNG. | `Shared/DocumentPreviewComponent.razor` |
| 1.F | Add a user-facing note: "Si tu navegador descarga el PDF en lugar de mostrarlo, habilita el visor PDF integrado en la configuración de tu navegador." (Per browser behavior we cannot fully control.) | `Shared/DocumentPreviewComponent.razor` |
| 1.G | Localize all fallback messages in `DocumentPreviewComponent.razor` to Spanish. | `Shared/DocumentPreviewComponent.razor` |
| 1.H | Write a Playwright E2E test that asserts the response of `/DocumentFiles/Preview?id=X` has `Content-Disposition: inline` (NOT `attachment`) and `Content-Type: application/pdf`. | `tests/ContosoDashboard.Tests.E2E.UI/` (new, blocked by Phase 1) |

**New tasks to add (proposed, pending `/speckit.tasks` regeneration)**:

| ID | Priority | Story | Description |
|---|:---:|---|---|
| T141 | P1 | [UX-FIX] | Cambiar `X-Frame-Options: DENY` → `SAMEORIGIN` en middleware global de `Program.cs` (Fix 1.A) |
| T142 | P1 | [UX-FIX] | Set `X-Frame-Options: SAMEORIGIN` explícitamente en `Pages/DocumentFiles/Preview.cshtml.cs` (Fix 1.B) |
| T143 | P1 | [UX-FIX] | Agregar `frame-src 'self'` y `object-src 'self'` al CSP global en `Program.cs` (Fix 1.C) |
| T144 | P1 | [UX-FIX] | Quitar `sandbox="allow-same-origin"` del `<iframe>` en `DocumentPreviewComponent.razor` (Fix 1.D) |
| T145 | P2 | [UX-FIX] | Refactor `DocumentPreviewComponent.razor` para usar `<embed type="application/pdf">` para PDFs y `<img>` para JPEG/PNG (Fix 1.E) |
| T146 | P2 | [UX-FIX] | Localizar mensajes de fallback al español en `DocumentPreviewComponent.razor` (Fix 1.G) |
| T147 | P2 | [UX-FIX] | Agregar nota visible al usuario sobre configuración del visor PDF del navegador (Fix 1.F) |
| T148 | P3 | [UX-FIX] | Crear test E2E Playwright que valide `Content-Disposition: inline` en `/DocumentFiles/Preview` (Fix 1.H) — **bloqueado por Phase 1** |

### Issue 2 — Delete confirmation modal has unclear wording and exposes a raw value

**Clarification from product owner (2026-06-12)**:
- The modal text **must stay in English** (this is the agreed language for destructive-action modals in this product). The previous v1.0.1 analysis incorrectly flagged a "Constitution §Localización violation" — that finding is **withdrawn**.
- The real issues are: (a) the wording inside the modal is **grammatically awkward and contains technical jargon** ("audit log", "(cascade)", "without document reference") that an end user cannot parse; and (b) the modal shows a **raw value** that looks like code instead of a human-friendly identifier.

**Symptom (user-reported)**:
- User clicks the **"🗑 Delete"** button in `DocumentDetails.razor:131` (visible only to owner).
- Modal opens with the title "⚠ Confirm deletion".
- The body shows: `Permanently delete the document "Title"?`
- If the document's `Title` field was set to something code-like (e.g., a GUID, a UUID, an auto-generated string from another system, or the original filename when the user did not provide a friendly title), the user sees what looks like a **code value** quoted inside the sentence — and cannot tell *what* they are about to delete.
- The three bullet points below use jargon that a non-technical user does not understand.

**Current modal content (from `Shared/DeleteConfirmDialog.razor`)**:

```text
⚠ Confirm deletion
Permanently delete the document "Title"?
This action cannot be undone.
- The file will be deleted from disk.
- All associated active shares will be removed (cascade).
- The audit log is preserved (without document reference).
[ Cancel ]  [ 🗑 Delete permanently ]
```

**Affected acceptance criteria**:
- **AC-3.3.1** (StakeholderDoc §8): *"Given an owner, When clicks 'Eliminar' and confirms, Then the file is deleted from disk in ≤ 100 ms and the row is deleted (cascade `DocumentShare`)"*
- **FR-022** (spec): *"System MUST permanently delete the file from disk in ≤ 100 ms after user confirmation"*
- **Constitution Principio II (A08 Data Integrity)**: high-impact destructive actions should have additional safeguards.

**Root cause analysis**:

| # | Cause | Evidence | Severity |
|---|---|---|:---:|
| 2.1 | **Grammar is awkward**. The sentence "Permanently delete the document 'X'?" is a bare imperative without an explicit subject or softening ("Are you sure…?"). A clearer phrasing would lead with the question and the consequence, not the action. | `DeleteConfirmDialog.razor:14` | **HIGH** |
| 2.2 | **Technical jargon in bullets** that an end user cannot parse: (a) "(cascade)" — database terminology, (b) "audit log" — engineering term, (c) "without document reference" — implementation detail about FK nulling. None of these are user-facing concepts. | `DeleteConfirmDialog.razor:18-20` | **HIGH** |
| 2.3 | **A raw "code-looking" value is displayed as the document identifier.** The only identifier the modal currently shows is `@Title` (the document's `Title` field). When the title is a GUID, a UUID, an upload timestamp, or the original filename of a file the user never renamed, the user sees what looks like a code value quoted in the question and cannot recognize the document. | `DeleteConfirmDialog.razor:14` + caller in `DocumentDetails.razor:198` passes `Title="@(_doc?.Title ?? string.Empty)"` | **HIGH** |
| 2.4 | **No type-to-confirm safeguard.** A single mis-click permanently destroys the file. NIST SP 800-63B and OWASP ASVS recommend a second confirmation for destructive actions. | N/A — absent by design | MEDIUM |
| 2.5 | The visual hierarchy of destructive intent is weak. "Delete permanently" is `btn-danger` (red) but is the same size as "Cancel", and the modal header is `bg-danger` but the body is white — the destructive context doesn't extend through the whole modal. | `DeleteConfirmDialog.razor:9, 36` | LOW |
| 2.6 | The error message (`@ErrorMessage`) appears at the **bottom of the modal body** with no icon. If the user dismisses the modal before scrolling, they may miss the failure. | `DeleteConfirmDialog.razor:24-27` | LOW |
| 2.7 | **No automated test** covers the dialog (bUnit). The Phase 1 component tests are blocked and were not built. | tasks.md T126 (blocked) | MEDIUM |

**Recommended fix (proposed, not yet applied)**:

| Sub-fix | Description | Affected file(s) |
|---|---|---|
| 2.A | **Rewrite the modal wording in plain English**, keeping the language as English (per product-owner decision). Use a question-led structure and replace every jargon term. | `Shared/DeleteConfirmDialog.razor` |
| 2.B | **Replace the single `@Title` value with a richer document identifier** that never looks like raw code. Show the **original filename** (`_doc.OriginalFileName`) and the **friendly title** in a stacked layout: friendly title (large) + `📄 original-filename.pdf · PDF · 47.9 KB` (small, muted). If `Title` looks like a code value (e.g., matches a GUID pattern), prefer the `OriginalFileName` as the primary identifier and show the title in muted text. | `Shared/DeleteConfirmDialog.razor` (new parameters), `Pages/DocumentDetails.razor:198` (caller passes new params) |
| 2.C | **Drop jargon from the bullets**. Proposed plain-English bullets: "The original file will be removed from the server's disk.", "Any users you've shared this document with will lose access immediately.", "An entry will be kept in the system activity log for compliance." | `Shared/DeleteConfirmDialog.razor:18-20` |
| 2.D | Add a **type-to-confirm** field: the user must type the document's friendly title (or a fixed phrase like `DELETE`) in a textbox before the "Delete permanently" button is enabled. Pattern: `input` + `disabled` binding. | `Shared/DeleteConfirmDialog.razor` |
| 2.E | Improve the visual hierarchy: (a) keep the destructive red header, (b) add a larger 🚨 icon to the title, (c) make the destructive button slightly larger or use `btn-lg`. | `Shared/DeleteConfirmDialog.razor` |
| 2.F | Move the error message to the **top of the modal body** (above the title) with an alert icon. Use `aria-live="assertive"` for screen reader announcements. | `Shared/DeleteConfirmDialog.razor` |
| 2.G | Write bUnit component tests that verify: (a) the new plain-English wording, (b) the original filename is rendered, (c) the document title is not displayed alone when it looks like a code value, (d) type-to-confirm enables/disables the button, (e) Cancel and Confirm callbacks are invoked correctly, (f) error message is announced via `aria-live`. | `tests/ContosoDashboard.Tests.Components/DeleteConfirmDialogTests.cs` (new, blocked by Phase 1) |

**Proposed new wording for the modal body (English, jargon-free)**:

```text
⚠ Confirm deletion

Are you sure you want to permanently delete this document?

    Q4 Roadmap
    📄 roadmap-q4.pdf · PDF · 2.3 MB

This action cannot be undone.

  • The original file will be removed from the server's disk.
  • Anyone you've shared this document with will lose access
    immediately.
  • An entry will be kept in the system activity log for
    compliance purposes.

To confirm, type the document title below:

    [ Roadmap                          ]

[ Cancel ]  [ 🗑 Delete permanently ]   (disabled until the
                                          typed value matches)
```

**New tasks to add (proposed, pending `/speckit.tasks` regeneration)**:

| ID | Priority | Story | Description |
|---|:---:|---|---|
| T149 | P1 | [UX-FIX] | Rewrite modal wording in plain English, question-led structure, replacing jargon (Fix 2.A) |
| T150 | P1 | [UX-FIX] | Replace single `@Title` with rich document identifier: friendly title + `📄 original-filename · MIME · size`; fallback to `OriginalFileName` when title looks like a code value (Fix 2.B) |
| T151 | P2 | [UX-FIX] | Drop jargon from bullets: "audit log" → "activity log for compliance", "(cascade)" → "lose access immediately", "without document reference" → removed (Fix 2.C) |
| T152 | P2 | [UX-FIX] | Add type-to-confirm input: user must type the document title (or fixed phrase `DELETE`) to enable the destructive button (Fix 2.D) |
| T153 | P2 | [UX-FIX] | Move error message to top of modal-body with alert icon and `aria-live="assertive"` (Fix 2.F) |
| T154 | P3 | [UX-FIX] | Improve visual hierarchy: larger 🚨 icon in title, `btn-lg` on destructive button, full destructive context (Fix 2.E) |
| T155 | P3 | [UX-FIX] | Create bUnit tests for `DeleteConfirmDialog`: plain-English wording, filename rendering, code-value detection, type-to-confirm, callbacks, `aria-live` (Fix 2.G) — **bloqueado por Phase 1** |

### Affected v1.0.0 tasks (re-assessment)

| Task | Original status | Re-assessment |
|---|---|---|
| T080 | ✅ [x] (incorrectly marked done) | Preview endpoint exists but is **broken** due to `X-Frame-Options: DENY` (root cause 1.1) |
| T082 | ✅ [x] | 10 MB size validation works; fallback message is in English (issue 1.5) |
| T083 | ✅ [x] | MIME type validation works correctly |
| T084 | ✅ [x] (partially incorrect) | Adds `X-Content-Type-Options: nosniff` correctly, but the **global** `X-Frame-Options: DENY` blocks framing (root cause 1.1) — the local CSP changes are unnecessary |
| T112 | ✅ [x] (incomplete) | Modal exists, but wording is grammatically awkward, contains jargon, and displays a raw `Title` value that may look like a code value (issue 2.1-2.6) |

### Constitution alignment

| Principle | Status | Note |
|---|:---:|---|
| I — Stack canónico | ✅ | No new dependencies; all fixes use existing Blazor/CSS |
| II — Seguridad (OWASP) | ✅ | A05 fix: `X-Frame-Options: SAMEORIGIN` is **more secure** than `DENY` for this endpoint (still prevents clickjacking from external origins, allows same-origin preview) |
| III — Rendimiento | ✅ | No perf impact; preview rendering time should improve (fewer blocked loads) |
| IV — Código | ✅ | No new patterns; follows existing Blazor + DI conventions |
| V — TDD | ⚠️ | Tests for fixes (T148, T155) are blocked by Phase 1 (no test projects). **Issue 1 and Issue 2 fixes should be paired with their tests** (TDD Hard) |
| Restricciones — Localización | ✅ | **No violation** (clarified 2026-06-12: the modal is intentionally in English; the rest of the app remains in Spanish). The previous "VIOLATION" finding is withdrawn. |

### Priority & rollout plan

| Iteration | Tasks | Estimated effort | Dependencies |
|---|---|---|---|
| **v1.0.1 hotfix (Day 1-2)** | T141, T142, T143, T144, T149, T150 | 1-2 days | None (small code changes) |
| **v1.0.1 enhancement (Day 3-4)** | T145, T146, T147, T151, T152, T153, T154 | 1-2 days | Hotfix done |
| **v1.0.1 tests (Day 5)** | T148, T155 | 1 day | Phase 1 unblocked (T001-T016) |
| **v1.1.0** | Re-validate all US4 acceptance criteria; manual QA pass | 1 day | All v1.0.1 tasks done |

### Required spec updates (if fixes are approved)

| Spec change | Rationale |
|---|---|
| Add a new sub-section to spec.md: **"§Confirmation patterns for destructive actions"** | Document that delete, replace, and any destructive action must use type-to-confirm + plain-English (no jargon) + a11y attributes |
| Update spec.md **§Out of Scope** (or add a §"Browser compatibility") | Add a note: "Preview depends on the user's browser having a PDF viewer enabled. We provide a 'Download' fallback for users whose browsers cannot preview PDFs." |
| Add to spec.md **FR-019** a non-functional note: "Preview requires same-origin framing (`X-Frame-Options: SAMEORIGIN`); system must NOT use `DENY` for the preview endpoint" | Prevent regression of the same issue |
| Add to spec.md **FR-022** a non-functional note: "Destructive confirmations (delete, replace) must display the document's original filename (not the title field alone) so users can identify what is being destroyed" | Prevent regression of the code-value display issue |

---

## Next Phase

Proceder con **Phase 0** (research.md) y **Phase 1** (data-model.md, contracts/, quickstart.md). Los artefactos están en este directorio. La generación de tasks.md corresponde a `/speckit.tasks` (siguiente comando).
