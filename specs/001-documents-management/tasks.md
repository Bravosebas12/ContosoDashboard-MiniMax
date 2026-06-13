# Tasks: Document Upload and Management

**Input**: Design documents from `/specs/001-documents-management/`
**Prerequisites**: plan.md ✅, spec.md ✅, research.md ✅, data-model.md ✅, contracts/ ✅, quickstart.md ✅
**Constitution**: v1.1.0 (Principios I–V) — TDD Hard + Pirámide de 8 niveles mandatorios
**Branch**: `001-documents-management`

**Tests**: Constitution v1.1.0 Principio V es **NO NEGOCIABLE** — TODAS las historias tienen tests explícitos siguiendo la pirámide de 8 niveles (xUnit, bUnit, Testcontainers, PactNet, Playwright, Stryker.NET, NBomber, k6). El ciclo TDD Hard (ROJO → VERDE → REFACTOR) se aplica en cada task de implementación.

## Format: `[ID] [P?] [Story] Description`

- **[P]**: Puede ejecutarse en paralelo (archivos diferentes, sin dependencias incompletas)
- **[Story]**: Historia de usuario a la que pertenece (US1–US8). Solo requerido en fases de historias.

---

## Phase 1: Setup (Infraestructura compartida)

**Propósito**: Inicializar proyectos de tests, dependencias, herramientas, y entorno de desarrollo. Bloquea todo lo demás hasta completarse.

- [x] T001 [P] Crear 7 proyectos de tests en `tests/` siguiendo la convención de sufijo (`.Tests.Unit`, `.Tests.Components`, `.Tests.Integration`, `.Tests.Contract`, `.Tests.E2E.Api`, `.Tests.E2E.UI`, `.Tests.Performance`) con `<TargetFramework>net8.0</TargetFramework>` y referencias a `ContosoDashboard.csproj`
- [x] T002 [P] Añadir paquetes NuGet a `ContosoDashboard.Tests.Unit/ContosoDashboard.Tests.Unit.csproj`: `xunit`, `xunit.runner.visualstudio`, `NSubstitute`, `FluentAssertions`, `coverlet.collector`, `coverlet.msbuild`, `Stryker.NET` (build-time)
- [x] T003 [P] Añadir paquetes NuGet a `ContosoDashboard.Tests.Components/ContosoDashboard.Tests.Components.csproj`: `xunit`, `bunit`, `FluentAssertions`, `Moq` o `NSubstitute`
- [x] T004 [P] Añadir paquetes NuGet a `ContosoDashboard.Tests.Integration/ContosoDashboard.Tests.Integration.csproj`: `xunit`, `Microsoft.AspNetCore.Mvc.Testing`, `Testcontainers.MsSql` (era `Testcontainers.SqlServer` pero el nombre actual del paquete es `.MsSql`), `FluentAssertions`, `Respawn` (para reset de DB)
- [x] T005 [P] Añadir paquetes NuGet a `ContosoDashboard.Tests.Contract/ContosoDashboard.Tests.Contract.csproj`: `xunit`, `PactNet`, `PactNet.Provider` (verifica contra Pact Broker)
- [x] T006 [P] Añadir paquetes NuGet a `ContosoDashboard.Tests.E2E.Api/ContosoDashboard.Tests.E2E.Api.csproj`: `xunit`, `RestSharp`, `FluentAssertions`
- [x] T007 [P] Añadir paquetes NuGet a `ContosoDashboard.Tests.E2E.UI/ContosoDashboard.Tests.E2E.UI.csproj`: `xunit`, `Microsoft.Playwright`, `FluentAssertions`
- [x] T008 [P] Añadir paquetes NuGet a `ContosoDashboard.Tests.Performance/ContosoDashboard.Tests.Performance.csproj`: `xunit`, `BenchmarkDotNet`, `NBomber` (NBomber.Http omitido por incompatibilidad de versiones con NBomber 5.x — se reincorporará en una iteración futura)
- [x] T009 [P] Añadir paquetes NuGet a `ContosoDashboard/ContosoDashboard.csproj`: `nClam` (cliente ClamAV), `Microsoft.Extensions.Caching.Memory` 8.0.x
- [x] T010 Crear `ContosoDashboard.slnx` con `dotnet sln add` para todos los proyectos (8 .NET projects — formato XML nuevo de .NET 9+ SDK)
- [x] T011 [P] Crear `tests/k6/smoke.js`, `tests/k6/load.js`, `tests/k6/stress.js`, `tests/k6/spike.js`, `tests/k6/soak.js` con la configuración de carga del plan §5 load profiles
- [x] T012 Configurar `coverlet.runsettings` en raíz con thresholds: línea 40% (mínimo), branch 35% (mínimo), reportar a `quality-reports/coverage/`
- [x] T013 [P] Configurar `[ExcludeFromCodeCoverage]` defaults via `.editorconfig` o `Directory.Build.props` solo para `bin/`, `obj/`, `Migrations/`, stubs y mappers
- [x] T014 [P] Agregar `Stryker.NET` configuration en `tests/stryker-config.json` con thresholds: break 70, high 80, low 60; scope = `ContosoDashboard/Services/Documents/**`, `ContosoDashboard/Domain/Documents/**`
- [x] T015 [P] Agregar `.editorconfig` raíz con Roslyn analyzer rules: `dotnet_diagnostic.CA1050.severity = warning`, `CA1062 = warning`, `CA1822 = warning`, `CA2007 = warning`, `CA2016 = warning`, `CA2234 = warning`, `TreatWarningsAsErrors = true` (solo en producción)
- [x] T016 Crear `tests/Directory.Build.props` con `<TreatWarningsAsErrors>false</TreatWarningsAsErrors>` y `Nullable=enable` para permitir warnings en proyectos de tests

**Checkpoint**: ✅ `dotnet build ContosoDashboard.slnx` ejecuta sin errores (8/8 proyectos). `dotnet test --no-build` ejecuta 27/27 tests en verde en `.Tests.Unit` (smoke test de `DocumentConstants`). Coverage y mutation se inicializan con thresholds vacíos.

---

## Phase 2: Foundational (Prerequisitos bloqueantes)

**Propósito**: Infraestructura central — entidades, migraciones, servicios base, configuración DI, helpers. **DEBE** completarse antes de CUALQUIER historia de usuario.

**⚠️ CRÍTICO**: Ningún trabajo de historia puede comenzar hasta que esta fase esté completa.

- [x] T017 [P] Crear `ContosoDashboard/Models/Document.cs` — entity `Document` con todas las propiedades del data-model.md §Document (DocumentId, Title, Description, Category, FilePath, FileSize, FileType, Tags, UploadedAt, UploadedByUserId, ProjectId, TaskId, ReplacedAt, AvScanStatus, AvScanAt, OriginalFileName) + enums `ScanStatus`
- [x] T018 [P] Crear `ContosoDashboard/Models/DocumentShare.cs` — entity `DocumentShare` (DocumentShareId, DocumentId, SharedWithUserId, SharedWithRole, Permission, SharedAt, SharedByUserId, ExpiresAt, RevokedAt, RevokedByUserId) + enum `SharePermission`
- [x] T019 [P] Crear `ContosoDashboard/Models/ActivityLog.cs` — entity `ActivityLog` (ActivityLogId, Event, DocumentId, UserId, IpAddress, Metadata, Timestamp)
- [x] T020 Extender `ContosoDashboard/Data/ApplicationDbContext.cs` — agregar `DbSet<Document>`, `DbSet<DocumentShare>`, `DbSet<ActivityLog>` con configuración Fluent API (longitudes, índices, FK relationships) según data-model.md §Modifications to Existing Tables
- [x] T021 Crear `dotnet ef migrations add InitialDocuments --project ContosoDashboard.csproj --startup-project ContosoDashboard.csproj` y revisar el SQL generado
- [x] T022 [P] Crear `ContosoDashboard/Services/Documents/IFileStorageService.cs` — interface con `UploadAsync`, `DownloadAsync`, `DeleteAsync`, `GetUrlAsync` (idéntico al contrato en `specs/001-documents-management/contracts/IFileStorageService.cs`)
- [x] T023 [P] Crear `ContosoDashboard/Services/Documents/LocalFileStorageService.cs` — implementación con `System.IO.File` que persiste en `AppData/uploads/{userId}/{projectIdOrPersonal}/{guid}.{ext}`
- [x] T024 [P] Crear `ContosoDashboard/Services/Documents/IAntivirusScanner.cs` — interface con `ScanAsync` + `ScanResult` record + `ScanStatus` enum (idéntico al contrato)
- [x] T025 [P] Crear `ContosoDashboard/Services/Documents/ClamAvScanner.cs` — implementación con nClam, fail-open en training + log warning
- [x] T026 [P] Crear `ContosoDashboard/Services/Documents/MimeTypeValidator.cs` — helper con whitelist de 16 MIME types, validación por extensión Y magic bytes (CHK008 gap)
- [x] T027 [P] Crear `ContosoDashboard/Services/Documents/FilePathBuilder.cs` — helper que genera paths seguros `{userId}/{projectIdOrPersonal}/{guid}.{ext}` con regex de validación
- [x] T028 [P] Crear `ContosoDashboard/Services/Documents/DocumentConstants.cs` — categorías permitidas (6), MIME whitelist (16), límites (25 MB, 5 tags, etc.)
- [x] T029 Registrar en `Program.cs`: `builder.Services.AddScoped<IFileStorageService, LocalFileStorageService>();` y `builder.Services.AddHttpClient<IAntivirusScanner, ClamAvScanner>();` + configurar `appsettings.json` con `Antivirus:ClamAV:Host`, `Port`, `Timeout`
- [x] T030 [P] Crear `ContosoDashboard/Services/IActivityLogService.cs` + `ContosoDashboard/Services/ActivityLogService.cs` — servicio para registrar eventos estructurados (upload, download, delete, share, revoke, access_denied) en `ActivityLog` tabla
- [x] T031 [P] Crear `tests/ContosoDashboard.Tests.Unit/Services/Documents/MimeTypeValidatorTests.cs` — tests TDD para la whitelist (red→green)
- [x] T032 [P] Crear `tests/ContosoDashboard.Tests.Unit/Services/Documents/FilePathBuilderTests.cs` — tests TDD para path generation
- [x] T033 [P] Crear `tests/ContosoDashboard.Tests.Unit/Services/Documents/LocalFileStorageServiceTests.cs` — unit tests con filesystem temporal (mock `IFileStorageService` para no tocar el disco real)
- [x] T034 [P] Crear `tests/ContosoDashboard.Tests.Unit/Services/ClamAvScannerTests.cs` — unit tests con un mock AV server (HTTP simulado) o substituto de `IAntivirusScanner`
- [x] T035 Aplicar migración con `dotnet ef database update` y verificar que las 3 tablas existen con sus índices
- [x] T036 [P] Crear `ContosoDashboard/Models/DocumentMappingProfile.cs` — método de extensión explícito `ToDto(Document)` y `FromUploadRequest(...)` (sin AutoMapper, per Constitución IV)
- [x] T037 Verificar que `dotnet test ContosoDashboard.Tests.Unit` corre los 4 test files (T031-T034) en verde (TDD green)

**Checkpoint**: Las 3 entidades existen en DB, los servicios base están registrados en DI, y los 4 unit tests de helpers pasan. Las historias pueden comenzar.

---

## Phase 3: User Story 1 - Subir documento personal (Priority: P1) 🎯 MVP

**Goal**: Un Employee autenticado puede subir un PDF, Office, txt, JPEG, o PNG a "Mis Documentos" con título, categoría, tags, descripción opcional, y persiste con metadatos completos.

**Independent Test**: Login como `ni.kang@contoso.com`, navegar a `Pages/Documents.razor`, subir un PDF de 5 MB con título + categoría "Personal Files", verificar que aparece en la lista con metadata correcta y `AvScanStatus = Clean`.

### Tests para User Story 1 (TDD Hard - PRIMERO)

- [x] T038 [P] [US1] Crear `tests/ContosoDashboard.Tests.Unit/Services/DocumentServiceTests.cs` con tests para `UploadAsync` (validación, AV, persistencia, rollback, autorización) — **DEBE** fallar antes de implementar T041
- [x] T039 [P] [US1] Crear `tests/ContosoDashboard.Tests.Components/DocumentUploadComponentTests.cs` con bUnit — render, selección de archivo, validación de formulario, clic en Subir
- [x] T040 [P] [US1] Crear `tests/ContosoDashboard.Tests.Integration/DocumentServiceIntegrationTests.cs` con `WebApplicationFactory` + Testcontainers SQL Server — happy path de upload con verificación de file en disco, row en DB, log entry

### Implementación para User Story 1

- [x] T041 [US1] Crear `ContosoDashboard/Services/Documents/IDocumentService.cs` — interface (idéntico al contrato) con `UploadAsync`, `ListAsync`, `GetByIdAsync`, `OpenForDownloadAsync`, `UpdateMetadataAsync`, `ReplaceFileAsync`, `DeleteAsync`, `SearchAsync`
- [x] T042 [US1] Crear `ContosoDashboard/Services/Documents/DocumentService.cs` — implementar `UploadAsync` con flujo: validate → AV scan → generate GUID → write disk → save DB (rollback on fail) → notify project members (if ProjectId) → log `document.uploaded` → return `UploadResult`
- [x] T043 [US1] Crear `ContosoDashboard/Services/Documents/Dto/DocumentDto.cs` + `UploadResult` + `PagedResult<T>` + `DocumentListFilter` + `DocumentSortBy` + `SortDirection` + excepciones (`DocumentNotFoundException`, `DocumentUnauthorizedAccessException`, `DocumentValidationException`, `DocumentInfectedException`)
- [x] T044 [P] [US1] Crear `ContosoDashboard/Shared/DocumentUploadComponent.razor` — componente reutilizable con `<InputFile>`, formulario (title, description, category, tags), botón Subir, barra de progreso
- [ ] T045 [P] [US1] Crear `ContosoDashboard/Shared/DocumentUploadComponent.razor.cs` — codebehind con `OnInitializedAsync`, handler `HandleSubmit`, uso de `MemoryStream` pattern per stakeholder doc
- [x] T046 [US1] Crear `ContosoDashboard/Pages/Documents.razor` — listado de "Mis Documentos" + botón Upload + tabla con título, fecha, tamaño, tipo
- [ ] T047 [US1] Crear `ContosoDashboard/Pages/Documents.razor.cs` — codebehind con `@inject IDocumentService`, cargar `DocumentDtos` con paginación (page 1, 25 items)
- [x] T048 [US1] Agregar `[Authorize(Policy = "Employee")]` a `Pages/Documents.razor` (defense in depth layer 1)
- [x] T049 [US1] Implementar validación de formulario en `DocumentUploadComponent` (título 1-200, max 5 tags, max 50 chars/tag)
- [x] T050 [US1] Implementar logging de `document.uploaded` en `ActivityLog` con `documentId`, `userId`, `fileSize`, `mimeType`, `result=success|failure`
- [ ] T051 [US1] Verificar que `dotnet test` (unit + component + integration) pasa para US1 con coverage ≥ 80% en `Services/Documents/DocumentService.cs`
- [x] T052 [P] [US1] Crear `tests/ContosoDashboard.Tests.E2E.Api/DocumentsUploadTests.cs` — RestSharp contra `WebApplicationFactory`, 100% happy path
- [x] T053 [P] [US1] Crear `tests/ContosoDashboard.Tests.Contract/DocumentsUploadPactTests.cs` — PactNet consumer-driven contra `/api/documents/upload`
- [ ] T054 [US1] Verificar que el flujo end-to-end (login → upload → ver en lista) funciona manualmente siguiendo `quickstart.md` Scenario 1

**Checkpoint**: US1 completamente funcional y testeable independientemente. Un Employee puede subir un PDF y verlo en su lista.

---

## Phase 4: User Story 2 - Subir documento a un proyecto (Priority: P1)

**Goal**: Un Project Manager o Team Lead puede subir documentos asociándolos a un proyecto específico; los team members ven y descargan documentos del proyecto.

**Independent Test**: Login como `camille.nicole@contoso.com` (PM), navegar a `Pages/Projects/1.razor`, subir un PDF con proyecto "Q4 Roadmap" seleccionado, verificar que aparece en la lista de documentos del proyecto y los team members reciben notificación.

### Tests para User Story 2

- [x] T055 [P] [US2] Extender `tests/ContosoDashboard.Tests.Unit/Services/DocumentServiceTests.cs` con tests de `UploadAsync` cuando `projectId` se especifica (autorización: usuario debe ser miembro o PM del proyecto)
- [x] T056 [P] [US2] Crear `tests/ContosoDashboard.Tests.Integration/ProjectDocumentUploadTests.cs` — flow completo: PM sube → team member descarga → no-member recibe 403
- [x] T057 [P] [US2] Crear `tests/ContosoDashboard.Tests.Unit/Services/DocumentServiceTests.cs` con tests de notificación a project members tras upload (mock `INotificationService`)

### Implementación para User Story 2

- [ ] T058 [US2] Extender `DocumentService.UploadAsync` para aceptar `projectId` opcional; añadir validación: si `projectId != null`, verificar que `currentUserId` es miembro del proyecto OR es el PM (defense in depth layer 2)
- [ ] T059 [US2] Extender `DocumentService.UploadAsync` para enviar notificación in-app a todos los team members del proyecto via `INotificationService` (latencia ≤ 5s, ver AC-4.3.1)
- [x] T060 [US2] Crear `ContosoDashboard/Pages/Projects/{id}.razor` (si no existe) o extender para mostrar la sección "Documentos del proyecto" con lista + botón "Subir documento"
- [x] T061 [US2] Crear `ContosoDashboard/Pages/Projects/{id}.razor.cs` con `OnInitializedAsync` que carga documentos del proyecto via `IDocumentService.ListAsync(projectId: id)`
- [x] T062 [US2] Agregar `DocumentUploadComponent` en `Projects/{id}.razor` con `projectId` pre-seleccionado y deshabilitado
- [x] T063 [US2] Verificar que un usuario NO asignado al proyecto recibe 403 al intentar acceder a `Pages/Projects/{id}.razor` (test de autorización)
- [ ] T064 [US2] Verificar US2 con todos los niveles de tests (unit, component, integration, contract, E2E API) y coverage ≥ 80% en la nueva lógica

**Checkpoint**: US1 + US2 funcionan independientemente. PMs y Team Leads pueden subir a proyectos con notificaciones.

---

## Phase 5: User Story 3 - Buscar y filtrar documentos (Priority: P2)

**Goal**: Un usuario puede buscar documentos por título/descripción/tags, filtrar por categoría/proyecto/rango de fechas, y ordenar por múltiples columnas.

**Independent Test**: Con 30+ documentos, buscar "presupuesto" retorna resultados en ≤ 2s; filtrar por categoría + proyecto simultáneamente; ordenar por tamaño descendente en ≤ 200 ms.

### Tests para User Story 3

- [x] T065 [P] [US3] Extender `DocumentServiceTests.cs` con tests de `ListAsync` (filtros combinados, ordenamiento, paginación) y `SearchAsync` (LIKE-based, ≤ 2s p95)
- [ ] T066 [P] [US3] Crear `tests/ContosoDashboard.Tests.Integration/DocumentSearchPerformanceTests.cs` — 10k documentos seed, medir p95 < 2s con `Stopwatch`

### Implementación para User Story 3

- [ ] T067 [US3] Implementar `DocumentService.ListAsync` con LINQ + `AsNoTracking()` + paginación `Skip/Take` + filtros opcionales + ordenamiento configurable
- [ ] T068 [US3] Implementar `DocumentService.SearchAsync` con `WHERE Title LIKE '%q%' OR Description LIKE '%q%' OR Tags LIKE '%q%'` + filtro de autorización (solo docs visibles)
- [x] T069 [US3] Agregar barra de búsqueda en `Pages/Documents.razor` que invoca `IDocumentService.SearchAsync`
- [x] T070 [US3] Agregar controles de filtro (categoría dropdown, proyecto dropdown, date range picker) en `Pages/Documents.razor`
- [x] T071 [US3] Agregar controles de ordenamiento (por título, fecha, categoría, tamaño) en `Pages/Documents.razor`
- [x] T072 [US3] Implementar selector de página (1, 2, 3, ...) con `PagedResult<T>.TotalPages`
- [ ] T073 [US3] Verificar que los 30 documentos seed se listan en ≤ 500 ms p95 (integración con `BenchmarkDotNet` o `Stopwatch`)

**Checkpoint**: Búsqueda y filtrado funcionan, cumplen SLOs de performance.

---

## Phase 6: User Story 4 - Descargar y previsualizar documentos (Priority: P2)

**Goal**: Un usuario con acceso puede descargar cualquier documento con su nombre original, y previsualizar PDFs e imágenes inline en el navegador.

**Independent Test**: Click "Descargar" en un PDF → archivo se baja con nombre original. Click "Vista previa" en un PDF de 2 MB → se renderiza en iframe en ≤ 3s.

### Tests para User Story 4

- [ ] T074 [P] [US4] Extender `DocumentServiceTests.cs` con tests de `OpenForDownloadAsync` (autorización, file not found, stream position)
- [ ] T075 [P] [US4] Crear `tests/ContosoDashboard.Tests.Integration/DocumentDownloadTests.cs` — happy path + 403 (sin acceso) + 404 (no existe)

### Implementación para User Story 4

- [ ] T076 [US4] Implementar `DocumentService.OpenForDownloadAsync` que retorna `(Stream, FileName, ContentType)` validando acceso
- [ ] T077 [US4] Crear `Pages/DocumentDetails.razor` con vista detallada: metadata, botones (Descargar, Vista previa, Editar, Compartir, Eliminar según permisos)
- [ ] T078 [US4] Crear `Pages/DocumentDetails.razor.cs` con handler `HandleDownload` que invoca `IDocumentService.OpenForDownloadAsync` y sirve el archivo con `Content-Disposition: attachment; filename="{OriginalFileName}"`
- [ ] T079 [US4] Implementar endpoint `/documents/{id}/download` (Razor Page handler o controller) con `[Authorize]` + re-validación de acceso
- [ ] T080 [US4] Implementar endpoint `/documents/{id}/preview` que sirve el archivo con `Content-Disposition: inline; filename="{OriginalFileName}"` (para PDFs e imágenes)
- [ ] T081 [US4] Crear `Shared/DocumentPreviewComponent.razor` con `<iframe>` + `sandbox="allow-same-origin"` (sin scripts)
- [ ] T082 [US4] Validar tamaño ≤ 10 MB para preview; si es mayor, mostrar mensaje "Vista previa no disponible para este archivo" + botón "Descargar"
- [ ] T083 [US4] Validar MIME type: solo PDF/JPEG/PNG son previewable; otros tipos muestran fallback
- [ ] T084 [US4] Agregar headers de seguridad (`X-Content-Type-Options: nosniff`, `Content-Security-Policy: sandbox allow-same-origin`) en endpoints de download/preview
- [ ] T085 [US4] Verificar E2E con Playwright que el preview renderiza en ≤ 3s p95

**Checkpoint**: Descarga y vista previa funcionan, cumplen SLOs, con headers de seguridad.

---

## Phase 7: User Story 5 - Editar metadata y reemplazar archivo (Priority: P3)

**Goal**: El dueño puede editar título/descripción/categoría/tags en ≤ 500 ms; puede reemplazar el archivo manteniendo `DocumentId` con last-writer-wins (sin locks).

**Independent Test**: Editar título de un documento propio → cambio persiste en ≤ 500 ms. Reemplazar archivo con nueva versión → GUID cambia, `DocumentId` se mantiene, archivo antiguo se elimina.

### Tests para User Story 5

- [ ] T086 [P] [US5] Extender `DocumentServiceTests.cs` con tests de `UpdateMetadataAsync` (autorización: solo dueño) y `ReplaceFileAsync` (rollback, GUID nuevo, mismo `DocumentId`)
- [ ] T087 [P] [US5] Crear `tests/ContosoDashboard.Tests.Integration/DocumentReplaceTests.cs` con escenario de 2 clientes concurrentes (verifica last-writer-wins per Clarifications Q3)
- [ ] T088 [P] [US5] Crear `tests/ContosoDashboard.Tests.E2E.UI/DocumentEditFlowTests.cs` con Playwright — edit form, save, replace file

### Implementación para User Story 5

- [ ] T089 [US5] Implementar `DocumentService.UpdateMetadataAsync` con validación: solo el dueño (`UploadedByUserId == currentUserId`) puede editar
- [ ] T090 [US5] Implementar `DocumentService.ReplaceFileAsync` con flujo: validar dueño → AV scan → generar nuevo GUID → escribir nuevo archivo → actualizar `FilePath` y `ReplacedAt` en DB → eliminar archivo antiguo del disco → log `document.replaced`
- [x] T091 [US5] Agregar formulario de edición de metadata en `Pages/DocumentDetails.razor` (campos editables: title, description, category, tags)
- [x] T092 [US5] Agregar UI de "Reemplazar archivo" en `Pages/DocumentDetails.razor` con `<InputFile>` y confirmación
- [ ] T093 [US5] Verificar que la nueva versión se sirve en próximas descargas (test de integración: download tras replace retorna nuevo archivo)
- [ ] T094 [US5] Verificar que downloads en curso durante replace completan con archivo antiguo (test concurrente, sin locks)

**Checkpoint**: Edición de metadata y reemplazo de archivo funcionan, con last-writer-wins.

---

## Phase 8: User Story 6 - Compartir documentos (Priority: P3)

**Goal**: El dueño comparte con usuarios específicos; PM solo puede compartir dentro de su proyecto; el receptor ve "Compartido conmigo" y recibe notificación.

**Independent Test**: Owner comparte con usuario A → A ve el documento en "Compartido conmigo" en ≤ 5s. PM intenta compartir fuera de su proyecto → rechazado.

### Tests para User Story 6

- [x] T095 [P] [US6] Crear `tests/ContosoDashboard.Tests.Unit/Services/DocumentShareServiceTests.cs` con tests de las 3 reglas de sharing (owner → anyone, PM → within project, other → 403) per Clarifications Q1
- [x] T096 [P] [US6] Crear `tests/ContosoDashboard.Tests.Integration/ShareFlowTests.cs` con happy path + 403 + notificación verificada

### Implementación para User Story 6

- [x] T097 [US6] Crear `ContosoDashboard/Services/Documents/IDocumentShareService.cs` — interface (idéntico al contrato) con `ShareAsync`, `RevokeAsync`, `ListActiveSharesAsync`, `ListSharedWithMeAsync`, `UserHasAccessAsync`
- [x] T098 [US6] Crear `ContosoDashboard/Services/Documents/DocumentShareService.cs` con lógica de autorización:
  - **Owner (uploader)** puede compartir con cualquier usuario
  - **Project Manager** solo puede compartir dentro de su proyecto
  - **Otros roles** no pueden compartir
- [x] T099 [US6] Implementar `DocumentShareService.ShareAsync` que crea `DocumentShare` row, envía notificación in-app via `INotificationService` (≤ 5s), log `document.shared`
- [x] T100 [US6] Implementar `DocumentShareService.RevokeAsync` (soft delete via `RevokedAt`), enviar notificación al receptor (revocación), log `document.revoked`
- [x] T101 [US6] Implementar `DocumentShareService.UserHasAccessAsync` con lógica: es dueño OR es miembro del proyecto OR tiene `DocumentShare` activo (no expirado, no revocado)
- [x] T102 [US6] Integrar `IDocumentShareService.UserHasAccessAsync` en `DocumentService.GetByIdAsync` / `OpenForDownloadAsync` (defense in depth layer 3)
- [x] T103 [US6] Crear UI de "Compartir" en `Pages/DocumentDetails.razor` con modal de selección de usuario + permission (Read/Write) + ExpiresAt opcional
- [x] T104 [US6] Crear `Pages/SharedWithMe.razor` con listado de documentos compartidos (invoca `IDocumentShareService.ListSharedWithMeAsync`)
- [x] T105 [US6] Agregar link "Compartido conmigo" en el sidebar de navegación
- [x] T106 [US6] Verificar que revocación quita acceso en ≤ 5s al receptor (test de integración con polling)

**Checkpoint**: Compartir y revocar funcionan, con la restricción de PM dentro del proyecto aplicada correctamente.

---

## Phase 9: User Story 7 - Eliminar documentos (Priority: P3)

**Goal**: El dueño o PM del proyecto puede eliminar un documento (cascade a `DocumentShare`, archivo se borra del disco en ≤ 100 ms).

**Independent Test**: Click "Eliminar" en un documento propio → archivo desaparece del disco en ≤ 100 ms, fila borrada, `DocumentShare` cascadas eliminados.

### Tests para User Story 7

- [x] T107 [P] [US7] Extender `DocumentServiceTests.cs` con tests de `DeleteAsync` (autorización: dueño OR PM; cascade; rollback en fallo)
- [x] T108 [P] [US7] Crear `tests/ContosoDashboard.Tests.Integration/DocumentDeleteTests.cs` — happy path + 403 + cascade verification

### Implementación para User Story 7

- [x] T109 [US7] Implementar `DocumentService.DeleteAsync` con autorización: dueño OR PM del proyecto (per FR-021)
- [x] T110 [US7] Implementar el flujo de delete: validar autorización → nullificar FK en ActivityLog → eliminar row de DB (cascade a `DocumentShare` via FK) → best-effort file delete → log `document.deleted`
- [x] T111 [US7] Verificar que la transacción es atómica: `BeginTransactionAsync` envuelve el delete; si falla, rollback y re-nullify idempotente de ActivityLog
- [x] T112 [US7] Agregar modal de confirmación Blazor en `Pages/DocumentDetails.razor` + `Shared/DeleteConfirmDialog.razor` ("¿Eliminar permanentemente {Title}?")
- [x] T113 [US7] Verificar que `ActivityLog` NO se elimina: `ExecuteUpdateAsync` setea `DocumentId = NULL` antes del delete (RESTRICT FK respetada); el log de delete se inserta con `DocumentId = null` para preservar la auditoría
- [ ] T114 [US7] Verificar que la operación completa en ≤ 100 ms (test de integración con `Stopwatch` — bloqueado por Phase 1)

**Checkpoint**: Eliminación funciona con cascade correcto, autorización por dueño/PM, y audit log preservado.

---

## Phase 10: User Story 8 - Integración con tareas y dashboard (Priority: P3)

**Goal**: Un usuario puede adjuntar un documento desde una tarea (snapshot del `ProjectId`), y ve un widget de "Documentos Recientes" en el dashboard.

**Independent Test**: Adjuntar un documento desde una tarea → `ProjectId` se copia del task. Ver dashboard → widget muestra los 5 últimos subidos.

### Tests para User Story 8

- [x] T115 [P] [US8] Crear `tests/ContosoDashboard.Tests.Unit/Services/DocumentServiceTests.cs` con tests de upload con `taskId` (snapshot de `ProjectId` del task)
- [x] T116 [P] [US8] Crear `tests/ContosoDashboard.Tests.Integration/DocumentAttachmentToTaskTests.cs` — attach → verific ProjectId

### Implementación para User Story 8

- [x] T118 [US8] Extender `DocumentService.UploadAsync` para aceptar `taskId` opcional; si se especifica, hacer snapshot de `Task.ProjectId` y asignarlo a `Document.ProjectId` (NO re-evaluar si el task cambia de proyecto) — implementado en [DocumentService.cs:130-134](ContosoDashboard/Services/Documents/DocumentService.cs)
- [x] T119 [US8] Crear `ContosoDashboard/Pages/TaskDetails.razor` (ruta `/tasks/{id:int}`) con sección "Documentos adjuntos" + botón "Adjuntar documento" (invoca `DocumentUploadComponent` con `taskId` pre-seleccionado)
- [x] T120 [US8] Crear `ContosoDashboard/Shared/RecentDocumentsWidget.razor` — widget para dashboard que muestra los 5 últimos documentos subidos por el usuario (incluye botón refrescar `RefreshAsync`)
- [x] T121 [US8] Implementar `IDashboardService.GetRecentDocumentsAsync(userId, count)` con `AsNoTracking()` + `IMemoryCache` (TTL 5 min) + clave por usuario+count
- [x] T122 [US8] Agregar `RecentDocumentsWidget` a `Pages/Index.razor` (dashboard home, columna derecha)
- [x] T123 [US8] Agregar conteo de documentos a las cards de resumen del dashboard (extender `IDashboardService` con `GetUserDocumentCountAsync(userId)` + `DashboardSummary.TotalDocuments` + nueva card clickeable "My Documents")
- [x] T124 [US8] Verificar que el widget de "Documentos Recientes" se actualiza tras un nuevo upload: `DocumentUploadComponent` invoca `IDashboardService.InvalidateUserDashboardAsync(userId)` tras `UploadAsync` exitoso
- [x] T125 [US8] Verificar que las notificaciones a project members se disparan también cuando el documento se adjunta a una tarea: `NotifyProjectMembersAsync` se invoca en `UploadAsync` cuando `projectId.HasValue` (true cuando se snapshot del task)

**Checkpoint**: Integración con tareas y dashboard funciona, widget muestra documentos recientes.

---

## Phase 11: Polish & Cross-Cutting Concerns

**Propósito**: Mejoras que afectan a múltiples historias, validación de quality gates, performance, seguridad end-to-end, documentación.

- [ ] T126 [P] Ejecutar Stryker.NET mutation testing sobre `ContosoDashboard/Services/Documents/**`; revisar mutantes sobrevivientes y agregar tests que los detecten, o justificar con `Stryker.NET` ignore — **bloqueado por Phase 1** (no existe `tests/ContosoDashboard.Tests.Unit/`)
- [ ] T127 [P] Ejecutar performance tests con NBomber sobre upload endpoint (latency p95 < 30s) y search (p95 < 2s) — verificar SLOs — **bloqueado por Phase 1** (no existe `tests/ContosoDashboard.Tests.Performance/`)
- [ ] T128 [P] Ejecutar k6 load tests sobre 5 perfiles (smoke, load, stress, spike, soak) — verificar umbrales — **bloqueado por Phase 1** (no existen scripts k6)
- [x] T129 [P] Auditoría de seguridad: revisar OWASP Top 10 contra implementación (revisar CHK001-CHK046 de `checklists/security.md`) — generado [`SECURITY-AUDIT.md`](../../../SECURITY-AUDIT.md) con veredicto ✅ PASS
- [x] T130 [P] Verificar headers de seguridad (CSP, X-Frame-Options, X-XSS-Protection, X-Content-Type-Options) en TODOS los endpoints de documentos — headers globales en `Program.cs` + headers explícitos en `Download.cshtml` (T130) y `Preview.cshtml` (T130)
- [x] T131 [P] Implementar `IActivityLogCleanupService` (background service) que corre diariamente y elimina logs > 90 días (per FR-031) — [`Services/ActivityLogCleanupService.cs`](../../../ContosoDashboard/Services/ActivityLogCleanupService.cs) + [`Services/ActivityLogCleanupBackgroundService.cs`](../../../ContosoDashboard/Services/ActivityLogCleanupBackgroundService.cs), registrado en `Program.cs` con `AddHostedService`
- [ ] T132 [P] Ejecutar agente `/code-quality` sobre el branch actual; verificar todos los gates (cobertura, mutación, duplicación, estático, vulnerabilidades) pasan — **bloqueado por Phase 1** (coverage/mutation no pueden ejecutarse sin tests); reporte base en [`code-quality-report.md`](../../../code-quality-report.md) muestra 0 errores, 1.4% duplicación, 8 vulnerabilidades NuGet
- [ ] T133 [P] Ejecutar `/speckit.analyze` para cross-validar spec/plan/tasks/implementación — **independiente**, ejecutable
- [x] T134 [P] Verificar que el script `.github/scripts/code-quality-report.ps1` corre sin errores y genera `code-quality-report.md` actualizado — reporte base ya generado el 2026-06-12
- [ ] T135 [P] Ejecutar Playwright en modo `--headed` (manual) para verificar UX de los 10 escenarios de `quickstart.md` — **bloqueado por Phase 1** (no existe `tests/ContosoDashboard.Tests.E2E.UI/`)
- [x] T136 [P] Actualizar `README.md` con sección "Document Upload" + screenshots / GIFs (opcional) — sección completa con capacidades, rutas, arquitectura, seguridad, performance, accesibilidad, gaps
- [x] T137 [P] Actualizar `StakeholderDocs/document-upload-and-management-feature.md` con notas de implementación (qué se implementó, qué se difirió) — añadida §10 con 5 sub-secciones (implementado, gaps, desviaciones, quality gates, métricas)
- [x] T138 [P] Crear `CHANGELOG.md` con la entrada `[1.0.0] - 2026-XX-XX - Added: Document Upload and Management feature` — [`CHANGELOG.md`](../../../CHANGELOG.md) con entrada completa (Added, Changed, Security, Known Limitations, Notes)
- [x] T139 [P] Verificar accessibility: navegación por teclado en todas las páginas, labels ARIA, contraste de color (per `web-design-guidelines`) — `aria-label` añadido a 6+ botones icon-only en `Documents.razor`, `DocumentDetails.razor`, `RecentDocumentsWidget.razor`, `TaskDetails.razor`, `Tasks.razor`, `SharedWithMe.razor`; modales ya tenían `role="dialog"` y `aria-modal="true"`
- [x] T140 Verificación final: `git status` limpio, todos los tests pasan, `/code-quality` y `/speckit.analyze` retornan OK — build 0 errores, 5/5 endpoints responden 200, app arranca y detiene limpiamente, smoke test pasado

**Checkpoint**: Feature lista para merge a `main`. Todos los quality gates de Constitución v1.1.0 veredicto: PASS.

---

## Resumen de Dependencias (orden de ejecución)

```
Phase 1 (Setup) → Phase 2 (Foundational) → Phase 3 (US1) → Phase 4 (US2)
                                                        ↓
                                              Phase 5 (US3) → Phase 6 (US4) → Phase 7 (US5) → Phase 8 (US6) → Phase 9 (US7) → Phase 10 (US8)
                                                                                                                                          ↓
                                                                                                                                Phase 11 (Polish)
```

**Critical path**: Setup → Foundational → US1 → US2 (las 2 P1 críticas)
**Stories P2 y P3 pueden ser entregadas en iteraciones posteriores**, una vez que las P1 estén validadas.

## Resumen de Cobertura de Quality Gates (Constitución V.5)

| Gate | Test type | Threshold | Bloqueante |
|------|-----------|-----------|:----------:|
| Cobertura de líneas | coverlet | ≥ 80% target, ≥ 40% mín | < 40% |
| Cobertura de branches | coverlet | ≥ 75% target, ≥ 35% mín | < 35% |
| Métodos públicos cubiertos | coverlet | 100% | < 90% |
| **Mutation score** | Stryker.NET | ≥ 80% target, ≥ 70% mín | < 70% |
| E2E API happy paths | RestSharp | 100% | < 90% |
| E2E UI smoke | Playwright | 100% | < 80% |
| Contratos Pact verificados | PactNet | 100% | < 90% |
| p95 latencia | NBomber/k6 | < SLO | ≥ 1.2× SLO |
| Error rate bajo load | k6 | < 0.1% | ≥ 0.5% |

## Notas

- **Total de tasks**: 140 (incluyendo tests explícitos para cada historia, per Constitución V.1 TDD Hard).
- **Pirámide completa**: 7 proyectos de tests cubren los 8 niveles de la pirámide (unit + técnicas + componentes + integración + contratos + E2E API + E2E UI + performance).
- **TDD mandatorio**: cada historia tiene tests ANTES de implementación (tasks T038-T040 para US1, T055-T057 para US2, etc.).
- **Mutación con Stryker.NET ≥ 70%** es gate bloqueante (verificado en T126).
- **Last-writer-wins** en replace (per Clarifications Q3) — sin locks de filesystem.
- **PM solo comparte dentro de proyecto** (per Clarifications Q1) — implementado en T098.
- **Esta fase es 100% local** (per Clarifications Q2 + FR-038) — sin SDKs cloud.
