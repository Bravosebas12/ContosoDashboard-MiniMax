# ContosoDashboard — Docker setup

This directory contains the containerized stack for running ContosoDashboard
(Blazor Server, .NET 8) end-to-end without installing SQL Server or ClamAV
on the host machine.

## Architecture

```
┌──────────────────────┐    ┌──────────────────────┐    ┌──────────────────────┐
│  app  (Blazor 8)     │    │  db  (SQL Server 22) │    │  clamav (1.3)        │
│  Kestrel :8080       │◀──▶│  Express edition     │    │  clamd :3310         │
│  /app/AppData/uploads│    │  /var/opt/mssql      │    │  /var/lib/clamav     │
└──────────────────────┘    └──────────────────────┘    └──────────────────────┘
        │                              ▲                            ▲
        │  uploaded docs persisted     │ EF Core 8                  │ nClam client
        ▼                              │                            │
  contoso-uploads (named vol)          └────────────────────────────┘
                                                  │
                                          contoso-net (bridge)
```

## Prerequisites

| Tool          | Version   | Notes                                        |
|---------------|-----------|----------------------------------------------|
| Docker Engine | ≥ 24.0    | WSL2 backend recommended on Windows         |
| Docker Compose| ≥ 2.20    | Ships with Docker Desktop                    |
| RAM           | ≥ 4 GB    | SQL Server container alone needs ~1.5 GB     |
| Disk          | ≥ 20 GB   | Images + DB + ClamAV signatures (~1 GB)      |

## Quick start

```bash
# 1. Copy the environment template and edit secrets
cp .env.example .env

# 2. (Optional) start ClamAV definitions in advance to avoid first-run delay
docker compose pull clamav

# 3. Bring up the full stack
docker compose up -d --build

# 4. Follow the application logs
docker compose logs -f app

# 5. Open the dashboard
#    http://localhost:8080
```

The first start of the **clamav** service may take 2–5 minutes to download
virus signatures. The **app** service waits on `depends_on` health checks, so
it will not start accepting traffic until both `db` and `clamav` are ready.

## Files in this directory

| File                                | Purpose                                              |
|-------------------------------------|------------------------------------------------------|
| `../Dockerfile`                     | Multi-stage build: SDK → ASP.NET runtime, non-root   |
| `../docker-compose.yml`             | Base stack: db + clamav + app                        |
| `../docker-compose.override.yml`    | Local dev overrides: bind mount + hot-reload         |
| `../.env.example`                   | Environment variables template                       |
| `../.dockerignore`                  | Excludes from the build context                      |
| `./db-init/`                        | SQL scripts run on first DB start (optional)         |

## Common tasks

### View logs of a single service

```bash
docker compose logs -f app
docker compose logs -f db
docker compose logs -f clamav
```

### Open a shell inside the app container

```bash
docker compose exec app bash
```

### Connect to the database with sqlcmd

```bash
docker compose exec db /opt/mssql-tools18/bin/sqlcmd \
    -S localhost -U sa -P "$SA_PASSWORD" -No
```

### Reset all data (CAUTION — destructive)

```bash
docker compose down -v       # removes containers AND named volumes
docker compose up -d --build
```

### Update virus definitions only

```bash
docker compose exec clamav freshclam
```

### Rebuild only the app image

```bash
docker compose build app
docker compose up -d app
```

## Production notes

> ⚠️ This project is officially **training-only** per `plan.md`. The Compose
> stack here is intended for **local development and CI testing**, not for
> production deployment. If you do deploy it:
>
> 1. **Change all secrets** in `.env` (especially `SA_PASSWORD`).
> 2. **Add a reverse proxy** (Traefik / Caddy / nginx) in front of the `app`
>    service to terminate TLS — the container exposes HTTP only.
> 3. **Back up the volumes** `contoso-db-data` and `contoso-uploads` on a
>    schedule (e.g. `docker run --rm -v contoso-db-data:/data -v $(pwd):/backup
>    alpine tar czf /backup/db-$(date +%F).tar.gz /data`).
> 4. **Set `Antivirus__ClamAV__AllowDegradedMode=false`** so a missing AV
>    engine fails uploads closed (per Constitution II A06).
> 5. **Pin image digests**, not tags, in a real release pipeline.

## Troubleshooting

| Symptom                                              | Likely cause / fix                                                                                  |
|------------------------------------------------------|------------------------------------------------------------------------------------------------------|
| `app` keeps restarting, logs show SQL connection error | `db` healthcheck still failing. Run `docker compose logs db`. Most common: weak `SA_PASSWORD`.       |
| `dotnet` exits with code 137                         | Out of memory. Increase Docker Desktop RAM to ≥ 4 GB, or switch `MSSQL_PID` to `Express`.            |
| `clamav` container restarts in a loop                | `freshclam` cannot reach the network. Set `CLAMAV_NO_FRESHCLAM=1` and pre-bake signatures.           |
| `dotnet watch` exits immediately in dev override     | You are missing `Dockerfile`'s `build` target in the override. Check `target: build` is present.     |
| Permission denied on `/app/AppData/uploads`          | On Linux hosts, ensure the bind-mounted volume in the override preserves UID 1000 (`app` user).      |

## SQL seed scripts

Drop `*.sql` files in `./db-init/`. They are executed **once**, in
alphabetical order, the first time the `db` container starts with an empty
data volume. Subsequent restarts skip them.

Example `docker/db-init/01-seed-demo-users.sql`:

```sql
USE ContosoDashboard;
GO
-- Demo data goes here. Idempotent statements only.
```
