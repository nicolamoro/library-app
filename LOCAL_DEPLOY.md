# Local Deploy — Docker Compose

Guide to starting the full stack (database + app) with a single command.

## Prerequisites

- [Docker Desktop](https://www.docker.com/products/docker-desktop/) installed and running

## Full start

From the project root:

```sh
docker compose up --build
```

Docker handles everything:
1. Builds the SQL Server image with the init scripts
2. Starts the `db` container, automatically runs `01_schema.sql`, `02_procedures.sql`, `03_seed.sql`
3. Waits for the database to be ready (healthcheck)
4. Builds and starts the `app` container (Blazor Server)

The app is available at **http://localhost:8080**.

First start takes ~2-3 minutes for image downloads. Subsequent starts are much faster.

> **Note:** database data persists in a Docker volume (`sqlserver-data`). Deleting the volume resets everything back to the seed data.

## Stopping the stack

```sh
docker compose down        # stops containers, volume is preserved
docker compose down -v     # stops containers and deletes the volume (data lost)
```

## Rebuilding after changes

If you only changed Blazor code (no changes to SQL scripts or the db Dockerfile):

```sh
docker compose up --build -d app
```

Rebuilds and restarts only the `app` container, leaving the database intact and running. Faster than a full rebuild.

To rebuild the full stack:

```sh
docker compose up --build
```

## Direct database connection

Use **Azure Data Studio** or **DBeaver** with the following parameters while the stack is running:

| Parameter | Value |
|---|---|
| Host | `localhost` |
| Port | `1433` |
| User | `sa` |
| Password | `StrongPass123!` |
| Database | `LibraryDB` |

## Container structure

| Container | Base image | Exposed port |
|---|---|---|
| `library-app-db-1` | SQL Server 2022 | 1433 |
| `library-app-app-1` | .NET 10 ASP.NET Core | 8080 |

## Healthcheck

The `app` container depends on `db` via `condition: service_healthy`. The healthcheck runs a T-SQL query every 10 seconds to verify that the `LibraryDB` database exists; only once it passes does Docker start the app. If the database is slow to start, increase `retries` in `docker-compose.yml`.
