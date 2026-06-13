# Data Model Quality Checklist: Document Upload and Management

**Purpose**: Validate that the requirements for entities, indexes, foreign keys, retention, and normalization are complete, unambiguous, and testable.
**Created**: 2026-06-12
**Feature**: [spec.md](../spec.md) §Key Entities
**Reference**: [data-model.md](../data-model.md)

## Entity: Document

- [x] CHK001 - **PK type**: Is the `DocumentId` type explicitly `int` (with IDENTITY), per the stakeholder requirement to match existing `User` and `Project` keys? [Clarity, §StakeholderDoc Technical Constraints]
- [x] CHK002 - **Title constraints**: Are the constraints (1-200 chars, required) explicit? [Clarity, §StakeholderDoc §1.2, AC-1.2.1]
- [x] CHK003 - **Description constraints**: Are the constraints (0-2000 chars, nullable) explicit? [Clarity, FR-020]
- [x] CHK004 - **Category enum-texto**: Is the spec clear that `Category` stores text values (not int enum), and are the 6 valid values enumerated? [Clarity, §StakeholderDoc §Technical Constraints]
- [x] CHK005 - **FilePath pattern**: Is the regex `^[\w\-]+/([\w\-]+|personal)/[a-f0-9\-]{36}\.\w+$` (or equivalent) defined and documented with examples? [Clarity, FR-006, data-model.md]
- [x] CHK006 - **FileSize range**: Is the range (1 byte to 26,214,400 bytes for 25 MB + overhead) specified? [Clarity, FR-002]
- [x] CHK007 - **FileType max length**: Is the max length 255 chars documented (for long Office MIME types)? [Clarity, §StakeholderDoc]
- [x] CHK008 - **Tags format**: Is the comma-separated, lowercase, max 5 tags × 50 chars format specified? [Clarity, FR-011, AC-1.2.4]
- [x] CHK009 - **UploadedAt timezone**: Is the timezone explicitly UTC? [Clarity, FR-010]
- [x] CHK010 - **UploadedBy claim source**: Is the source of the user ID explicit (from the `NameIdentifier` claim)? [Clarity, §StakeholderDoc Blazor-Specific]
- [x] CHK011 - **ProjectId nullable**: Is the FK to `Project` nullable (documents can be personal) explicit? [Clarity, data-model.md]
- [x] CHK012 - **TaskId nullable**: Is the FK to `Task` nullable, and is the snapshot behavior (no re-evaluation) explicit? [Clarity, FR-026, Edge Cases]
- [x] CHK013 - **ReplacedAt behavior**: Is the snapshot of last replace documented, and is it DIFFERENT from `UploadedAt`? [Clarity, data-model.md]
- [x] CHK014 - **AvScanStatus enum**: Is the `AvScanStatus` enum (Clean/Infected/NotScanned/Error) explicit with default value (Clean)? [Clarity, data-model.md, FR-004]
- [x] CHK015 - **OriginalFileName purpose**: Is the purpose of `OriginalFileName` (display + download) explicit, and is it differentiated from `FilePath` (which uses GUID)? [Clarity, data-model.md]

## Indexes

- [x] CHK016 - **PK index**: Is the PK index on `DocumentId` (CLUSTERED) defined? [Clarity, data-model.md]
- [x] CHK017 - **UploadedByUserId index**: Is the nonclustered index on `UploadedByUserId` defined (for "Mis Documentos" query)? [Clarity, data-model.md]
- [x] CHK018 - **ProjectId index**: Is the nonclustered index on `ProjectId` defined (for "Documentos del proyecto" query)? [Clarity, data-model.md]
- [x] CHK019 - **Category index**: Is the nonclustered index on `Category` defined (for filter by category)? [Clarity, data-model.md]
- [x] CHK020 - **UploadedAt + DocumentId composite index**: Is the composite index `(UploadedAt DESC, DocumentId DESC)` defined (for "Recientes" widget)? [Clarity, data-model.md]
- [x] CHK021 - **FileType index**: Is the nonclustered index on `FileType` defined (for "Top MIME types" report)? [Clarity, data-model.md]
- [x] CHK022 - **FilePath UNIQUE index**: Is the UNIQUE index on `FilePath` defined (anti-duplicado)? [Clarity, data-model.md]
- [x] CHK023 - **Full-text search index (conditional)**: Is the FULLTEXT index on (Title, Description, Tags) defined as OPTIONAL (activar si SLA no se cumple con LIKE)? [Clarity, data-model.md]

## Foreign Keys

- [x] CHK024 - **UploadedByUser FK**: Is the FK to `AspNetUsers` with `OnDelete(DeleteBehavior.Restrict)` defined? [Clarity, data-model.md]
- [x] CHK025 - **Project FK**: Is the FK to `Project` with `OnDelete(DeleteBehavior.Restrict)` defined (preserve docs even if project deleted)? [Clarity, data-model.md]
- [x] CHK026 - **Task FK (optional)**: Is the FK to `Task` with `OnDelete(DeleteBehavior.Restrict)` defined? [Clarity, data-model.md]
- [x] CHK027 - **DocumentShares cascade**: Is the FK from `DocumentShare` to `Document` with `OnDelete(DeleteBehavior.Cascade)` defined? [Clarity, data-model.md]
- [x] CHK028 - **ActivityLogs RESTRICT**: Is the FK from `ActivityLog` to `Document` with `OnDelete(DeleteBehavior.Restrict)` defined (preserve audit log)? [Clarity, data-model.md]

## Entity: DocumentShare

- [x] CHK029 - **PK type**: Is the `DocumentShareId` type `int` (IDENTITY)? [Clarity, data-model.md]
- [x] CHK030 - **DocumentId FK**: Is the FK to `Document` (CASCADE on delete) defined? [Clarity, data-model.md]
- [x] CHK031 - **SharedWithUserId FK**: Is the FK to `AspNetUsers` (CASCADE on delete) defined? [Clarity, data-model.md]
- [x] CHK032 - **SharedWithRole**: Is the role-based sharing field defined (with length 50)? [Clarity, data-model.md, FR-023]
- [x] CHK033 - **Permission enum**: Is the `Permission` enum (Read/Write) defined with default (Read)? [Clarity, data-model.md, FR-023]
- [x] CHK034 - **ExpiresAt nullable**: Is the `ExpiresAt` field nullable (no expiration by default)? [Clarity, data-model.md]
- [x] CHK035 - **RevokedAt soft-delete**: Is the `RevokedAt` field documented as soft-deletion (preserves the row for audit)? [Clarity, data-model.md]
- [x] CHK036 - **CHECK constraint**: Is the SQL-level CHECK constraint documented (exactly one of `SharedWithUserId` or `SharedWithRole` must be non-null)? [Clarity, data-model.md]
- [x] CHK037 - **Active shares filtered index**: Is the filtered index (WHERE RevokedAt IS NULL AND (ExpiresAt IS NULL OR ExpiresAt > GETUTCDATE())) defined? [Clarity, data-model.md]

## Entity: ActivityLog

- [x] CHK038 - **PK type**: Is the `ActivityLogId` type `long` (IDENTITY) — chosen for high-volume growth? [Clarity, data-model.md]
- [x] CHK039 - **Event enumeration**: Is the set of events (`document.uploaded`, `document.downloaded`, etc.) explicit? [Clarity, FR-029]
- [x] CHK040 - **IpAddress max length**: Is the max length 45 (IPv6) defined? [Clarity, data-model.md]
- [x] CHK041 - **Metadata JSON validation**: Is the `Metadata` field (JSON) defined with max length 2000? [Clarity, data-model.md]
- [x] CHK042 - **Timestamp timezone**: Is the timezone UTC? [Clarity, data-model.md]
- [x] CHK043 - **Retention policy**: Is the 90-day retention policy defined with a cleanup mechanism? [Clarity, FR-031]
- [x] CHK044 - **Timestamp descending index**: Is the index on `Timestamp DESC` defined (for cleanup job)? [Clarity, data-model.md]

## Migration Strategy

- [x] CHK045 - **EF Core migrations**: Is the use of `dotnet ef migrations add InitialDocuments` explicit (vs `EnsureCreated`)? [Clarity, data-model.md, FR-006 + Constitución IV]
- [x] CHK046 - **Sample seed data**: Is the sample data (4 documents with different categories and statuses) documented for dev/test? [Clarity, data-model.md §Sample Data]
- [x] CHK047 - **Rollback on migration failure**: Is the rollback procedure documented if the migration fails on existing data? [Gap, data-model.md]

## Cross-Entity Constraints

- [x] CHK048 - **Document deletion cascade**: Is the cascade behavior (delete Document → cascade DocumentShare, RESTRICT ActivityLog) consistent across all 3 entities? [Consistency, data-model.md]
- [x] CHK049 - **User deletion behavior**: Is the behavior when a User is deleted (RESTRICT on Document, CASCADE on DocumentShare, RESTRICT on ActivityLog) consistent and intentional? [Clarity, data-model.md]
- [x] CHK050 - **Project deletion behavior**: Is the behavior when a Project is deleted (RESTRICT on Document, preserve docs as "orphans") explicit? [Clarity, data-model.md, Edge Cases]

## Notes

- All entities have explicit FK delete behavior, which is excellent.
- The `CHECK` constraint on `DocumentShare` (CHK036) is a SQL-level rule that should be enforced via raw SQL in the migration.
- Two gaps: rollback procedure on migration failure (CHK047) and project deletion behavior (CHK050) are not fully addressed.
- 50 items covering 3 entities, 8+ indexes, 5 FK relationships, and cross-entity consistency.
