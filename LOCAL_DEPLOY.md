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
1. Pulls the `postgres:16-alpine` image and mounts `db/init/` into `/docker-entrypoint-initdb.d`
2. Starts the `db` container, which on first init automatically runs `01_schema.sql`, `02_procedures.sql`, `03_seed.sql`
3. Waits for the database to be ready (healthcheck)
4. Builds and starts the `app` container (Blazor Server)

The app is available at **http://localhost:8080**.

First start takes ~1-2 minutes for image downloads. Subsequent starts are much faster.

> **Note:** database data persists in a Docker volume (`postgres-data`). The init scripts run **only when the volume is empty**, so to re-seed you must delete the volume (`docker compose down -v`).

## Stopping the stack

```sh
docker compose down        # stops containers, volume is preserved
docker compose down -v     # stops containers and deletes the volume (data lost)
```

## Rebuilding after changes

If you only changed Blazor code (no changes to SQL scripts):

```sh
docker compose up --build -d app
```

Rebuilds and restarts only the `app` container, leaving the database intact and running. Faster than a full rebuild.

To rebuild the full stack:

```sh
docker compose up --build
```

## Direct database connection

Use **DBeaver**, **pgAdmin**, or the `psql` CLI with the following parameters while the stack is running:

| Parameter | Value |
|---|---|
| Host | `localhost` |
| Port | `5432` |
| User | `postgres` |
| Password | `StrongPass123!` |
| Database | `librarydb` |

```sh
docker exec -it $(docker compose ps -q db) psql -U postgres -d librarydb
```

## Container structure

| Container | Base image | Exposed port |
|---|---|---|
| `library-app-db-1` | PostgreSQL 16 (alpine) | 5432 |
| `library-app-app-1` | .NET 10 ASP.NET Core | 8080 |

## Healthcheck

The `app` container depends on `db` via `condition: service_healthy`. The healthcheck runs `pg_isready` every 10 seconds to verify that PostgreSQL is accepting connections; only once it passes does Docker start the app. If the database is slow to start, increase `retries` in `docker-compose.yml`.
