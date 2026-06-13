# Changelog

All notable changes to the ContosoDashboard project are documented in this file.

The format is based on [Keep a Changelog](https://keepachangelog.com/en/1.1.0/),
and this project adheres to [Semantic Versioning](https://semver.org/spec/v2.0.0.html).

## [1.0.0] - 2026-06-12 - Added: Document Upload and Management feature

### Added

#### Domain & Persistence
- `Models/Document.cs` — entity `Document` con propiedades (DocumentId, Title, Description, Category, FilePath, FileSize, FileType, Tags, UploadedAt, UploadedByUserId, ProjectId, TaskId, ReplacedAt, AvScanStatus, AvScanAt, OriginalFileName, AvThreatName) + enum `DocumentAvStatus`
- `Models/DocumentShare.cs` — entity `DocumentShare` con propiedades (DocumentShareId, DocumentId, SharedWithUserId, SharedWithRole, Permission, SharedAt, SharedByUserId, ExpiresAt, RevokedAt, RevokedByUserId) + enum `DocumentSharePermission`
- `Models/ActivityLog.cs` — entity `ActivityLog` con propiedades (ActivityLogId, Event, DocumentId, UserId, IpAddress, Metadata, Timestamp) + `ActivityLogEvents` constants
- EF Core migration `20260612180018_InitialDocuments` con las 3 nuevas tablas, índices, FKs (CASCADE en DocumentShare, RESTRICT en ActivityLog)

#### Services
- `Services/Documents/IFileStorageService` + `LocalFileStorageService` — abstracción con implementación local-only (filesystem `AppData/uploads/{userId}/{projectIdOrPersonal}/{guid}.{ext}`), anti-path-traversal
- `Services/Documents/IAntivirusScanner` + `ClamAvScanner` — integración nClam con fail-open en training
- `Services/Documents/IMimeTypeValidator` + `MimeTypeValidator` — whitelist de 16 MIME types + magic bytes
- `Services/Documents/IFilePathBuilder` + `FilePathBuilder` — generación de paths seguros
- `Services/Documents/DocumentConstants` — constantes (6 categorías, 16 MIME, 25 MB, 5 tags, etc.)
- `Services/Documents/IDocumentService` + `DocumentService` — orquestación upload/download/update/replace/delete/search/list con rollback atómico
- `Services/Documents/IDocumentShareService` + `DocumentShareService` — share/revoke/list con 3 reglas de autorización (owner, PM, others)
- `Services/Documents/IActivityLogService` + `ActivityLogService` — logging estructurado JSON
- `Services/IActivityLogCleanupService` + `ActivityLogCleanupService` — cleanup de logs > 90 días
- `Services/ActivityLogCleanupBackgroundService` — `BackgroundService` que ejecuta cleanup cada 24h
- `Services/DashboardService` extendido con `GetRecentDocumentsAsync`, `GetUserDocumentCountAsync`, `InvalidateUserDashboardAsync` + `IMemoryCache` TTL 5 min

#### UI
- `Pages/Documents.razor` — listado de "Mis Documentos" con paginación, ordenamiento, upload
- `Pages/DocumentDetails.razor` — vista detallada con acciones (download, preview, share, edit metadata, replace, delete)
- `Pages/SharedWithMe.razor` — documentos compartidos conmigo
- `Pages/TaskDetails.razor` — vista detallada de tarea con sección "Documentos adjuntos"
- `Pages/Documents/Download.cshtml` — endpoint de descarga con `[Authorize]` + headers de seguridad
- `Pages/Documents/Preview.cshtml` — endpoint de previsualización inline con `Content-Security-Policy: sandbox allow-same-origin`
- `Shared/DocumentUploadComponent.razor` — componente reutilizable con `PreSelectedProjectId` y `PreSelectedTaskId`
- `Shared/ShareDialog.razor` — modal Blazor para compartir con TargetUser + Permission + ExpiresAt
- `Shared/DeleteConfirmDialog.razor` — modal Blazor para confirmación de eliminación
- `Shared/EditMetadataModal.razor` — modal para edición de metadata
- `Shared/DocumentPreviewComponent.razor` — preview inline con `<iframe sandbox="allow-same-origin">`
- `Shared/RecentDocumentsWidget.razor` — widget del dashboard con los 5 últimos documentos
- `Shared/NavMenu.razor` — añadido link "Compartido conmigo"

#### Security
- Headers de seguridad globales en `Program.cs` (CSP, X-Frame-Options DENY, X-Content-Type-Options nosniff, X-XSS-Protection, Referrer-Policy, HSTS)
- Headers explícitos en download (`X-Content-Type-Options`, `Content-Disposition` con RFC 5987)
- Headers explícitos en preview (`X-Content-Type-Options`, `Content-Security-Policy: sandbox allow-same-origin`, `X-Frame-Options: DENY`)
- `[Authorize(Policy = "Employee")]` en todas las páginas de documentos
- Re-validación de acceso en cada llamada a `IDocumentService.GetByIdAsync` / `OpenForDownloadAsync`
- Notificación a project members al subir documento a proyecto (latencia ≤ 5s)
- Notificación al receptor al compartir/revocar
- Soft delete de shares vía `RevokedAt` para preservar auditoría

#### Accessibility
- `aria-label` en todos los botones icon-only (download, view, share, revoke)
- `aria-hidden="true"` en iconos decorativos (`<i class="bi bi-...">`)
- `role="dialog"`, `aria-modal="true"`, `aria-labelledby` en modales (`ShareDialog`, `DeleteConfirmDialog`)
- Navegación por teclado funcional (Blazor nativo + Bootstrap focus management)

### Changed
- `Services/DashboardService` — inyectado `IMemoryCache` para caching de summary/recent-docs/count
- `Pages/Index.razor` — layout de cards ajustado (5 cards: tasks, due, projects, **documents**, notifications) + reemplazo "Recent Notifications" por `<RecentDocumentsWidget />`
- `Pages/Tasks.razor` — botón "Ver" ahora enlaza a `/tasks/{id}` (vista detallada con adjuntos)
- `Models/Document` — añadido `AvThreatName` para tracking de amenazas detectadas

### Security
- Defense in depth de 3 capas: `[Authorize]` + `IDocumentService.UserHasAccessAsync` + `IDocumentShareService` business rules
- `IActivityLogCleanupService` + background service para retención de 90 días (FR-031)
- `ActivityLog.DocumentId` se nulifica antes del delete del doc (RESTRICT FK) para preservar auditoría (T113)

### Known Limitations
- **Test framework pendiente**: Los 7 proyectos de tests de la pirámide (`.Tests.Unit`, `.Tests.Components`, `.Tests.Integration`, `.Tests.Contract`, `.Tests.E2E.Api`, `.Tests.E2E.UI`, `.Tests.Performance`) no han sido creados. Esto bloquea T095, T096, T107, T108, T114, T115, T116, T126, T127, T128, T135.
- **Vulnerabilidades NuGet**: 8 vulnerabilidades conocidas en dependencias (no bloqueante para training).
- **CSP permisivo**: `script-src 'unsafe-inline' 'unsafe-eval'` requerido por Blazor Server (mitigable con nonces en producción).
- **Mock authentication**: Sin passwords reales (per scope de training).

### Notes
- **No breaking changes** (es la primera release de esta feature)
- **Constitution v1.1.0**: Compatible con todos los principios (I, II, III, IV, V) con la salvedad de los tests pendientes
- **Stack canónico respetado**: .NET 8, Blazor Server, EF Core 8, SQL Server LocalDB
