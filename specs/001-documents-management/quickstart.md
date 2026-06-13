# Quickstart: Document Upload and Management

**Phase**: 1 (Design)
**Date**: 2026-06-12
**Audience**: Developers and QA testing the feature locally

## Prerequisites

- .NET 8 SDK
- SQL Server LocalDB (included with Visual Studio or [standalone installer](https://learn.microsoft.com/en-us/sql/database-engine/configure-windows/sql-server-express-localdb))
- ClamAV daemon (only for full antivirus testing; the feature works in degraded mode without it)

## Setup (5 minutes)

```powershell
# 1. Clone and restore
git clone <repo-url> ContosoDashboard
cd ContosoDashboard-MiniMax/ContosoDashboard
dotnet restore

# 2. Install EF Core tools (one-time)
dotnet tool install --global dotnet-ef

# 3. Apply migrations
dotnet ef database update

# 4. (Optional) Install ClamAV for full AV testing
# Windows: download from https://www.clamav.net/downloads
# Or use docker: docker run -d -p 3310:3310 --name clamav clamav/clamav
# Configure daemon endpoint in appsettings.Development.json:
# {
#   "Antivirus": {
#     "ClamAV": {
#     "Host": "localhost",
#     "Port": 3310,
#     "Timeout": "00:00:30"
#   }
# }
```

## Run the App (1 minute)

```powershell
# From ContosoDashboard/ directory
dotnet run

# Browse to https://localhost:5001
# Or http://localhost:5000
```

## Test as Different Roles

The app uses mock authentication. Select a user from the login dropdown:

| User | Role | Use to test |
|------|------|-------------|
| `admin@contoso.com` | Administrator | Full access, reports |
| `camille.nicole@contoso.com` | Project Manager | Share within project |
| `floris.kregel@contoso.com` | Team Lead | Project documents |
| `ni.kang@contoso.com` | Employee | Personal uploads |

## Test Scenarios

### Scenario 1: Upload a personal document (3 minutes)

1. Login as `ni.kang@contoso.com`.
2. Navigate to **Documents** in the sidebar.
3. Click **Upload**.
4. Select a PDF (e.g., `<5 MB`).
5. Fill in:
   - **Title**: "My Onboarding Notes"
   - **Category**: "Personal Files"
   - **Tags**: "onboarding, training" (optional)
6. Click **Subir**.
7. **Expected**: progress bar reaches 100% in ≤ 10s; document appears in "Mis Documentos".

### Scenario 2: Upload a project document (3 minutes)

1. Login as `camille.nicole@contoso.com` (PM).
2. Navigate to **Projects** → click "Q4 Roadmap" (or any project).
3. Click **Upload document**.
4. Select a PDF.
5. Fill in:
   - **Title**: "Q4 Status Update"
   - **Category**: "Project Documents"
   - **Project**: "Q4 Roadmap" (auto-selected)
6. Click **Subir**.
7. **Expected**: all team members receive a notification in ≤ 5s.

### Scenario 3: Share a document (PM restriction test) (2 minutes)

1. Login as `camille.nicole@contoso.com` (PM of Q4 Roadmap).
2. Go to a project document uploaded by Camille.
3. Click **Share**.
4. Try to share with `ni.kang@contoso.com` (a team member of Q4 Roadmap).
5. **Expected**: share succeeds, Ni Kang sees the document in "Compartido conmigo".
6. Try to share the same document with an Employee NOT in the project.
7. **Expected**: share is **rejected** with "Solo el dueño del documento puede compartir fuera del proyecto."

### Scenario 4: Edit and replace file (2 minutes)

1. Login as the uploader of a document.
2. Open the document details.
3. Click **Edit metadata**, change the title, save.
4. **Expected**: change persists in ≤ 500 ms.
5. Click **Replace file**, upload a new version (different content).
6. **Expected**: new file replaces the old; the old file is deleted from disk in ≤ 100 ms.

### Scenario 5: Delete a document (1 minute)

1. Login as the uploader of a document.
2. Click **Delete**, confirm.
3. **Expected**: file deleted from disk in ≤ 100 ms; row removed from DB; cascade to `DocumentShare` and `ActivityLog` (per FK constraints).

### Scenario 6: Search and filter (1 minute)

1. Login as any user with multiple documents.
2. Go to **Documents** → **Search**.
3. Type a keyword that appears in `Title`, `Description`, or `Tags`.
4. **Expected**: results in ≤ 2s p95 (with ≤ 10k docs).
5. Apply filter by **Category** and **Project** simultaneously.
6. **Expected**: only matching documents shown.

### Scenario 7: Preview a PDF (1 minute)

1. Login as any user with a PDF document.
2. Click **Vista previa** on a PDF ≤ 10 MB.
3. **Expected**: PDF renders inline in an `<iframe>` in ≤ 3s.

### Scenario 8: Antivirus (AV) — file clean (2 minutes)

1. Login as any user.
2. Upload a clean PDF.
3. **Expected**: `ClamAV` reports "clean" in ≤ 5s; document persists.

### Scenario 9: Antivirus (AV) — file infected (2 minutes)

1. Create an EICAR test file (https://www.eicar.org/?page_id=3950):
   ```bash
   # Save this content to a file named "eicar.com"
   X5O!P%@AP[4\PZX54(P^)7CC)7}$EICAR-STANDARD-ANTIVIRUS-TEST-FILE!$H+H*
   ```
2. Login as any user, try to upload `eicar.com`.
3. **Expected**: rejected with "Archivo rechazado: amenaza detectada"; no record in DB; no file in disk.

### Scenario 10: Antivirus (AV) — degraded mode (2 minutes)

1. Stop the ClamAV daemon (or skip installation entirely).
2. Login as any user, upload a clean file.
3. **Expected**: file uploads successfully; document appears with a "⚠️ sin verificación AV" badge; warning logged.

## Run the Test Suite

### Unit + Technical + Component (fast, ~30s)

```powershell
# From ContosoDashboard.Tests.Unit/ directory
dotnet test

# From ContosoDashboard.Tests.Components/ directory
dotnet test
```

### Integration with real SQL Server (~2 min)

```powershell
# From ContosoDashboard.Tests.Integration/ directory
# Requires Docker (Testcontainers spin up SQL Server)
dotnet test
```

### E2E UI with Playwright (~3 min)

```powershell
# From ContosoDashboard.Tests.E2E.UI/ directory
# Requires Playwright browsers (installed via `pwsh bin/Debug/net8.0/playwright.ps1 install`)
dotnet test
```

### Mutation Testing (~10 min, only on `Services/Documents/`)

```powershell
# From the project root
dotnet stryker --project "ContosoDashboard/Services/Documents" `
  --threshold-break 70 `
  --threshold-high 80 `
  --threshold-low 60
```

### Performance — micro (BenchmarkDotNet, ~1 min per benchmark)

```powershell
# From ContosoDashboard.Tests.Performance/ directory
dotnet run -c Release --project . --filter "*Benchmark*"
```

### Performance — load (k6, ~10 min for full load profile)

```powershell
# Install k6
choco install k6  # Windows
# Or: https://k6.io/docs/getting-started/installation/

# Smoke test
k6 run --duration 1m --vus 1 k6/smoke.js

# Load test (10 min, 50 VUs)
k6 run --duration 10m --vus 50 k6/load.js
```

## Inspect Generated Artifacts

After running tests, coverage reports are in `quality-reports/coverage/`. Open `index.html` in a browser.

The full Markdown report is at `code-quality-report.md` (regenerated by the `/code-quality` agent or the orchestrator script).

## Common Issues

| Issue | Solution |
|-------|----------|
| `dotnet ef` not found | `dotnet tool install --global dotnet-ef` |
| Migration fails on existing DB | `dotnet ef database drop --force` then `dotnet ef database update` |
| ClamAV connection refused | Verify daemon is running on port 3310; check `appsettings.Development.json` |
| Testcontainers fails | Verify Docker is running |
| Playwright browsers missing | `pwsh ContosoDashboard.Tests.E2E.UI/bin/Debug/net8.0/playwright.ps1 install` |
| `dotnet run` fails on macOS/Linux | Use `dotnet run --urls "http://0.0.0.0:5000"` to bind to all interfaces |

## What's Next?

- Run `/speckit.tasks` to get the task breakdown for implementation.
- After implementing, run `/code-quality` to generate the quality report.
- Run `/speckit.analyze` to cross-check spec/plan/tasks consistency.
