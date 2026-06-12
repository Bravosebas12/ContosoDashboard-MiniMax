# Document Upload and Management Feature - Requirements

## Overview

Contoso Corporation needs to add document upload and management capabilities to the ContosoDashboard application. This feature will enable employees to upload work-related documents, organize them by category and project, and share them with team members.

## Business Need

Currently, Contoso employees store work documents in various locations (local drives, email attachments, shared drives), leading to:

- Difficulty locating important documents when needed
- Security risks from uncontrolled document sharing
- Lack of visibility into which documents are associated with specific projects or tasks

The document upload and management feature addresses these issues by providing a centralized, secure location for work-related documents within the dashboard application that employees already use daily.

## Target Users

All Contoso employees who use the ContosoDashboard application will have access to document management features, with permissions based on their existing roles:

- **Employees**: Upload personal documents and documents for projects they're assigned to
- **Team Leads**: Upload documents and view/manage documents uploaded by their team members
- **Project Managers**: Upload documents and manage all documents associated with their projects
- **Administrators**: Full access to all documents for audit and compliance purposes

## Core Requirements

### 1. Document Upload

**File Selection and Upload**

- Users must be able to select one or more files from their computer to upload
- Supported file types: PDF, Microsoft Office documents (Word, Excel, PowerPoint), text files, and images (JPEG, PNG)
- Maximum file size: 25 MB per file
- Users should see a progress indicator during upload
- System should display success or error messages after upload completes

**Document Metadata**

- When uploading, users must provide:
  - Document title (required)
  - Description (optional)
  - Category selection from predefined list (required): Project Documents, Team Resources, Personal Files, Reports, Presentations, Other
  - Associated project (optional - if the document relates to a specific project)
  - Tags for easier searching (optional - users can add custom tags)
- System should automatically capture:
  - Upload date and time
  - Uploaded by (user name)
  - File size
  - File type (MIME type, e.g., "application/pdf" - field must accommodate 255 characters for Office documents)

**Validation and Security**

- System must scan uploaded files for viruses and malware before storage
- System must reject files that exceed size limits with clear error messages
- System must reject unsupported file types
- Uploaded files must be stored securely with appropriate access controls

**Implementation Notes for Local File Storage**

**Offline Storage Pattern:**
- Store files in a dedicated directory outside `wwwroot` for security (e.g., `AppData/uploads`)
- Generate unique file paths BEFORE database insertion to prevent duplicate key violations
- Recommended pattern: `{userId}/{projectId or "personal"}/{uniqueId}.{extension}` where uniqueId is a GUID
- **Upload sequence: Generate unique path → Save file to disk → Save metadata to database**
- **This prevents orphaned database records if file save fails**
- **This prevents duplicate key errors from empty or non-unique file paths**

**Security Considerations:**
- Files stored outside `wwwroot` require controller endpoints to serve them (enables authorization checks)
- Validate file extensions against whitelist before saving
- Use GUID-based filenames to prevent path traversal attacks
- Never use user-supplied filenames directly in file paths
- Implement authorization checks in download endpoint to prevent unauthorized access

**Azure Migration Design:**
- Create `IFileStorageService` interface with methods: `UploadAsync()`, `DeleteAsync()`, `DownloadAsync()`, `GetUrlAsync()`
- Local implementation (`LocalFileStorageService`) uses `System.IO.File` operations
- Future `AzureBlobStorageService` implementation will use Azure.Storage.Blobs SDK
- Same path pattern works for Azure blob names: `{userId}/{projectId}/{guid}.{ext}`
- Swap implementations via dependency injection configuration
- No changes to business logic, UI, or database schema required for migration

### 2. Document Organization and Browsing

**My Documents View**

- Users must be able to view a list of all documents they have uploaded
- The view should display: document title, category, upload date, file size, associated project
- Users should be able to sort documents by: title, upload date, category, file size
- Users should be able to filter documents by: category, associated project, date range

**Project Documents View**

- When viewing a specific project, users should see all documents associated with that project
- All project team members should be able to view and download project documents
- Project Managers should be able to upload documents to their projects

**Search**

- Users should be able to search for documents by: title, description, tags, uploader name, associated project
- Search should return results within 2 seconds
- Users should only see documents they have permission to access in search results

### 3. Document Access and Management

**Download and Preview**

- Users must be able to download any document they have access to
- For common file types (PDF, images), users should be able to preview documents in the browser without downloading

**Edit Metadata**

- Users who uploaded a document should be able to edit the document metadata (title, description, category, tags)
- Users should be able to replace a document file with an updated version

**Delete Documents**

- Users should be able to delete documents they uploaded
- Project Managers can delete any document in their projects
- Deleted documents should be permanently removed after user confirmation

**Share Documents**

- Document owners should be able to share documents with specific users or teams
- Users who receive shared documents should be notified via in-app notification
- Shared documents should appear in recipients' "Shared with Me" section

### 4. Integration with Existing Features

**Task Integration**

- When viewing a task, users should be able to see and attach related documents
- Users should be able to upload a document directly from a task detail page
- Documents attached to tasks should automatically be associated with the task's project

**Dashboard Integration**

- Add a "Recent Documents" widget to the dashboard home page showing the last 5 documents uploaded by the user
- Add document count to the dashboard summary cards

**Notifications**

- Users should receive notifications when someone shares a document with them
- Users should receive notifications when a new document is added to one of their projects

### 5. Performance Requirements

- Document upload should complete within 30 seconds for files up to 25 MB (on typical network)
- Document list pages should load within 2 seconds for up to 500 documents
- Document search should return results within 2 seconds
- Document preview should load within 3 seconds

### 6. Reporting and Audit

**Activity Tracking**

- System should log all document-related activities: uploads, downloads, deletions, share actions
- Administrators should be able to generate reports showing:
  - Most uploaded document types
  - Most active uploaders
  - Document access patterns

## User Experience Goals

- **Simplicity**: Uploading a document should require no more than 3 clicks
- **Speed**: Common operations (upload, download, search) should feel instant
- **Clarity**: Users should always know what happens to uploaded files
- **Confidence**: Users should trust that their documents are secure and won't be lost

## Success Metrics

The feature will be considered successful if, within 3 months of launch:

- 70% of active dashboard users have uploaded at least one document
- Average time to locate a document is reduced to under 30 seconds
- 90% of uploaded documents are properly categorized
- Zero security incidents related to document access

## Technical Constraints

- Must work **offline without cloud services** for training purposes
- Must use **local filesystem storage** for uploaded documents
- Must implement **interface abstractions** (`IFileStorageService`) for future cloud migration
- Must work within current application architecture (no major rewrites)
- Must comply with existing mock authentication system
- Development timeline: Feature should be production-ready within 8-10 weeks
- **Database: DocumentId must be integer (not GUID) for consistency with existing User/Project keys**
- **Database: Category must store text values (not integer enum) for simplicity**

## Implementation Approach

The document management feature is built using a **layered architecture** that separates concerns and enables future cloud migration:

**Data Layer:**
- Document entity stores metadata (title, category, filename, file path, upload date, uploader)
- DocumentId uses integer keys (consistent with existing User and Project tables)
- Category stores text values ("Project Documents", "Personal Files", etc.) for simplicity
- FileType field accommodates long MIME types (255 characters for Office documents)
- FilePath accommodates GUID-based filenames for security (prevents path traversal attacks)
- DocumentShare entity tracks sharing relationships between users

**Storage Layer:**
- Files stored outside web-accessible directories (security requirement)
- IFileStorageService interface abstracts storage implementation
- LocalFileStorageService for training (uses local filesystem)
- Future: Swap to AzureBlobStorageService for production (no code changes needed)
- File organization: `{userId}/{projectId or "personal"}/{guid}.{extension}`

**Business Logic Layer:**
- DocumentService orchestrates upload workflow:
  1. Validate file (size limit, extension whitelist)
  2. Authorize user (project membership if uploading to project)
  3. Generate unique GUID-based filename
  4. Save file to disk
  5. Create database record with file path
  6. Send notifications to project members
- Authorization checks prevent unauthorized document access (IDOR protection)
- Service layer enforces all security rules before data access

**Presentation Layer:**
- Blazor Server page for document upload and viewing
- File upload uses MemoryStream pattern (prevents disposal issues in Blazor)
- Responsive table displays user's documents with metadata
- Upload modal validates input before submission

This architecture ensures security, maintainability, and cloud-readiness while keeping the training implementation simple and offline-capable.

### Cloud Migration Readiness

While this feature must work offline for training, it should be designed for easy migration to Azure services:

**Offline Implementation Requirements:**
- Store files in local directory structure (e.g., `AppData/uploads/{userId}/{projectId}/{guid}.ext`)
- Implement `LocalFileStorageService : IFileStorageService` using `System.IO` operations
- File paths stored in database should be relative and portable
- No Azure SDK dependencies in training implementation

**Azure Migration Design Pattern:**

```csharp
// Interface abstraction (implement in training version)
public interface IFileStorageService
{
    Task<string> UploadAsync(Stream fileStream, string fileName, string contentType);
    Task DeleteAsync(string filePath);
    Task<Stream> DownloadAsync(string filePath);
    Task<string> GetUrlAsync(string filePath, TimeSpan expiration);
}

// Training: LocalFileStorageService implementation
// Production: AzureBlobStorageService implementation
// Switch via appsettings.json and dependency injection
```

**Migration Benefits:**
- Swap service implementation without changing controllers, pages, or business logic
- Database schema remains unchanged (FilePath column works for both local paths and blob names)
- Configuration-driven deployment (dev = local, production = Azure)
- Students learn industry-standard abstraction patterns

### Blazor-Specific Implementation Requirements

**File Upload Component State Management**

- Use `@key` attribute on `InputFile` component to force re-render after successful upload
- Extract file metadata (name, size, contentType) into local variables BEFORE opening stream
- Copy `IBrowserFile` stream to `MemoryStream` immediately to prevent disposal issues
- Clear `IBrowserFile` reference (set to null) after copying stream to prevent reuse errors
- Example pattern:
  ```csharp
  var fileName = SelectedFile.Name;
  var fileSize = SelectedFile.Size;
  var contentType = SelectedFile.ContentType;
  
  using var memoryStream = new MemoryStream();
  using (var fileStream = SelectedFile.OpenReadStream(maxFileSize))
  {
      await fileStream.CopyToAsync(memoryStream);
  }
  memoryStream.Position = 0;
  
  SelectedFile = null; // Clear reference to prevent reuse
  StateHasChanged();
  ```

**Authentication Claims**

- Ensure Login flow includes ALL required claims: NameIdentifier, Name, Email, Role, Department
- Department claim is required for team-based authorization in document sharing
- Missing claims will cause authorization failures in DocumentService methods

### Database Setup Requirements

**Clean State for Testing:**

- Before testing document upload for the first time, ensure clean database state
- If previous upload attempts failed, drop and recreate database to remove orphaned records:
  ```powershell
  sqllocaldb stop mssqllocaldb
  sqllocaldb delete mssqllocaldb
  # Database will be recreated automatically on next run
  ```
- Orphaned records with empty FilePath values will cause duplicate key violations
- For LocalDB: `dotnet ef database drop --force` also works if EF tools are installed

> **🆕 Contenido generado por el agente** (secciones §7–§9) redactado en español conforme a la política de idioma del proyecto. Las secciones §1–§6 permanecen en su idioma original (inglés) por estar autorizadas por el stakeholder.

---

## 7. Estrategia de Pruebas (Testing Strategy)

> **🆕 Sección añadida — referencia**: Constitución v1.1.0, Principio V (TDD Hard + Pirámide de Pruebas Completa) — **NO NEGOCIABLE**.

Esta sección define la estrategia de pruebas obligatoria para esta feature. **Ningún PR con código de producción sin tests correspondientes es aceptado.** El ciclo **ROJO → VERDE → REFACTOR → REVISIÓN** se aplica a cada historia de usuario, historia técnica o corrección de bug.

### 7.1 Pirámide de Pruebas Completa (8 niveles)

Cada nivel tiene propósito, herramienta, scope y umbral definidos:

| # | Nivel | Propósito | Herramienta .NET | Scope | Umbral mínimo |
|---|-------|-----------|------------------|-------|----------------|
| 1 | **Unit funcionales** | Comportamiento observable del dominio (casos de uso, reglas de negocio) | **xUnit** + **NSubstitute** o **Moq** | Una clase/método aislado, sin I/O real | ≥ 80% cobertura de líneas en `Services/Documents/` |
| 2 | **Unit técnicas** | Edge cases, casos de error, boundaries, mutaciones internas | **xUnit** + **FluentAssertions** | Helpers, extensiones, validadores, mappers de documentos | ≥ 70% en `Utils/Documents/`, `Mappers/Documents/` |
| 3 | **Componentes** | Renderizado e interacción del componente `DocumentUpload.razor` y `DocumentList.razor` | **bUnit** | Un componente `.razor` con sus dependencias inyectadas | ≥ 60% de los componentes de documentos |
| 4 | **Integración** | Contratos entre módulos reales (DB, HTTP, AV scanner) | `WebApplicationFactory<Program>` + **Testcontainers** (SQL Server) | Repositorios EF, endpoints, antivirus mock | ≥ 50% cobertura de ramas en `Data/Documents/` y endpoints |
| 5 | **Contrato** | Compatibilidad API consumidor↔productor (consumer-driven) | **PactNet** + Pact Broker | Endpoints públicos de `/api/documents/**` y DTOs compartidos | 100% de endpoints públicos con pacto verificado |
| 6 | **E2E API** | Flujos completos a través de la API HTTP | **RestSharp** / `HttpClient` con `WebApplicationFactory` | Flujos críticos: upload, list, search, download, share, delete | 100% de los happy paths |
| 7 | **E2E UI** | Flujos completos a través del navegador | **Microsoft Playwright** + bUnit para assertions Blazor | User journeys: subir desde la página, descargar, previsualizar, compartir | Smoke + 1 happy path por feature principal |
| 8 | **Rendimiento** | Latencia, throughput, estabilidad bajo carga | **NBomber** / **k6** / **BenchmarkDotNet** | Ver §7.3 | Ver umbrales §7.3 |

**Cobertura de código** (verificable por el agente `/code-quality`):

- Líneas: **≥ 80%** (target), **≥ 40%** (mínimo absoluto, **bloqueante si < 40%**).
- Branches: **≥ 75%** (target), **≥ 35%** (mínimo absoluto).
- Métodos públicos: 100% **DEBEN** estar cubiertos (mínimo 1 test por método).
- `<ExcludeFromCodeCoverageAttribute>` **PROHIBIDO** salvo justificación aprobada en PR.

### 7.2 Pruebas de Mutación (Mutation Testing)

La cobertura de líneas/branches **NO** es suficiente: un test que ejecuta código sin verificar comportamiento tiene 100% de cobertura y 0% de valor. Las pruebas de mutación validan la **calidad de los tests**.

- **Herramienta**: **Stryker.NET** (mutaciones a nivel de statements, branches, y strings).
- **Alcance**: `Domain/Documents/`, `Services/Documents/`, validadores de extensión de archivo, parsers de MIME.
- **Mutation score mínimo**: **≥ 70%** (target **≥ 80%**, **bloqueante si < 70%**).
- **Frecuencia**: en CI en cada PR; en local antes de pedir review.
- **Mutantes sobrevivientes**: deben justificarse explícitamente (equivalente a `Stryker.NET` ignore) **O** eliminarse mediante un test que sí detecte la mutación.
- **Stubs y mappers excluidos** del scope de mutación (mutaciones equivalentes sin valor).

```powershell
# Comando local
dotnet stryker --project "src/ContosoDashboard.Services.Documents" `
  --threshold-break 70 --threshold-high 80 --threshold-low 60
```

### 7.3 Pirámide de Pruebas de Rendimiento (aplicada a cada nivel)

El rendimiento **NO** se valida solo en producción. Se aplica la **misma pirámide** que a las pruebas funcionales.

| Nivel de pirámide | Tipo de test | Herramienta | Métrica objetivo |
|-------------------|--------------|-------------|------------------|
| **Micro/nano** (unit) | Benchmark de un método aislado (ej. `GuidGenerator`, `FilePathBuilder`, `MimeTypeDetector`) | **BenchmarkDotNet** | ns/op, allocs, GC pressure |
| **Componente** | Latencia de un endpoint individual en aislamiento (sin DB compartida) | **NBomber** scenarios simples, **k6** | p50, p95, p99 por endpoint |
| **Integración** | Throughput de upload con DB real (Testcontainers) y AV mock | **NBomber** + **Testcontainers** | RPS sostenible, error rate < 0.1% |
| **Contrato** | Latencia y SLO por endpoint de `/api/documents/**` | **NBomber** contract suite, OpenAPI-driven | p95 < SLO contractual documentado |
| **E2E sistema** | Carga realista simulando journeys de subida y descarga | **k6** / **JMeter** | Concurrencia objetivo (N usuarios), throughput, TTI p95 |
| **Resiliencia** | Stress (romper límites), spike (picos), soak (carga sostenida) | **k6** stress profile, **NBomber** soak | Sin degradación > 10% en p95 después de 1h; sin memory leaks |

**Perfiles de carga requeridos** (en E2E sistema):

- **Smoke**: 1 usuario virtual, 1 minuto — verifica que el escenario corre sin errores.
- **Load**: N usuarios (target de diseño), 10 minutos — capacidad nominal.
- **Stress**: 2×N usuarios, 5 minutos — punto de quiebre.
- **Spike**: 0 → 5×N usuarios en 30s, mantener 2 min — elasticidad.
- **Soak**: N usuarios, 24h+ — estabilidad y leaks.

**Umbrales de aceptación** (gate de merge y release):

- p95 de endpoints de upload: **< 30s** para archivos de 25 MB.
- p95 de endpoints de list/search: **< 500 ms**.
- Error rate bajo carga nominal: **< 0.1%**.
- Memory growth en soak 24h: **< 10%** sobre baseline.
- CPU saturación bajo load: **< 75%** promedio, **< 90%** p95.

### 7.4 Hot-fixes de seguridad — excepción documentada

Única excepción al ciclo TDD Hard: hot-fixes de seguridad críticos (CVE activo) pueden saltarse el paso ROJO, **DEBEN** incluir tests de regresión en el mismo PR y dejar ticket de seguimiento.

---

## 8. Acceptance Criteria (Criterios de Aceptación)

> **🆕 Sección añadida** — Cada criterio está en formato **Given/When/Then** (Gherkin-like), con condiciones previas, acciones, resultados esperados y umbrales medibles. Cumplen con la constitución v1.1.0 (DoR/DoD verificable).

### AC-1 — Document Upload

**AC-1.1 Selección y subida de archivo**

- **AC-1.1.1**: *Given* un usuario autenticado en `Pages/Documents.razor`, *When* selecciona un archivo PDF de 5 MB y hace clic en "Subir", *Then* el sistema muestra una barra de progreso al 100% en ≤ 10s, y el documento aparece en "Mis Documentos" con título, fecha, tamaño, y tipo MIME `application/pdf`.
- **AC-1.1.2**: *Given* un usuario con rol Employee, *When* selecciona un archivo de extensión `.exe`, *Then* el sistema rechaza el archivo con mensaje "Tipo de archivo no soportado" y NO se crea ningún registro en la base de datos.
- **AC-1.1.3**: *Given* un usuario autenticado, *When* selecciona un archivo de 26 MB, *Then* el sistema rechaza con mensaje "Tamaño máximo permitido: 25 MB" y NO se crea ningún registro.
- **AC-1.1.4**: *Given* un usuario autenticado, *When* selecciona múltiples archivos simultáneamente (5 archivos de 3 MB), *Then* todos se suben en paralelo y se crean 5 documentos con sus respectivos `DocumentId` correlativos.
- **AC-1.1.5**: *Given* el upload en progreso, *When* el usuario pierde conectividad a la red, *Then* el sistema muestra "Error de red — reintentar" y NO se crean registros huérfanos (rollback automático).

**AC-1.2 Metadata del documento**

- **AC-1.2.1**: *Given* un usuario subiendo un archivo, *When* omite el campo "Título" y hace clic en "Subir", *Then* el sistema valida y rechaza con "El título es obligatorio".
- **AC-1.2.2**: *Given* un documento subido, *Then* la base de datos contiene automáticamente: `UploadedAt` (UTC), `UploadedBy` (userId del claim `NameIdentifier`), `FileSize` (bytes), `FileType` (MIME).
- **AC-1.2.3**: *Given* un usuario subiendo, *When* selecciona categoría "Project Documents" y un proyecto de la lista, *Then* el documento se asocia a ese proyecto y aparece en `Pages/Projects/{id}.razor`.
- **AC-1.2.4**: *Given* un usuario añadiendo tags, *When* introduce 6 tags, *Then* el sistema rechaza con "Máximo 5 tags permitidos".
- **AC-1.2.5**: *Given* un documento con `FileType = "application/vnd.openxmlformats-officedocument.wordprocessingml.document"` (longitud ≥ 60 chars), *Then* el campo acepta y persiste correctamente sin truncamiento (columna `NVARCHAR(255)`).

**AC-1.3 Validación y seguridad (antivirus)**

- **AC-1.3.1**: *Given* un archivo limpio, *When* se sube, *Then* el antivirus lo marca como "clean" en ≤ 5s y el documento se persiste.
- **AC-1.3.2**: *Given* un archivo infectado (firma EICAR test), *When* se sube, *Then* el antivirus lo marca como "infected", el sistema rechaza con "Archivo rechazado: amenaza detectada", y NO se persiste archivo en disco.
- **AC-1.3.3**: *Given* el servicio de AV no disponible, *When* se sube un archivo en **training**, *Then* el sistema registra warning en log, permite el upload, y muestra una insignia "⚠️ sin verificación AV" en el documento.
- **AC-1.3.4**: *Given* el servicio de AV no disponible, *When* se sube un archivo en **producción**, *Then* el sistema rechaza con "Servicio de seguridad no disponible — reintente más tarde" (fail-closed).
- **AC-1.3.5**: *Given* un archivo subido, *Then* el path en disco es `{userId}/{projectIdOrPersonal}/{guid}.{ext}`, **nunca** incluye el nombre original del archivo (anti path-traversal).
- **AC-1.3.6**: *Given* un archivo `ContentType` con caracteres maliciosos, *Then* el sistema valida contra whitelist de MIME types antes de persistir.

**AC-1.4 Secuencia de subida (orden de operaciones)**

- **AC-1.4.1**: *Given* un upload, *When* el guardado en disco falla (disco lleno), *Then* NO se crea ningún registro en la base de datos.
- **AC-1.4.2**: *Given* un upload, *When* `SaveChangesAsync` falla (constraint violation), *Then* el archivo en disco se elimina (rollback) y la excepción se propaga al usuario como "Error al guardar — reintente".
- **AC-1.4.3**: *Given* dos uploads concurrentes del mismo usuario, *Then* ambos obtienen GUIDs distintos y se persisten sin colisión.

### AC-2 — Document Organization and Browsing

- **AC-2.1.1**: *Given* un usuario con 30 documentos, *When* accede a "Mis Documentos", *Then* la página carga en ≤ 2s y muestra los 25 más recientes (paginado, page size 25).
- **AC-2.1.2**: *Given* un usuario, *When* ordena por "tamaño" descendente, *Then* la lista se reordena en ≤ 200 ms.
- **AC-2.1.3**: *Given* un usuario, *When* filtra por categoría "Reports" + proyecto "Q4 Planning", *Then* solo aparecen documentos que cumplen ambos filtros.
- **AC-2.2.1**: *Given* un usuario Team Lead, *When* visualiza `Pages/Projects/5.razor`, *Then* aparecen los documentos del proyecto, y "Subir documento" está habilitado.
- **AC-2.2.2**: *Given* un usuario Employee NO asignado al proyecto, *When* intenta acceder a `Pages/Projects/5.razor`, *Then* recibe 403 (forbidden).
- **AC-2.3.1**: *Given* un usuario buscando "presupuesto", *Then* el sistema busca en `Title` + `Description` + `Tags` y devuelve resultados en ≤ 2s.
- **AC-2.3.2**: *Given* un usuario, *When* busca, *Then* solo aparecen documentos a los que tiene acceso (autorización server-side, no client-side).
- **AC-2.3.3**: *Given* 10k documentos en el sistema, *Then* la búsqueda retorna primeros 50 resultados ordenados por relevancia en ≤ 2s p95.

### AC-3 — Document Access and Management

- **AC-3.1.1**: *Given* un usuario con acceso, *When* hace clic en "Descargar", *Then* el archivo se sirve con `Content-Disposition: attachment` y el nombre original (NO el GUID).
- **AC-3.1.2**: *Given* un PDF de 2 MB, *When* el usuario hace clic en "Vista previa", *Then* se renderiza en un `<iframe>` inline en ≤ 3s.
- **AC-3.1.3**: *Given* un archivo `.docx`, *When* el usuario hace clic en "Vista previa", *Then* se muestra un mensaje "Vista previa no disponible para este tipo de archivo" + botón "Descargar".
- **AC-3.2.1**: *Given* un usuario dueño del documento, *When* edita el título y guarda, *Then* el cambio persiste y aparece en la lista en ≤ 500 ms.
- **AC-3.2.2**: *Given* un usuario NO dueño, *When* intenta acceder a `edit/{id}`, *Then* recibe 403.
- **AC-3.2.3**: *Given* un usuario reemplazando un archivo, *When* sube la nueva versión, *Then* el archivo antiguo se elimina del disco y el `FilePath` se actualiza; el `DocumentId` se mantiene.
- **AC-3.3.1**: *Given* un usuario dueño, *When* hace clic en "Eliminar" y confirma, *Then* el archivo se elimina del disco en ≤ 100 ms y la fila de DB se borra (cascade `DocumentShare`).
- **AC-3.3.2**: *Given* un Project Manager, *When* elimina un documento subido por un team member en su proyecto, *Then* la eliminación procede.
- **AC-3.4.1**: *Given* un usuario dueño, *When* comparte con otro usuario + permiso "Read", *Then* se crea un registro `DocumentShare` y aparece en "Compartido conmigo" del receptor en ≤ 5s.
- **AC-3.4.2**: *Given* un documento compartido, *When* el dueño revoca el acceso, *Then* el receptor pierde acceso en ≤ 5s.
- **AC-3.4.3**: *Given* un usuario, *When* recibe un documento compartido, *Then* recibe una notificación in-app vía `INotificationService`.

### AC-4 — Integration

- **AC-4.1.1**: *Given* un usuario en `Pages/Tasks/{id}.razor`, *When* adjunta un documento, *Then* el documento se asocia al `TaskId` y hereda el `ProjectId` del task.
- **AC-4.1.2**: *Given* un usuario, *When* sube un documento desde una tarea, *Then* el `ProjectId` del documento se copia del task en el momento de la asociación (no se re-evalúa si el task cambia de proyecto).
- **AC-4.2.1**: *Given* un usuario autenticado, *When* accede a `Pages/Index.razor`, *Then* el widget "Documentos Recientes" muestra los 5 últimos que subió el usuario (orden por `UploadedAt DESC, DocumentId DESC`).
- **AC-4.2.2**: *Given* un usuario, *Then* las cards de resumen incluyen el conteo total de documentos propios.
- **AC-4.3.1**: *Given* un usuario A comparte un documento con B, *Then* B recibe una notificación in-app en ≤ 5s.
- **AC-4.3.2**: *Given* un documento añadido a un proyecto del usuario, *Then* el usuario recibe notificación.

### AC-5 — Performance

- **AC-5.1.1**: *Given* una red LAN 100 Mbps, *When* un usuario sube un archivo de 25 MB, *Then* el upload completa en ≤ 30s (p95).
- **AC-5.2.1**: *Given* un usuario con 500 documentos, *When* carga "Mis Documentos" (página 1 de 20), *Then* la página renderiza en ≤ 2s (p95).
- **AC-5.3.1**: *Given* 10k documentos en el sistema, *When* un usuario busca por palabra clave, *Then* los resultados se devuelven en ≤ 2s (p95).
- **AC-5.4.1**: *Given* un PDF de 2 MB, *When* el usuario hace clic en "Vista previa", *Then* la vista se renderiza en ≤ 3s (p95).

### AC-6 — Reporting and Audit

- **AC-6.1.1**: *Given* una operación de upload, *Then* se registra en log estructurado: `event="document.uploaded", documentId, userId, fileSize, mimeType, timestamp`.
- **AC-6.1.2**: *Given* un download, *Then* se registra: `event="document.downloaded", documentId, userId, ipAddress, timestamp`.
- **AC-6.1.3**: *Given* un delete, *Then* se registra: `event="document.deleted", documentId, userId, deletedByUserId, timestamp`.
- **AC-6.2.1**: *Given* un Administrator, *When* accede a `/admin/reports/documents/types`, *Then* recibe un CSV con "Top 10 tipos MIME por uploads" en ≤ 5s.

---

## 9. Escaneo de Antivirus y Malware (Requisitos Detallados)

> **🆕 Sección añadida** — Detalla §1 "Validation and Security" alineado a constitución v1.1.0 Principio II (OWASP A03 — Injection, A06 — Vulnerable Components).

### 9.1 Proveedor de AV

- **Training (offline)**: **ClamAV** via **nClam** (.NET client) — open-source, sin dependencias cloud, soporta todos los formatos comunes.
- **Producción (futuro)**: **Microsoft Defender for Cloud** o **ClamAV en contenedor** — decisión se difiere hasta la fase de deployment real (este proyecto es training-only).

### 9.2 Interfaz

```csharp
public interface IAntivirusScanner
{
    Task<ScanResult> ScanAsync(Stream fileStream, string fileName, CancellationToken ct = default);
}

public record ScanResult(
    ScanStatus Status,          // Clean, Infected, Error, Timeout
    string? ThreatName,         // Nombre de la amenaza si aplica
    TimeSpan Duration,          // Tiempo de escaneo
    string? ScannerVersion      // Versión del motor de AV
);
```

### 9.3 Whitelist de tipos MIME (16 tipos)

| Extensión | MIME Type | Tamaño máx. |
|-----------|-----------|-------------:|
| `.pdf` | `application/pdf` | 25 MB |
| `.doc`, `.docx` | `application/msword`, `application/vnd.openxmlformats-officedocument.wordprocessingml.document` | 25 MB |
| `.xls`, `.xlsx` | `application/vnd.ms-excel`, `application/vnd.openxmlformats-officedocument.spreadsheetml.sheet` | 25 MB |
| `.ppt`, `.pptx` | `application/vnd.ms-powerpoint`, `application/vnd.openxmlformats-officedocument.presentationml.presentation` | 25 MB |
| `.txt` | `text/plain` | 5 MB |
| `.jpg`, `.jpeg` | `image/jpeg` | 10 MB |
| `.png` | `image/png` | 10 MB |

Cualquier archivo fuera de esta whitelist es rechazado con `400 Bad Request` y mensaje "Tipo de archivo no soportado".

### 9.4 Comportamiento ante fallo del AV

| Entorno | Comportamiento | Justificación |
|---------|----------------|---------------|
| **Training** | Log warning + permitir upload + insignia "⚠️ sin verificación AV" | Maximizar disponibilidad para entrenamiento |
| **Producción** | Rechazar con `503 Service Unavailable` (fail-closed) | Seguridad no es negociable en producción |

### 9.5 Latencia y timeouts

- **Latencia objetivo**: ≤ 5 segundos por archivo de 25 MB.
- **Timeout**: 30 segundos (después del cual se considera `Timeout`).
- **Métrica observable**: `_antivirus_scan_duration_seconds` (histogram, Prometheus).
- **Alerta**: p95 > 10s en dashboards de producción.

### 9.6 Secuencia de operaciones con AV

```
1. Validar extensión + tamaño (fail-fast, sin AV)
2. Validar MIME type real (magic bytes, no solo Content-Type del cliente)
3. Copiar stream a MemoryStream (Blazor safety)
4. Scan antivirus (síncrono en el flujo de upload)
5. Si clean → generar GUID + escribir a disco + guardar DB
6. Si infected → log + rechazar 422
7. Si error/timeout → comportamiento según entorno (training: allow + flag; prod: 503)
```

---

## Assumptions

- Training environment has local disk storage available
- Most documents will be under 10 MB in size
- Users are familiar with basic file management concepts
- Local filesystem storage is acceptable for training purposes
- Cloud migration to Azure Blob Storage is planned for production deployment
- Users may work offline (no internet connection required for core functionality)

## Out of Scope

The following features are NOT included in this initial release:

- Real-time collaborative editing of documents
- Version history and rollback capabilities
- Advanced document workflows (approval processes, document routing)
- Integration with external systems (SharePoint, OneDrive)
- Mobile app support (initial release is web-only)
- Document templates or document generation features
- Storage quotas and quota management
- Soft delete/trash functionality with recovery

These may be considered for future enhancements based on user feedback and business needs.

## Next Steps

Once approved, these requirements will be used to create detailed specifications using the Spec-Driven Development methodology with GitHub Spec Kit.
