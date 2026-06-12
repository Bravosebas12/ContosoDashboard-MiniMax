# Data Model: Document Upload and Management

**Phase**: 1 (Design)
**Date**: 2026-06-12
**Status**: Complete

## Overview

Tres nuevas entidades se agregan al `ApplicationDbContext` existente en `ContosoDashboard/Data/ApplicationDbContext.cs`:

1. **`Document`** — registro de metadatos de un documento subido.
2. **`DocumentShare`** — permiso de acceso compartido a un documento.
3. **`ActivityLog`** — registro de auditoría de eventos relacionados con documentos.

Las migraciones se generan con `dotnet ef migrations add InitialDocuments` (no `EnsureCreated()` — alineado con Constitución IV).

## Entity-Relationship Diagram

```
┌─────────────┐         ┌──────────────────┐         ┌──────────────┐
│   User      │◄────────│   Document       │────────►│   Project    │
│ (existing)  │ N    1  │                  │ 1   N   │  (existing)  │
└─────────────┘         │  (new)           │         └──────────────┘
       ▲                └──────┬───────────┘                ▲
       │                       │ N                          │
       │                       │ 1                          │
       │                ┌──────▼───────────┐                │
       │                │  DocumentShare   │                │
       │                │      (new)       │                │
       │                └──────────────────┘                │
       │                                                  │
       │                ┌──────────────────┐                │
       └────────────────│   ActivityLog    │────────────────┘
              N     1   │      (new)       │         N   1
                       └──────────────────┘
                              │
                              │ N
                              │ 1
                       ┌──────▼───────────┐
                       │   Document       │
                       │   (new)          │
                       └──────────────────┘
```

## Entity: `Document`

Representa un documento subido al sistema. PK entero por consistencia con `User` y `Project` existentes (per StakeholderDoc §Technical Constraints).

### Properties

| Property | Type | Nullable | Default | Description | Constraints |
|----------|------|----------|---------|-------------|-------------|
| `DocumentId` | `int` | No | IDENTITY | Identificador único | PK, IDENTITY(1,1) |
| `Title` | `string` (`NVARCHAR(200)`) | No | — | Título del documento | 1-200 chars, required |
| `Description` | `string` (`NVARCHAR(2000)`) | Yes | `null` | Descripción opcional | 0-2000 chars |
| `Category` | `string` (`NVARCHAR(50)`) | No | — | Categoría enum-texto | Must be in {Project Documents, Team Resources, Personal Files, Reports, Presentations, Other} |
| `FilePath` | `string` (`NVARCHAR(500)`) | No | — | Path relativo al directorio de uploads | Pattern: `{userId}/{projectIdOrPersonal}/{guid}.{ext}` |
| `FileSize` | `long` | No | — | Tamaño en bytes | > 0; ≤ 26,214,400 (25 MB + overhead) |
| `FileType` | `string` (`NVARCHAR(255)`) | No | — | MIME type | Must be in whitelist of 16 types |
| `Tags` | `string` (`NVARCHAR(500)`) | Yes | `null` | Tags comma-separated | Lowercase, max 5 tags × 50 chars |
| `UploadedAt` | `DateTime` | No | `DateTime.UtcNow` | Timestamp UTC | DB-generated |
| `UploadedByUserId` | `string` (`NVARCHAR(450)`) | No | — | FK al usuario uploader | FK → AspNetUsers.Id, RESTRICT |
| `ProjectId` | `int?` | Yes | `null` | FK al proyecto asociado (opcional) | FK → Projects.ProjectId, RESTRICT |
| `TaskId` | `int?` | Yes | `null` | FK a la tarea asociada (opcional) | FK → Tasks.TaskId (futuro), RESTRICT |
| `ReplacedAt` | `DateTime?` | Yes | `null` | Timestamp del último reemplazo | Set on file replace |
| `AvScanStatus` | `int` (enum) | No | `0` (Clean) | Estado del último scan | 0=Clean, 1=Infected, 2=NotScanned, 3=Error |
| `AvScanAt` | `DateTime?` | Yes | `null` | Timestamp del scan | Set after each scan |
| `OriginalFileName` | `string` (`NVARCHAR(255)`) | No | — | Nombre original del archivo (para descarga) | Solo para display, NO se usa en path físico |

### Indexes

| Index | Columns | Type | Purpose |
|-------|---------|------|---------|
| `PK_Documents` | `DocumentId` | CLUSTERED | Primary key |
| `IX_Documents_UploadedByUserId` | `UploadedByUserId` | NONCLUSTERED | "Mis Documentos" query |
| `IX_Documents_ProjectId` | `ProjectId` | NONCLUSTERED | "Documentos del proyecto" query |
| `IX_Documents_Category` | `Category` | NONCLUSTERED | Filtro por categoría |
| `IX_Documents_UploadedAt` | `UploadedAt DESC, DocumentId DESC` | NONCLUSTERED | Ordenamiento "Recientes" |
| `IX_Documents_FileType` | `FileType` | NONCLUSTERED | Reporte "Top MIME types" |
| `IX_Documents_FilePath` | `FilePath` | UNIQUE, NONCLUSTERED | Anti-duplicado (por si `SaveChangesAsync` reintenta) |
| `IX_Documents_Search` | `Title`, `Description`, `Tags` | FULLTEXT (opcional) | Búsqueda full-text — activar si SLA no se cumple con LIKE |

### Relationships

- `UploadedByUser` → `AspNetUsers` (many-to-one, RESTRICT on delete)
- `Project` → `Project` (many-to-one, optional, RESTRICT on delete)
- `Task` → `Task` (many-to-one, optional, RESTRICT on delete) — futuro
- `DocumentShares` → `DocumentShare[]` (one-to-many, CASCADE on delete)
- `ActivityLogs` → `ActivityLog[]` (one-to-many, RESTRICT on delete — preservar auditoría)

### Validation Rules (DataAnnotations + FluentValidation in service)

- `Title.Length >= 1 && Title.Length <= 200`
- `Category` must be in the predefined set
- `FilePath` must match the regex `^[\w\-]+/([\w\-]+|personal)/[a-f0-9\-]{36}\.\w+$`
- `FileType` must be in the whitelist of 16 MIME types
- `FileSize > 0 && FileSize <= 26214400` (25 MB)
- `Tags` (if present): comma-separated, lowercase, each token ≤ 50 chars, max 5 tokens
- `AvScanStatus` must be 0 (Clean) before the document is considered "ready" for download

### State Transitions

```
   [Created with AvScanStatus=0]
            │
            ▼
   [Active in lists, can be downloaded/shared]
            │
   ┌────────┴────────┐
   │                 │
   ▼                 ▼
[Replaced]      [Deleted]
-ReplacedAt   -Row deleted (cascade)
 updated      -File deleted from disk
-FilePath    
 changed
```

There is no soft-delete state in this release (out of scope per StakeholderDoc §Out of Scope).

## Entity: `DocumentShare`

Representa un permiso de acceso compartido a un documento. Permite al dueño compartir con un usuario específico, o un PM compartir con team members de su proyecto (per Q1 of `## Clarifications`).

### Properties

| Property | Type | Nullable | Default | Description | Constraints |
|----------|------|----------|---------|-------------|-------------|
| `DocumentShareId` | `int` | No | IDENTITY | Identificador único | PK, IDENTITY(1,1) |
| `DocumentId` | `int` | No | — | FK al documento compartido | FK → Documents.DocumentId, CASCADE on delete |
| `SharedWithUserId` | `string` (`NVARCHAR(450)`) | Yes | `null` | FK al usuario receptor (si es por usuario) | FK → AspNetUsers.Id, CASCADE on delete |
| `SharedWithRole` | `string` (`NVARCHAR(50)`) | Yes | `null` | Rol destino (futuro) | Opcional; null por ahora |
| `Permission` | `int` (enum) | No | `0` (Read) | Nivel de permiso | 0=Read, 1=Write |
| `SharedAt` | `DateTime` | No | `DateTime.UtcNow` | Timestamp del share | DB-generated |
| `SharedByUserId` | `string` (`NVARCHAR(450)`) | No | — | FK al usuario que comparte | FK → AspNetUsers.Id, RESTRICT on delete |
| `ExpiresAt` | `DateTime?` | Yes | `null` | Expiración opcional | If set, share is invalid after this date |
| `RevokedAt` | `DateTime?` | Yes | `null` | Timestamp de revocación | If set, share is invalid (soft revocation) |
| `RevokedByUserId` | `string` (`NVARCHAR(450)`) | Yes | `null` | FK al usuario que revocó | FK → AspNetUsers.Id |

### Indexes

| Index | Columns | Type | Purpose |
|-------|---------|------|---------|
| `PK_DocumentShares` | `DocumentShareId` | CLUSTERED | Primary key |
| `IX_DocumentShares_DocumentId` | `DocumentId` | NONCLUSTERED | "Compartido conmigo" query |
| `IX_DocumentShares_SharedWithUserId` | `SharedWithUserId` | NONCLUSTERED | "Compartido conmigo" query |
| `IX_DocumentShares_Active` | `DocumentId, SharedWithUserId, RevokedAt, ExpiresAt` | NONCLUSTERED, FILTERED WHERE `RevokedAt IS NULL AND ExpiresAt IS NULL OR ExpiresAt > GETUTCDATE()` | Active shares lookup |

### Relationships

- `Document` → `Document` (many-to-one, CASCADE on delete — si el doc se elimina, también el share)
- `SharedWithUser` → `AspNetUsers` (many-to-one, CASCADE on delete — si el receptor se elimina, también el share)
- `SharedByUser` → `AspNetUsers` (many-to-one, RESTRICT on delete)

### Validation Rules

- Exactly one of `SharedWithUserId` or `SharedWithRole` must be non-null (CHECK constraint at SQL level).
- `ExpiresAt`, if set, must be in the future.
- `RevokedAt`, if set, must be after `SharedAt`.
- A user cannot share a document with themselves.

### Business Rules (enforced in `DocumentShareService`)

- **Per Q1 of Clarifications**: a Project Manager can only share a document with users in the same project. Only the document's owner (uploader) can share with any user.
- Sharing triggers a notification via `INotificationService` (FR-024, ≤ 5s delivery).
- Revocation is a soft delete (set `RevokedAt`); the row is preserved for audit.

## Entity: `ActivityLog`

Registro de auditoría de eventos relacionados con documentos. Separado de `Notification` (que es para comunicación con el usuario) — aquí el propósito es **trazabilidad** (per FR-029 + FR-031).

### Properties

| Property | Type | Nullable | Default | Description | Constraints |
|----------|------|----------|---------|-------------|-------------|
| `ActivityLogId` | `long` | No | IDENTITY | Identificador único | PK, IDENTITY(1,1) (long porque puede crecer mucho) |
| `Event` | `string` (`NVARCHAR(50)`) | No | — | Tipo de evento | Enum: `document.uploaded`, `document.downloaded`, `document.deleted`, `document.shared`, `document.revoked`, `document.access_denied`, `document.replaced`, `document.scanned` |
| `DocumentId` | `int?` | Yes | `null` | FK al documento (si aplica) | FK → Documents.DocumentId, RESTRICT on delete |
| `UserId` | `string` (`NVARCHAR(450)`) | No | — | FK al usuario que ejecutó la acción | FK → AspNetUsers.Id, RESTRICT |
| `IpAddress` | `string` (`NVARCHAR(45)`) | Yes | `null` | IP del cliente (IPv6 max length) | IPv4 (15) o IPv6 (45) |
| `Metadata` | `string` (`NVARCHAR(2000)`) | Yes | `null` | JSON con datos extra (result, fileSize, mimeType, etc.) | Valid JSON |
| `Timestamp` | `DateTime` | No | `DateTime.UtcNow` | Timestamp UTC | DB-generated, indexed |

### Indexes

| Index | Columns | Type | Purpose |
|-------|---------|------|---------|
| `PK_ActivityLogs` | `ActivityLogId` | CLUSTERED | Primary key |
| `IX_ActivityLogs_Timestamp` | `Timestamp DESC` | NONCLUSTERED | Retention cleanup (delete > 90 days) |
| `IX_ActivityLogs_DocumentId` | `DocumentId` | NONCLUSTERED | "Historial del documento" |
| `IX_ActivityLogs_UserId` | `UserId` | NONCLUSTERED | "Actividad del usuario" |
| `IX_ActivityLogs_Event` | `Event` | NONCLUSTERED | Reporte por tipo de evento |

### Relationships

- `Document` → `Document` (many-to-one, RESTRICT — preservar auditoría incluso si el doc se elimina; el `DocumentId` queda como FK lógico aunque el doc se haya borrado)
- `User` → `AspNetUsers` (many-to-one, RESTRICT)

### Retention Policy

- **Minimum 90 days** (per FR-031, configurable).
- Cleanup: `IActivityLogCleanupService` runs daily, deletes records older than retention period.
- Reports aggregate from this table (FR-030).

### Validation Rules

- `Event` must be in the predefined set.
- `Metadata`, if present, must be valid JSON (enforced at service layer).
- `IpAddress`, if present, must be a valid IPv4 or IPv6 string.

## Modifications to Existing Tables

The following changes to existing `ApplicationDbContext.cs` are needed:

```csharp
// In ApplicationDbContext.cs
public DbSet<Document> Documents { get; set; } = default!;
public DbSet<DocumentShare> DocumentShares { get; set; } = default!;
public DbSet<ActivityLog> ActivityLogs { get; set; } = default!;

protected override void OnModelCreating(ModelBuilder modelBuilder)
{
    base.OnModelCreating(modelBuilder);
    // ... existing configuration ...

    // Document
    modelBuilder.Entity<Document>(entity =>
    {
        entity.HasKey(d => d.DocumentId);
        entity.Property(d => d.Title).HasMaxLength(200).IsRequired();
        entity.Property(d => d.Description).HasMaxLength(2000);
        entity.Property(d => d.Category).HasMaxLength(50).IsRequired();
        entity.Property(d => d.FilePath).HasMaxLength(500).IsRequired();
        entity.Property(d => d.FileType).HasMaxLength(255).IsRequired();
        entity.Property(d => d.Tags).HasMaxLength(500);
        entity.Property(d => d.OriginalFileName).HasMaxLength(255).IsRequired();
        entity.Property(d => d.AvScanStatus).HasConversion<int>();

        entity.HasIndex(d => d.FilePath).IsUnique();
        entity.HasIndex(d => d.UploadedByUserId);
        entity.HasIndex(d => d.ProjectId);
        entity.HasIndex(d => d.Category);
        entity.HasIndex(d => new[] { d.UploadedAt, d.DocumentId });

        entity.HasOne(d => d.UploadedByUser)
              .WithMany()
              .HasForeignKey(d => d.UploadedByUserId)
              .OnDelete(DeleteBehavior.Restrict);

        entity.HasOne(d => d.Project)
              .WithMany()
              .HasForeignKey(d => d.ProjectId)
              .OnDelete(DeleteBehavior.Restrict);
    });

    // DocumentShare
    modelBuilder.Entity<DocumentShare>(entity =>
    {
        entity.HasKey(s => s.DocumentShareId);
        entity.Property(s => s.SharedWithRole).HasMaxLength(50);
        entity.Property(s => s.Permission).HasConversion<int>();

        entity.HasOne(s => s.Document)
              .WithMany(d => d.DocumentShares)
              .HasForeignKey(s => s.DocumentId)
              .OnDelete(DeleteBehavior.Cascade);

        entity.HasOne(s => s.SharedWithUser)
              .WithMany()
              .HasForeignKey(s => s.SharedWithUserId)
              .OnDelete(DeleteBehavior.Cascade);
    });

    // ActivityLog
    modelBuilder.Entity<ActivityLog>(entity =>
    {
        entity.HasKey(l => l.ActivityLogId);
        entity.Property(l => l.Event).HasMaxLength(50).IsRequired();
        entity.Property(l => l.IpAddress).HasMaxLength(45);
        entity.Property(l => l.Metadata).HasMaxLength(2000);

        entity.HasIndex(l => new[] { l.Timestamp }).IsDescending();
        entity.HasIndex(l => l.DocumentId);
        entity.HasIndex(l => l.UserId);
        entity.HasIndex(l => l.Event);

        entity.HasOne(l => l.Document)
              .WithMany()
              .HasForeignKey(l => l.DocumentId)
              .OnDelete(DeleteBehavior.Restrict);
    });
}
```

## Migration

```bash
# From ContosoDashboard/ directory
dotnet ef migrations add InitialDocuments \
  --project ContosoDashboard.csproj \
  --startup-project ContosoDashboard.csproj

# Review the generated migration files in ContosoDashboard/Migrations/

# Apply (training only)
dotnet ef database update
```

## Sample Data (for tests + dev seed)

| DocumentId | Title | Category | UploadedByUserId | ProjectId | AvScanStatus |
|------------|-------|----------|------------------|-----------|---------------|
| 1 | Q4 Planning Notes | Project Documents | `camille.nicole@contoso.com` | 1 | Clean |
| 2 | Brand Guidelines | Reports | `floris.kregel@contoso.com` | null | Clean |
| 3 | Onboarding Checklist | Personal Files | `ni.kang@contoso.com` | null | NotScanned |
| 4 | Security Architecture | Other | `admin@contoso.com` | 1 | Infected (will be rejected) |

## Migration to Production Considerations

> Per spec FR-038, Azure Blob migration is OUT OF SCOPE. This is informational only.

If/when migrating to a cloud backend, the `FilePath` column (currently storing local paths) would store blob names instead. The pattern `{userId}/{projectIdOrPersonal}/{guid}.{ext}` is designed to be portable — it's a logical path, not a filesystem path. The `IFileStorageService` abstraction allows swapping the implementation without changing business logic. No data migration is needed if the pattern is preserved.

## References

- **Spec**: [spec.md](spec.md) §Key Entities, FR-001 to FR-038.
- **Constitution**: v1.1.0 §IV (Estándares de Código), §III (Rendimiento y Escalabilidad).
- **StakeholderDoc**: [§Technical Constraints](../../StakeholderDocs/document-upload-and-management-feature.md) — `DocumentId` integer, `Category` as text, `FileType` 255 chars, `FilePath` GUID-based.
