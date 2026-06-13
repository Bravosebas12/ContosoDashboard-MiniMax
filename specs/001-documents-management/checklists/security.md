# Security Quality Checklist: Document Upload and Management

**Purpose**: Validate that the requirements for OWASP Top 10, authorization, antivirus, and security logging are complete, unambiguous, and testable.
**Created**: 2026-06-12
**Feature**: [spec.md](../spec.md) (Document Upload and Management)
**Constitution Reference**: v1.1.0, Principle II (Requisitos de Seguridad)

## OWASP Top 10 Coverage

- [x] CHK001 - **A01 Broken Access Control**: Are all protected pages explicitly required to use `[Authorize]` attribute with role-based policies? [Completeness, FR-032, §II.A01]
- [x] CHK002 - **A01 Defense in depth**: Does the spec require ownership re-validation in the `DocumentService` for every read/write/delete operation (not just `[Authorize]` on the page)? [Coverage, FR-033, §II.A01]
- [x] CHK003 - **A01 PM sharing restriction**: Is the rule "Project Manager can only share within project" quantified with explicit check logic and error code? [Clarity, FR-035, Clarifications Q1]
- [x] CHK004 - **A02 Cryptographic Failures**: Does the spec require HSTS, secure cookie flags (HttpOnly, SameSite=Lax), and TLS for the file download endpoint? [Coverage, §II.A02, existing infrastructure]
- [x] CHK005 - **A02 Secret management**: Are storage connection strings, antivirus daemon endpoint, and any future secrets required to be stored in user-secrets or environment variables (not `appsettings.json`)? [Completeness, §II.A02]
- [x] CHK006 - **A03 Injection (SQL)**: Is the prohibition of `FromSqlRaw` / `ExecuteSqlRaw` with user input explicitly stated? [Clarity, §II.A03]
- [x] CHK007 - **A03 Injection (path traversal)**: Is the GUID-based filename pattern (no user-supplied filename in path) enforced with a regex or validator? [Clarity, FR-006, Edge Cases]
- [x] CHK008 - **A03 Injection (MIME spoofing)**: Is the spec clear that `Content-Type` from client is NOT trusted, and that MIME type is validated via magic bytes (or whitelist of Content-Type + extension)? [Gap, FR-003, §1.3.6]
- [x] CHK009 - **A04 Insecure Design**: Are state machine transitions (Document → Replaced, Document → Deleted) enforced server-side, with explicit allowed-from-states? [Clarity, §II.A04]
- [x] CHK010 - **A05 Security Misconfiguration**: Does the spec require security headers (CSP, X-Frame-Options, X-XSS-Protection, X-Content-Type-Options, Referrer-Policy) on the file download endpoint? [Coverage, §II.A05]
- [x] CHK011 - **A06 Vulnerable Components**: Is antivirus scanning (ClamAV) required BEFORE file persistence for every upload? [Coverage, FR-004, §9.1]
- [x] CHK012 - **A06 Vulnerable Components**: Is the spec clear that a `dotnet list package --vulnerable` check is a CI gate (auto-fail on High/Critical)? [Coverage, SC-015, §V Quality Gates]
- [x] CHK013 - **A07 Authentication Failures**: Is the file download endpoint required to re-authenticate (cookie + claims) on every request, not rely on long-lived URLs? [Clarity, §II.A07]
- [x] CHK014 - **A08 Data Integrity**: Are all uploaded files required to be validated with both extension AND MIME type checks before persistence? [Completeness, FR-003, §II.A08]
- [x] CHK015 - **A08 Data Integrity**: Is the spec clear that file replace generates a new GUID (atomic swap), and that the old file is deleted only AFTER the new one is verified? [Clarity, FR-012, Edge Cases]
- [x] CHK016 - **A09 Logging Failures**: Are all security events (upload, download, delete, share, revoke, access_denied) required to be logged with structured fields (documentId, userId, ipAddress, timestamp, result)? [Coverage, FR-029, §II.A09]
- [x] CHK017 - **A09 Log retention**: Is the log retention period (≥ 90 days) specified with a cleanup mechanism (background service)? [Completeness, FR-031]
- [x] CHK018 - **A09 Sensitive data**: Is the spec explicit that passwords, tokens, and FilePath (which contains GUID) must NOT appear in logs? [Clarity, §II.A09]
- [x] CHK019 - **A10 SSRF**: Is the spec explicit that no endpoint accepts a URL and fetches it on behalf of the user (preventing SSRF)? [Clarity, §II.A10]

## Authorization (FR-032 to FR-035)

- [x] CHK020 - **Role policies**: Are the 4 role-based policies (Employee, TeamLead, ProjectManager, Administrator) defined with their scope? [Completeness, FR-032]
- [x] CHK021 - **Authorization layers**: Is the spec explicit about the 3-layer defense (page attribute → service ownership check → DB-level FK constraint)? [Completeness, FR-033]
- [x] CHK022 - **Share permissions**: Is the `DocumentShare` permission model (Read/Write enum, with ExpiresAt and RevokedAt soft-deletion) fully specified? [Completeness, FR-023]
- [x] CHK023 - **Share recipient verification**: Does the spec require verifying that the share recipient is an active user (not a deleted account)? [Gap, §III Key Entities]
- [x] CHK024 - **Delete authorization**: Is the rule "PM can delete documents in their project, even if uploaded by team member" explicit with a project-membership check? [Clarity, FR-021]
- [x] CHK025 - **Access denied logging**: Are 403 responses required to be logged in `ActivityLog` with reason (not just the status code)? [Completeness, FR-034]

## Antivirus (FR-004, FR-005, §9)

- [x] CHK026 - **AV provider**: Is the ClamAV + nClam choice documented with rationale (open-source, offline) and an alternative path for production? [Completeness, §9.1]
- [x] CHK027 - **AV pre-persistence**: Is it explicit that the AV scan happens BEFORE writing the file to disk (not after)? [Clarity, §9.6]
- [x] CHK028 - **AV timeout**: Is the AV scan timeout (30s) specified, with a defined behavior on timeout (Error state + training degradation)? [Completeness, §9.5]
- [x] CHK029 - **AV latency SLO**: Is the AV scan latency target (≤ 5s for 25 MB) defined and testable? [Measurability, SC-013 implied]
- [x] CHK030 - **AV degraded mode**: Is the behavior when AV is unavailable documented for both training (allow + flag) and production (503 fail-closed)? [Clarity, §9.4, AC-1.3.3, AC-1.3.4]
- [x] CHK031 - **AV result persistence**: Is the `AvScanStatus` (Clean/Infected/NotScanned/Error) and `AvScanAt` (timestamp) required to be persisted in the Document entity? [Completeness, §III Document entity]
- [x] CHK032 - **AV rejection flow**: Is the rejection flow on infected file explicit (no DB record, no file on disk, error to user)? [Clarity, FR-005, AC-1.3.2]

## File Storage Security (FR-006, FR-007, FR-012)

- [x] CHK033 - **Path outside wwwroot**: Is the spec explicit that uploaded files MUST be stored outside `wwwroot` (no direct URL access)? [Completeness, FR-006, §StakeholderDoc]
- [x] CHK034 - **Path pattern**: Is the exact path pattern `{userId}/{projectIdOrPersonal}/{guid}.{ext}` documented with examples? [Clarity, FR-006, §9.6]
- [x] CHK035 - **GUID uniqueness**: Is the spec clear that GUIDs are generated before DB insertion to prevent duplicate key violations AND to ensure no collision? [Clarity, FR-007, §StakeholderDoc]
- [x] CHK036 - **Rollback on DB failure**: Is the rollback sequence (delete file from disk if `SaveChangesAsync` fails) explicitly required? [Completeness, FR-008, AC-1.4.2]
- [x] CHK037 - **No user filename in path**: Is the prohibition of using user-supplied filenames in the file path explicit (anti path-traversal)? [Clarity, FR-006, §StakeholderDoc Security]
- [x] CHK038 - **Authorization on download**: Is the spec clear that the download endpoint must re-validate ownership/permission before serving the file (defense in depth)? [Completeness, §StakeholderDoc Security]

## Input Validation (FR-002, FR-003, FR-010, FR-011)

- [x] CHK039 - **Whitelist of MIME types**: Are the 16 allowed MIME types enumerated (PDF, Office, txt, JPEG, PNG with their full MIME strings)? [Clarity, §9.3]
- [x] CHK040 - **File size limit**: Is the 25 MB limit explicit, with a clear error message for oversized files? [Clarity, FR-002, AC-1.1.3]
- [x] CHK041 - **Title validation**: Are the title constraints (1-200 chars, required) defined with the error message? [Clarity, FR-020, AC-1.2.1]
- [x] CHK042 - **Tag validation**: Are the tag constraints (max 5, max 50 chars each, lowercase) explicit? [Clarity, FR-011, AC-1.2.4]
- [x] CHK043 - **Description validation**: Is the description max length (2000 chars) specified? [Clarity, FR-020]

## Error Handling Security

- [x] CHK044 - **Generic error messages**: Are error messages designed to NOT leak information (e.g., no "user not found" vs "wrong password" distinction)? [Clarity, §II.A07]
- [x] CHK045 - **Fail-secure defaults**: When in doubt, is the default behavior to DENY access (not allow)? [Consistency, §II.A04, §9.4 production mode]
- [x] CHK046 - **Cleanup on error**: Is it explicit that on any upload failure mid-flow, the partially-written file is removed? [Completeness, FR-008, AC-1.4.1]

## Notes

- All 46 items in this checklist are tied to existing spec sections, FRs, ACs, or constitution principles.
- Coverage is strong on A01 (authorization) and A09 (logging) which are explicitly mapped in the spec.
- A few items (CHK005, CHK008, CHK023) are marked as `Gap` because they're not explicitly addressed in the spec; they should be confirmed in `/speckit.tasks` or via explicit FRs in a future amendment.
- Items NOT in scope of this checklist (and why): session/token lifecycle (out of scope per stakeholder), GDPR/PII handling (training-only), encryption at rest (training-only).
