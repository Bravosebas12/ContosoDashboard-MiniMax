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
| V | **TDD Hard + Pirámide Completa** | ✅ PASS | 8 niveles con proyectos separados (`.Tests.Unit`, `.Tests.Components`, `.Tests.Integration`, `.Tests.Contract`, `.Tests.E2E.Api`, `.Tests.E2E.UI`, `.Tests.Performance`); Stryker.NET ≥ 70% gate bloqueante; BenchmarkDotNet + NBomber + k6 para 6 niveles de pirámide de rendimiento; 5 perfiles de carga (smoke, load, stress, spike, soak); TDD Hard mandatorio; tests escritos ANTES de la implementación |

**Resultado final de gates**: ✅ **TODOS LOS GATES PASAN** después del diseño — no se requieren violaciones justificadas. La Constitución v1.1.0 se respeta en su totalidad.

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
│   └── LocalFileStorageServiceTests.cs        (unit técnicas)
├── Helpers/
│   ├── MimeTypeValidatorTests.cs
│   ├── FilePathBuilderTests.cs
│   └── DocumentConstantsTests.cs
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

## Next Phase

Proceder con **Phase 0** (research.md) y **Phase 1** (data-model.md, contracts/, quickstart.md). Los artefactos están en este directorio. La generación de tasks.md corresponde a `/speckit.tasks` (siguiente comando).
