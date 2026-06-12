# Feature Specification: Document Upload and Management

**Feature Branch**: `001-documents-management`
**Created**: 2026-06-12
**Status**: Draft
**Input**: User description: "Document Upload and Management Feature - Stakeholder requirements for centralized document upload, organization, and sharing within ContosoDashboard"
**Source**: `StakeholderDocs/document-upload-and-management-feature.md` (autorizado por el stakeholder; secciones §1–§6 en inglés, §7–§9 generadas por el agente en español)

> **Resumen del flujo**: Esta especificación traduce los 6 grupos de requisitos del stakeholder (subir, organizar, acceder, integrar, perf, auditoría) en historias de usuario priorizadas, requisitos funcionales testeables, entidades de dominio, y criterios de éxito medibles. La estrategia de pruebas detallada, los 52 criterios de aceptación (Given/When/Then) y los detalles de antivirus están en el StakeholderDoc §7–§9 — esta spec los referencia y los resume, no los duplica.

## User Scenarios & Testing *(mandatory)*

### User Story 1 - Subir documento personal (Priority: P1)

Como **Employee**, quiero subir un documento de trabajo (PDF, Office, imagen) con título, descripción, categoría y tags, para centralizar mis archivos en el dashboard en lugar de guardarlos en discos locales o emails.

**Why this priority**: Es el flujo de valor mínimo viable (MVP). Sin subida no existe la feature. Cubre el caso de uso más común (Employee sube sus propios archivos) y desbloquea todas las historias siguientes (browse, search, share, delete).

**Independent Test**: Se puede probar independientemente creando un usuario con rol `Employee`, autenticándose, navegando a `Documents`, subiendo un PDF de 5 MB, y verificando que aparece en "Mis Documentos" con su metadata. Entrega valor de forma aislada: el usuario ya tiene un repositorio centralizado.

**Acceptance Scenarios**:

1. **Given** un usuario autenticado en `Documents.razor`, **When** selecciona un PDF de 5 MB y completa título + categoría + clic en "Subir", **Then** la barra de progreso llega al 100% en ≤ 10s y el documento aparece en "Mis Documentos" con metadata correcta.
2. **Given** un usuario autenticado, **When** omite el título y hace clic en "Subir", **Then** el sistema rechaza con "El título es obligatorio" y no se crea ningún registro.
3. **Given** un usuario autenticado, **When** selecciona un archivo `.exe` o de 26 MB, **Then** el sistema rechaza con mensaje claro y no se crea ningún registro.
4. **Given** un upload en progreso, **When** el usuario pierde conectividad, **Then** el sistema muestra "Error de red — reintentar" y hace rollback automático (no quedan archivos huérfanos en disco).
5. **Given** un archivo limpio que pasa el antivirus, **Then** se persiste en `{userId}/personal/{guid}.{ext}` con `DocumentId` entero correlativo y metadata completa (`UploadedAt` UTC, `UploadedBy`, `FileSize`, `FileType`).

---

### User Story 2 - Subir documento a un proyecto (Priority: P1)

Como **Project Manager** o **Team Lead**, quiero subir documentos y asociarlos a un proyecto específico, para que todo el equipo del proyecto tenga acceso a materiales compartidos (planes, diseños, reportes).

**Why this priority**: Desbloquea la colaboración en proyectos. Es P1 porque el StakeholderDoc lo marca como caso de uso crítico para la adopción.

**Independent Test**: Se puede probar independientemente con un Project Manager, navegando a un proyecto, subiendo un PDF, y verificando que aparece en `Projects/{id}.razor` y que los team members del proyecto pueden descargarlo.

**Acceptance Scenarios**:

1. **Given** un Project Manager autenticado, **When** accede a `Projects/5.razor` y sube un documento asociándolo al proyecto, **Then** el documento se guarda con `ProjectId = 5` y aparece en la lista de documentos del proyecto.
2. **Given** un Team Lead asignado al proyecto 5, **When** accede a `Projects/5.razor`, **Then** ve el documento y el botón "Descargar" está habilitado.
3. **Given** un Employee NO asignado al proyecto 5, **When** intenta acceder a `Projects/5.razor`, **Then** recibe 403.
4. **Given** un documento subido a un proyecto, **Then** los team members del proyecto reciben una notificación in-app en ≤ 5s.

---

### User Story 3 - Buscar y filtrar documentos (Priority: P2)

Como usuario autenticado, quiero buscar documentos por título, descripción, tags, uploader, o proyecto, y filtrar por categoría o rango de fechas, para encontrar rápidamente lo que necesito sin perder tiempo navegando.

**Why this priority**: Importante para la adopción (StakeholderDoc marca "Average time to locate a document < 30s" como KPI). No es P1 porque sin búsqueda los usuarios aún pueden usar "Mis Documentos" o "Documentos del Proyecto".

**Independent Test**: Se puede probar independientemente con un usuario que tiene 100+ documentos, buscando "presupuesto" y verificando que los resultados se devuelven en ≤ 2s conteniendo solo documentos accesibles.

**Acceptance Scenarios**:

1. **Given** un usuario con 30 documentos, **When** accede a "Mis Documentos", **Then** la página carga en ≤ 2s y muestra los 25 más recientes (paginado, page size 25).
2. **Given** un usuario, **When** busca "presupuesto", **Then** el sistema busca en `Title` + `Description` + `Tags` y devuelve resultados en ≤ 2s p95.
3. **Given** un usuario, **When** ordena por "tamaño" descendente, **Then** la lista se reordena en ≤ 200 ms.
4. **Given** un usuario, **When** filtra por categoría "Reports" + proyecto "Q4 Planning", **Then** solo aparecen documentos que cumplen ambos filtros.
5. **Given** un usuario, **When** busca, **Then** solo aparecen documentos a los que tiene acceso (autorización server-side, no client-side).

---

### User Story 4 - Descargar y previsualizar documentos (Priority: P2)

Como usuario con acceso, quiero descargar cualquier documento al que tenga permiso, y previsualizar PDFs e imágenes en el navegador sin descargarlos, para revisar archivos rápidamente.

**Why this priority**: Complementa la subida. No es P1 porque los usuarios pueden ver/descargar desde la lista con un botón "ver" en otra implementación.

**Independent Test**: Se puede probar independientemente con un PDF de 2 MB, haciendo clic en "Vista previa" y verificando que se renderiza inline en ≤ 3s, y luego en "Descargar" verificando que se baja con el nombre original.

**Acceptance Scenarios**:

1. **Given** un usuario con acceso, **When** hace clic en "Descargar", **Then** el archivo se sirve con `Content-Disposition: attachment` y el nombre original (NO el GUID).
2. **Given** un PDF de 2 MB, **When** el usuario hace clic en "Vista previa", **Then** se renderiza en un `<iframe>` inline en ≤ 3s.
3. **Given** un archivo `.docx`, **When** el usuario hace clic en "Vista previa", **Then** se muestra "Vista previa no disponible para este tipo de archivo" + botón "Descargar".

---

### User Story 5 - Editar metadata y reemplazar archivo (Priority: P3)

Como dueño de un documento, quiero editar su metadata (título, descripción, categoría, tags) y reemplazar el archivo por una versión actualizada, para mantener la información al día.

**Why this priority**: Útil pero no crítico para MVP. La subida inicial cubre la mayoría de los casos.

**Independent Test**: Se puede probar independientemente con un documento del usuario, editando el título y verificando que persiste en ≤ 500 ms; luego reemplazando el archivo y verificando que el GUID del filename cambia pero el `DocumentId` se mantiene.

**Acceptance Scenarios**:

1. **Given** un usuario dueño del documento, **When** edita el título y guarda, **Then** el cambio persiste y aparece en la lista en ≤ 500 ms.
2. **Given** un usuario NO dueño, **When** intenta acceder a `edit/{id}`, **Then** recibe 403.
3. **Given** un usuario reemplazando un archivo, **When** sube la nueva versión, **Then** el archivo antiguo se elimina del disco y el `FilePath` se actualiza; el `DocumentId` se mantiene.

---

### User Story 6 - Compartir documentos (Priority: P3)

Como dueño de un documento, quiero compartirlo con usuarios específicos o equipos, y el receptor debe ver el documento en "Compartido conmigo" con notificación, para colaborar sin necesidad de descargar y reenviar.

**Why this priority**: Importante para colaboración pero no bloquea el uso individual.

**Independent Test**: Se puede probar independientemente con dos usuarios (A y B), A comparte un documento con B, y B lo ve en "Compartido conmigo" en ≤ 5s con notificación in-app.

**Acceptance Scenarios**:

1. **Given** un usuario dueño, **When** comparte con otro usuario + permiso "Read", **Then** se crea un registro `DocumentShare` y aparece en "Compartido conmigo" del receptor en ≤ 5s.
2. **Given** un documento compartido, **When** el dueño revoca el acceso, **Then** el receptor pierde acceso en ≤ 5s.
3. **Given** un usuario, **When** recibe un documento compartido, **Then** recibe una notificación in-app.

---

### User Story 7 - Eliminar documentos (Priority: P3)

Como dueño o Project Manager, quiero eliminar documentos propios o de mi proyecto, para mantener el sistema limpio y cumplir con retención de datos.

**Why this priority**: Necesario para mantenimiento pero no bloquea adopción inicial.

**Independent Test**: Se puede probar independientemente eliminando un documento y verificando que el archivo desaparece del disco en ≤ 100 ms y la fila se borra (cascade `DocumentShare`).

**Acceptance Scenarios**:

1. **Given** un usuario dueño, **When** hace clic en "Eliminar" y confirma, **Then** el archivo se elimina del disco en ≤ 100 ms y la fila de DB se borra (cascade `DocumentShare`).
2. **Given** un Project Manager, **When** elimina un documento subido por un team member en su proyecto, **Then** la eliminación procede.
3. **Given** un usuario NO dueño ni Project Manager, **When** intenta eliminar un documento de otro, **Then** recibe 403.

---

### User Story 8 - Integración con tareas y dashboard (Priority: P3)

Como usuario, quiero ver y adjuntar documentos desde una tarea, y ver un widget de "Documentos Recientes" en el dashboard, para acceder a documentos relevantes desde los puntos de entrada naturales del flujo de trabajo.

**Why this priority**: Mejora UX pero no es bloqueante. Se puede entregar en iteración posterior.

**Independent Test**: Se puede probar independientemente en una tarea específica adjuntando un documento, y verificando que el widget del dashboard muestra los 5 últimos subidos por el usuario.

**Acceptance Scenarios**:

1. **Given** un usuario en `Tasks/{id}.razor`, **When** adjunta un documento, **Then** el documento se asocia al `TaskId` y hereda el `ProjectId` del task (snapshot, no se re-evalúa).
2. **Given** un usuario autenticado, **When** accede al dashboard, **Then** el widget "Documentos Recientes" muestra los 5 últimos que subió (orden por `UploadedAt DESC, DocumentId DESC`).
3. **Given** un usuario, **Then** las cards de resumen incluyen el conteo total de documentos propios.

---

### Edge Cases

- **Disco lleno** durante upload: el sistema rechaza con mensaje claro y NO se crea registro en DB.
- **Fallo del antivirus** en training: log warning + permitir upload + insignia "⚠️ sin verificación AV". En producción: rechazar con 503 (fail-closed).
- **Usuario pierde sesión** durante upload: el sistema limpia el archivo temporal creado y notifica al usuario.
- **Dos uploads concurrentes** del mismo usuario: ambos obtienen GUIDs distintos y se persisten sin colisión.
- **Reemplazo de archivo** mientras otro usuario lo descarga: la descarga en curso completa con el archivo antiguo; la siguiente descarga obtiene el nuevo.
- **Borrado de documento con shares activos**: cascade delete en `DocumentShare` + notificación a usuarios afectados.
- **Búsqueda sin resultados**: mostrar mensaje "No se encontraron documentos" + sugerencia de ajustar filtros.
- **Tag con caracteres especiales**: validar longitud y charset; rechazar con mensaje claro.
- **Upload de archivo de 0 bytes**: rechazar como tamaño inválido.
- **ProjectId cambia después de asociar documento**: el documento conserva el ProjectId original (snapshot behavior).

---

## Requirements *(mandatory)*

### Functional Requirements

#### Subir y validar

- **FR-001**: System MUST permitir a usuarios autenticados subir uno o más archivos simultáneamente desde `Documents.razor` o `Projects/{id}.razor`.
- **FR-002**: System MUST limitar el tamaño máximo de archivo a 25 MB y rechazar tamaños superiores con mensaje claro.
- **FR-003**: System MUST validar extensiones y MIME types contra una whitelist de 16 tipos (PDF, Office, txt, JPEG, PNG) y rechazar el resto con `400 Bad Request`.
- **FR-004**: System MUST escanear todos los archivos subidos con un antivirus antes de persistir (ClamAV via nClam en training; decisión abierta para producción).
- **FR-005**: System MUST rechazar archivos infectados con mensaje "Archivo rechazado: amenaza detectada" y NO persistir nada en disco.
- **FR-006**: System MUST persistir archivos físicamente fuera de `wwwroot` siguiendo el patrón `{userId}/{projectIdOrPersonal}/{guid}.{ext}` (anti path-traversal).
- **FR-007**: System MUST generar un GUID único por archivo, separado del `DocumentId` entero del registro en DB.
- **FR-008**: System MUST implementar rollback automático: si `SaveChangesAsync` falla, el archivo en disco se elimina.
- **FR-009**: System MUST permitir seleccionar un proyecto asociado al documento (opcional) desde una lista filtrada por permisos del usuario.
- **FR-010**: System MUST capturar automáticamente `UploadedAt` (UTC), `UploadedBy` (userId del claim `NameIdentifier`), `FileSize` (bytes), y `FileType` (MIME, hasta 255 chars).
- **FR-011**: System MUST permitir añadir hasta 5 tags por documento (lowercase, max 50 chars cada uno).
- **FR-012**: System MUST soportar reemplazo de archivo manteniendo el `DocumentId` y eliminando el archivo antiguo del disco.

#### Organizar y navegar

- **FR-013**: System MUST mostrar al usuario una lista de "Mis Documentos" paginada (page size 25) en `Documents.razor`, ordenable por título, fecha, categoría o tamaño.
- **FR-014**: System MUST permitir filtrar documentos por categoría, proyecto asociado, y rango de fechas.
- **FR-015**: System MUST mostrar los documentos de un proyecto en `Projects/{id}.razor` cuando el usuario es miembro del proyecto o Project Manager.
- **FR-016**: System MUST implementar búsqueda full-text sobre `Title` + `Description` + `Tags`, retornando resultados en ≤ 2s p95.
- **FR-017**: System MUST limitar la búsqueda a documentos que el usuario tiene permiso de ver (autorización server-side, no client-side).

#### Acceder y gestionar

- **FR-018**: System MUST permitir descargar cualquier documento al que el usuario tenga acceso, sirviéndolo con el nombre original del archivo (NO el GUID).
- **FR-019**: System MUST permitir previsualizar inline PDFs e imágenes (JPEG, PNG) en un visor embebido, para archivos ≤ 10 MB.
- **FR-020**: System MUST permitir al dueño del documento editar metadata (título, descripción, categoría, tags) y persistir cambios en ≤ 500 ms.
- **FR-021**: System MUST permitir al Project Manager eliminar cualquier documento de su proyecto.
- **FR-022**: System MUST eliminar permanentemente el archivo del disco en ≤ 100 ms tras la confirmación del usuario.
- **FR-023**: System MUST permitir al dueño compartir un documento con un usuario específico o un rol/equipo, con un `DocumentShare` que incluye `Permission` (Read/Write), `ExpiresAt?` opcional, y `RevokedAt?` para revocación.
- **FR-024**: System MUST notificar al receptor en ≤ 5s vía `INotificationService` cuando un documento es compartido o revocado.
- **FR-025**: System MUST mostrar al usuario los documentos compartidos con él en una sección "Compartido conmigo".

#### Integración

- **FR-026**: System MUST permitir adjuntar un documento desde `Tasks/{id}.razor`, copiando el `ProjectId` del task como snapshot (no se re-evalúa si el task cambia de proyecto).
- **FR-027**: System MUST mostrar un widget "Documentos Recientes" en el dashboard con los 5 últimos documentos subidos por el usuario.
- **FR-028**: System MUST incluir el conteo total de documentos propios del usuario en las cards de resumen del dashboard.

#### Auditoría y reportes

- **FR-029**: System MUST registrar en log estructurado todos los eventos de documentos: `upload`, `download`, `delete`, `share`, `revoke`, con `documentId`, `userId`, `ipAddress`, `timestamp`, y `result`.
- **FR-030**: System MUST exponer endpoint `/admin/reports/documents/{type}` que devuelve CSV con los top 10 registros por categoría solicitada (tipos MIME, uploaders, patrones de acceso).
- **FR-031**: System MUST retener logs de auditoría por 90 días mínimo (configurable).

#### Permisos y seguridad

- **FR-032**: System MUST aplicar `[Authorize]` en todas las páginas Blazor relacionadas con documentos, con políticas por rol (Employee, TeamLead, ProjectManager, Administrator).
- **FR-033**: System MUST revalidar ownership en el `DocumentService` antes de cada operación (defense in depth): el servicio verifica que el usuario tiene acceso al documento antes de devolver datos.
- **FR-034**: System MUST registrar en `ActivityLog` cualquier intento de acceso denegado (403) a un documento, con `userId`, `documentId`, y razón.
- **FR-035**: System MUST [NEEDS CLARIFICATION: comportamiento de sharing entre Project Managers de distintos proyectos — ¿un PM puede compartir un documento de su proyecto con usuarios fuera del proyecto?].

#### Almacenamiento

- **FR-036**: System MUST abstraer el almacenamiento mediante la interfaz `IFileStorageService` con métodos `UploadAsync`, `DownloadAsync`, `DeleteAsync`, `GetUrlAsync`.
- **FR-037**: System MUST implementar `LocalFileStorageService` (training) usando `System.IO.File`, con paths relativos portables.
- **FR-038**: System MUST [NEEDS CLARIFICATION: en qué fase se introduce `AzureBlobStorageService` — si en esta release o solo como referencia arquitectónica para migración futura].

### Key Entities

- **Document**: Representa un documento subido. Atributos: `DocumentId` (int, PK), `Title` (string, 1-200), `Description` (string, 0-2000, nullable), `Category` (string enum-texto: "Project Documents", "Team Resources", "Personal Files", "Reports", "Presentations", "Other"), `FilePath` (string, relativo: `{userId}/{projectOrPersonal}/{guid}.{ext}`), `FileSize` (long, bytes), `FileType` (string, MIME, hasta 255 chars), `Tags` (string, comma-separated, max 5), `UploadedAt` (DateTime, UTC), `UploadedByUserId` (FK a User), `ProjectId` (FK a Project, nullable), `TaskId` (FK a Task, nullable), `ReplacedAt` (DateTime, nullable, snapshot de último reemplazo), `AvScanStatus` (enum: `Clean`, `Infected`, `NotScanned`, `Error`).

- **DocumentShare**: Representa un permiso de acceso compartido. Atributos: `DocumentShareId` (int, PK), `DocumentId` (FK a Document), `SharedWithUserId` (FK a User, nullable), `SharedWithRole` (string, nullable), `Permission` (enum: `Read`, `Write`), `SharedAt` (DateTime), `SharedByUserId` (FK a User), `ExpiresAt` (DateTime, nullable), `RevokedAt` (DateTime, nullable).

- **ActivityLog** (trazabilidad, ya existe `Notification` similar): Registra eventos de documentos. Atributos: `ActivityLogId` (int, PK), `Event` (string: `document.uploaded`, `document.downloaded`, `document.deleted`, `document.shared`, `document.revoked`, `document.access_denied`), `DocumentId` (FK), `UserId` (FK), `IpAddress` (string, nullable), `Metadata` (string JSON, nullable), `Timestamp` (DateTime, UTC).

- **Tag** (opcional, normalización): Si se prefiere tabla separada en lugar de columna `Tags`. Atributos: `TagId` (int, PK), `DocumentId` (FK), `Name` (string). **Decisión**: columna simple en training; normalizar si la cardinalidad crece.

- **Relaciones**:
  - `User 1—N Document` (UploadedBy)
  - `Project 1—N Document` (opcional)
  - `Task 1—N Document` (opcional)
  - `Document 1—N DocumentShare`
  - `Document 1—N ActivityLog`

---

## Success Criteria *(mandatory)*

### Measurable Outcomes

#### Adopción (negocio, 3 meses post-lanzamiento)

- **SC-001**: Al menos 70% de usuarios activos del dashboard han subido al menos un documento dentro de 3 meses del lanzamiento.
- **SC-002**: Tiempo promedio para localizar un documento es ≤ 30 segundos (medido vía instrumentación o UX study).
- **SC-003**: Al menos 90% de documentos subidos están categorizados correctamente (no usan "Other" como default).
- **SC-004**: Cero incidentes de seguridad relacionados con acceso a documentos en los primeros 6 meses.

#### Funcionales (UX)

- **SC-005**: Un usuario puede subir un documento con no más de 3 clics desde el dashboard (medido por test E2E UI con Playwright).
- **SC-006**: Las operaciones comunes (subir, descargar, buscar) se sienten instantáneas: p95 < 500 ms para list/search, p95 < 30s para upload de 25 MB en LAN.

#### Técnicas (rendimiento y calidad)

- **SC-007**: Búsqueda retorna resultados en ≤ 2 segundos para 10,000 documentos por usuario (medido con k6/NBomber).
- **SC-008**: Lista de documentos carga en ≤ 2 segundos para 500 documentos por usuario.
- **SC-009**: Vista previa de PDF de 2 MB carga en ≤ 3 segundos.
- **SC-010**: 100% de endpoints públicos cubiertos por contratos Pact verificados.
- **SC-011**: Mutation score ≥ 70% (gate bloqueante) en `Domain/Documents/` y `Services/Documents/`.
- **SC-012**: Cobertura de líneas ≥ 80% en código de producción de documentos; ≥ 40% mínimo absoluto (bloqueante).

#### Seguridad (compliance)

- **SC-013**: 100% de archivos subidos pasan por el antivirus antes de persistir (en producción).
- **SC-014**: 100% de accesos denegados (403) se registran en `ActivityLog`.
- **SC-015**: Cero vulnerabilidades NuGet de severidad High/Critical en el código de la feature (verificable por `dotnet list package --vulnerable`).

---

## Notas sobre la estrategia de pruebas

> **Detalle completo**: ver `StakeholderDocs/document-upload-and-management-feature.md` §7 (Estrategia de Pruebas) y §8 (Acceptance Criteria). Esta spec resume y referencia, no duplica.

- **Pirámide de 8 niveles**: unit funcionales + técnicas (xUnit/NSubstitute/FluentAssertions), componentes (bUnit), integración (Testcontainers SQL Server), contrato (PactNet), E2E API (RestSharp + WAF), E2E UI (Playwright), rendimiento (NBomber/k6/BenchmarkDotNet).
- **Mutación con Stryker.NET** ≥ 70% (bloqueante), alineado a constitución v1.1.0 Principio V.3.
- **Pirámide de rendimiento** con 5 perfiles: smoke, load, stress, spike, soak.
- **52 criterios AC** detallados en StakeholderDoc §8, en formato Given/When/Then.

---

## Out of Scope (no se implementa en esta release)

- Edición colaborativa en tiempo real.
- Historial de versiones y rollback.
- Workflows avanzados (aprobaciones, routing).
- Integración con sistemas externos (SharePoint, OneDrive).
- App móvil.
- Templates de documentos.
- Cuotas de almacenamiento.
- Soft delete con recuperación.
- Cloud storage real (Azure Blob) — la arquitectura está diseñada para soportarlo, pero la implementación queda para release futura.

---

## Referencias

- **StakeholderDoc fuente**: `StakeholderDocs/document-upload-and-management-feature.md` (572 líneas, 6 secciones core + 3 secciones de testing/AV).
- **Constitución**: `.specify/memory/constitution.md` v1.1.0 (Principios I–V, incluyendo TDD Hard + Pirámide de 8 niveles).
- **Análisis de gaps previo**: 40 hallazgos en análisis `speckit.analyze` del 2026-06-12; los 3 críticos (A1 Testing, A2 AV, A3 ACs) fueron remediados en el StakeholderDoc.
- **Agente de calidad**: `/code-quality` (en `.github/agents/code-quality.agent.md`) ejecuta los gates automatizables.
- **Skill CRAP**: `.agents/skills/crap-analysis/` (de `aaronontheweb/dotnet-skills`, 1K⭐, 305 installs) para análisis de cobertura + complejidad.
- **Skill OWASP**: `sigat-security-owasp` (en `~/.agents/skills/`) para los requisitos de seguridad.
