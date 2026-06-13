# db-init

Drop `*.sql` files in this directory. SQL Server 2022 will run them **once**,
in alphabetical order, the first time the `db` container starts with an empty
data volume. The database (`ContosoDashboard`) is created automatically by the
application on first run via `context.Database.EnsureCreated()`, so this
folder is for **seed data** and **schema patches**, not for creating the DB
itself.

## Conventions

- File names: `NN-description.sql` (e.g. `01-seed-demo-users.sql`).
- Each file should be idempotent (`IF NOT EXISTS` / `MERGE`).
- Do not put credentials here — pass them via environment variables.
