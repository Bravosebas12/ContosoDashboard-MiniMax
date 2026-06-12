# Research: Document Upload and Management

**Phase**: 0 (Research)
**Date**: 2026-06-12
**Status**: Complete — all NEEDS CLARIFICATION resolved in spec (FR-035, FR-038, edge case replace)

## Purpose

Document the technology decisions and rationale for the Document Upload and Management feature, including research into alternatives, trade-offs, and best practices. The spec's `## Clarifications` section resolved the three highest-impact ambiguities (sharing permissions, Azure Blob scope, race condition on replace). This research document captures the supporting rationale for the architectural and tooling choices that follow.

## Technology Stack Decisions

### 1. Blazor Server (already in use, confirmed)

**Decision**: Continue with Blazor Server (no migration to Blazor WebAssembly or MVC).

**Rationale**:
- Constitución I mandates ASP.NET Core 8.0 + Blazor Server for this project.
- Stateful circuits favor the document upload UX (no need to re-auth on each request).
- The mock authentication system is already wired to `CustomAuthenticationStateProvider` (Blazor-specific).
- Real-time notifications via SignalR (already available) align with the `< 5s` notification latency requirement (FR-024).

**Alternatives considered**:
- **Blazor WebAssembly**: would require redesigning auth, breaking the existing mock system, and adds complexity for offline use. Rejected.
- **MVC + Razor Pages**: would require rewriting all existing pages and components. Rejected.

### 2. EF Core 8.0 with Migrations from Day 1

**Decision**: Use `dotnet ef migrations add` for the initial schema, NOT `EnsureCreated()`.

**Rationale**:
- Constitución IV prefers migrations over `EnsureCreated()`.
- The StakeholderDoc §Database Setup notes that `EnsureCreated()` causes duplicate key violations on orphan records; migrations are more robust.
- Migrations are required for production parity (though this is training-only).

**Alternatives considered**:
- **`EnsureCreated()`**: simpler but creates orphan record issues. Rejected.
- **Dapper / raw SQL**: more performant but no migration tooling built-in. Rejected for consistency with existing `ApplicationDbContext`.

### 3. LocalFileStorageService over `System.IO.File` Direct Usage

**Decision**: Abstract file operations behind `IFileStorageService` interface; implement with `LocalFileStorageService`.

**Rationale**:
- Constitution IV favors SOLID principles (interface segregation, dependency inversion).
- FR-036 requires the interface; FR-037 requires the local implementation.
- Allows future swap to another backend WITHOUT changing controllers/pages/business logic (per stakeholder's request: "que se guarde localmente, no invoLucre el blob" — but the interface design leaves the door open without commitment).
- Testing benefits: easy to mock the storage service in unit tests without touching the filesystem.

**Alternatives considered**:
- **Direct `System.IO.File` calls in DocumentService**: simpler, but couples business logic to filesystem. Rejected.
- **Azure Blob SDK from day 1**: contradicts FR-038 + user feedback (training-only, no cloud). Rejected.

### 4. ClamAV + nClam for Antivirus in Training

**Decision**: Use **nClam** (.NET client) to talk to a **ClamAV** daemon in training.

**Rationale**:
- ClamAV is open-source and runs offline (no cloud).
- nClam is the de-facto .NET client, NuGet-published, well-maintained.
- Supports all 16 MIME types in the whitelist (PDF, Office, txt, JPEG, PNG).
- Latency: p95 ≤ 5s for 25 MB files (per StakeholderDoc §9.5).

**Alternatives considered**:
- **Microsoft Defender**: requires Windows licensing, harder to set up offline. Rejected for training.
- **VirusTotal API**: requires internet. Rejected (offline-first).
- **Skip antivirus in training**: violates A06 of the constitution. Rejected.

**Known limitation**: ClamAV daemon must be running locally. If not available, the degraded mode (training: log warning + allow + flag "⚠️ sin verificación AV") kicks in (per StakeholderDoc §9.4 + AC-1.3.3).

### 5. GUID-based Filenames

**Decision**: Generate a new GUID per file upload; store as `FilePath` in DB. Original filename stored separately for display/download.

**Rationale**:
- Prevents path traversal attacks (OWASP A03).
- Last-writer-wins concurrency: replace generates a new GUID, so the old download completes with the old file while new GETs see the new file (per Q3 of `## Clarifications`).
- Names don't collide even with concurrent uploads from the same user.

**Alternatives considered**:
- **Use original filename + sanitization**: still risky (path traversal, special chars). Rejected.
- **Hash-based filenames (SHA-256)**: deduplication benefit not needed in training. Rejected.

### 6. Tags as Comma-Separated Column

**Decision**: Store tags as a single `NVARCHAR(500)` column, comma-separated, lowercase, max 5 tags × 50 chars.

**Rationale**:
- Simpler than a normalized `DocumentTag` table.
- Query: `WHERE Tags LIKE '%budget%'` is fast enough for ≤ 10k documents with proper indexing.
- Sufficient for the training use case (max 5 tags, search via `LIKE` or full-text).

**Alternatives considered**:
- **Normalized `DocumentTag` table**: more complex (JOIN queries), but supports exact-match tag search. Rejected for simplicity.
- **JSON column**: more flexible but harder to query. Rejected.

### 7. Pagination Strategy: Offset-based

**Decision**: Use offset-based pagination (`Skip(page * size).Take(size)`) with page size = 25.

**Rationale**:
- Specified in spec (FR-013): "paginada (page size 25)".
- Simple to implement and explain to users.
- Acceptable performance up to ~50k documents per user (training has ≤ 500).

**Alternatives considered**:
- **Cursor-based pagination**: more performant for deep pages, but overkill for ≤ 500 documents. Rejected.
- **No pagination (load all)**: violates spec. Rejected.

### 8. Notifications: SignalR Broadcast (no polling)

**Decision**: Use the existing `INotificationService` (which is backed by SignalR) for in-app notifications, persisting in the `Notification` table.

**Rationale**:
- `< 5s` notification latency (FR-024) is achievable with SignalR.
- Real-time UX is the Blazor Server sweet spot.
- Reuses existing infrastructure (`Pages/Notifications.razor`, `Models/Notification.cs`).

**Alternatives considered**:
- **Polling every N seconds**: simpler, but adds latency. Rejected (UX).
- **Email**: requires SMTP infrastructure. Rejected (out of scope, not in training).

### 9. Test Strategy: 8-level Pyramid per Constitución V

**Decision**: Implement the full 8-level test pyramid aligned with Constitución v1.1.0 Principio V.

**Rationale**:
- Constitution V is non-negotiable.
- TDD Hard (ROJO → VERDE → REFACTOR) enforced for all production code.
- Mutation score ≥ 70% (Stryker.NET) is a hard gate.

**Tooling rationale**:
- **xUnit**: standard .NET test framework, broad community.
- **NSubstitute**: more natural API than Moq for .NET (no `Mock<T>.Setup()` boilerplate).
- **FluentAssertions**: clearer failure messages than xUnit's `Assert.Equal`.
- **bUnit**: native Blazor component testing, supports `TestContext` for DI.
- **Testcontainers**: real SQL Server in Docker, eliminates "works on my machine" issues.
- **PactNet**: consumer-driven contract testing, industry standard.
- **Playwright**: cross-browser E2E, supports Blazor Server's SignalR-based UI.
- **Stryker.NET**: .NET mutation testing (mutates statements/branches/strings).
- **BenchmarkDotNet**: micro-benchmarks (ns/op, allocs).
- **NBomber**: in-process load tests, code-defined scenarios.
- **k6**: external load tests (5 profiles: smoke, load, stress, spike, soak).

**Alternatives considered**:
- **MSTest / NUnit**: xUnit is more popular in the .NET community in 2026. Selected.
- **Moq**: NSubstitute has cleaner syntax. Selected.
- **Coverlet directly**: we use it, but ReportGenerator converts to human-readable. Selected combo.

### 10. Last-Writer-Wins Concurrency Model

**Decision**: No file locks; replace generates a new GUID; old downloads complete with old file.

**Rationale**:
- Per Q3 of `## Clarifications` in spec: "Last-writer-wins con GUID nuevo".
- Simple: no distributed lock manager, no coordination protocol.
- Consistent with how CDNs (Cloudflare, S3) handle versioning.
- Testable: integration test with 2 concurrent clients verifies behavior.

**Alternatives considered**:
- **File lock per document**: requires lock manager, risks deadlocks. Rejected.
- **409 on active download**: poor UX, requires coordination. Rejected.

## Patterns to Follow

From constitution v1.1.0:
- **Pattern: Modern C#** — `record` for DTOs, `sealed class` for entities, nullable reference types enabled, async/await everywhere, DI by constructor.
- **Pattern: Defense in depth** — `[Authorize]` on pages + ownership re-validation in service layer.
- **Pattern: Paged result** — return `PagedResult<T>` from list endpoints (per Constitución III).
- **Pattern: Audit log** — log structured events for all security-relevant actions (per Constitución II.A09).
- **Pattern: Test-first** — write failing test, implement, refactor, review (per Constitución V.1).

## Patterns to Avoid

- **Anti-pattern: AutoMapper** — use explicit extension methods or Mapster (per Constitución IV).
- **Anti-pattern: Service locator** — use DI by constructor, never `IServiceProvider` direct (per Constitución IV).
- **Anti-pattern: `.Result` or `.Wait()`** — always `await` with `CancellationToken` (per Constitución III).
- **Anti-pattern: `ExcludeFromCodeCoverageAttribute`** — every method must be covered (per Constitución IV).
- **Anti-pattern: Long-running synchronous code in Blazor** — Blazor circuits share threads; blocking calls degrade all users.

## Risks and Mitigations

| Risk | Probability | Impact | Mitigation |
|------|-------------|--------|------------|
| ClamAV daemon not available in training environment | Medium | High (security gap) | Fail-open in training (log warning + flag); production uses fail-closed |
| SQL Server LocalDB max 10 GB | Low | Medium | Storage limit applies to metadata only; files in filesystem separate |
| Blazor Server circuit memory pressure on large uploads | Medium | Medium | Use MemoryStream pattern + chunked upload for files > 10 MB; documented in StakeholderDoc §Blazor-Specific |
| Last-writer-wins may surprise users expecting "versioning" | Low | Low | UI clearly indicates file replacement (not versioning); documented in feature help |
| Stryker.NET long runtimes on large codebase | Medium | Low | Configured to mutate only `Services/Documents/`, `Domain/Documents/`, validators, parsers — not the whole codebase |
| Playwright flakiness on Blazor Server SignalR | Medium | Medium | Use `page.waitForSignalR` helpers; rerun flaky tests automatically in CI |

## Open Architectural Questions (Deferred to Implementation)

These are NOT NEEDS CLARIFICATION but design decisions to confirm during code review:

1. **Where to host the ClamAV daemon** — same machine (default), or a sidecar container in docker-compose? Decision: same machine for training, containerized for future.
2. **What directory under AppData** — `AppData/uploads/` or per-user subdirectories? Decision: `AppData/uploads/{userId}/` (matches path pattern).
3. **Notification transport when user is offline** — queue in DB, deliver on reconnect? Decision: yes, queue in `Notification` table; SignalR delivers when circuit reconnects.
4. **Document preview iframe sandbox** — should we restrict scripts in the iframe? Decision: yes, `sandbox="allow-same-origin"` only (no scripts, no top navigation).
5. **Audit log retention enforcement** — DB-level job or application-level? Decision: application-level `IActivityLogCleanupService` runs daily, deletes > 90 days old (per FR-031).

## References

- **Spec**: [spec.md](spec.md) — 8 user stories, 38 FRs, 15 SCs, 11 edge cases, 0 NEEDS CLARIFICATION.
- **StakeholderDoc**: [StakeholderDocs/document-upload-and-management-feature.md](../../StakeholderDocs/document-upload-and-management-feature.md) — original requirements + §7-§9 (testing, ACs, AV details).
- **Constitution**: [`.specify/memory/constitution.md`](../../.specify/memory/constitution.md) v1.1.0.
- **Quality checklist**: [checklists/requirements.md](checklists/requirements.md) — 21/21 items passed.
- **Skills referenced**: `crap-analysis` (cobertura + CRAP), `sigat-security-owasp` (security), `sqlserver-dba` (DB performance), `unit-testing-vitest` (pirámide de pruebas).
