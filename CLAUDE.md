# CLAUDE.md

This file provides guidance to Claude Code (claude.ai/code) when working with code in this repository.

## Running the application

```sh
# Start the full stack (DB + app)
docker compose up --build

# Rebuild only the Blazor app after code changes (DB stays up)
docker compose up --build -d app

# Stop without losing data
docker compose down

# Stop and wipe the DB volume
docker compose down -v
```

App runs at **http://127.0.0.1:8080** (use `127.0.0.1`, not `localhost` — corporate VPN/proxy intercepts the `localhost` hostname on this machine).

## Running SQL scripts manually

```sh
sqlcmd -S localhost -U sa -P "StrongPass123!" -No -i db/init/01_schema.sql
sqlcmd -S localhost -U sa -P "StrongPass123!" -No -d LibraryDB -i db/init/02_procedures.sql
sqlcmd -S localhost -U sa -P "StrongPass123!" -No -d LibraryDB -i db/init/03_seed.sql
```

### Volume seed (stress test)

`db/seed_volume.sql` inserts ~13,350 additional rows for load testing (14 genres, 50 publishers, 300 authors, 1,000 books, 2,000 customers, 10,000 loans). Run it **after** the base seed, on demand:

```sh
# Via Docker (DB container must be running)
docker exec -i $(docker compose ps -q db) \
  /opt/mssql-tools18/bin/sqlcmd -S localhost -U sa -P "StrongPass123!" -C \
  -i /dev/stdin < db/seed_volume.sql

# Or if sqlcmd is available locally
sqlcmd -S localhost -U sa -P "StrongPass123!" -No -d LibraryDB -i db/seed_volume.sql
```

The script is **not idempotent**: running it twice raises an error and exits without changes (guard on `isbn LIKE 'VOL%'`). To reset, wipe the volume and restart: `docker compose down -v && docker compose up --build`.

## Architecture

**Blazor Server → Dapper → SQL Server** (no service layer).

Razor components inject repositories directly, call `async` methods, and call `StateHasChanged()` where needed. There is no intermediate service or CQRS layer.

```
Browser ──WebSocket──► Blazor Server Circuit
                             │
                   Component Rendering
                             │
                   Repository (Dapper)
                             │
                        SQL Server
                   (stored procedures)
```

### Key directories

| Path | Purpose |
|---|---|
| `LibraryApp/Components/Pages/` | Razor pages: Home (dashboard), Books/, Customers/, Loans/ |
| `LibraryApp/Components/Layout/` | Shell: `MainLayout.razor` (AppBar, Drawer, dark mode), `NavMenu.razor` |
| `LibraryApp/Data/` | `DapperContext` (connection factory), repositories for Book/Customer/Loan |
| `LibraryApp/Models/` | POCOs: `Book`, `Author`, `Genre`, `Publisher`, `Customer`, `LoanDetail` |
| `db/init/` | SQL scripts: `01_schema.sql` (DDL), `02_procedures.sql` (SPs), `03_seed.sql` |

### Database

SQL Server 2022. Loan lifecycle is managed by two stored procedures:
- **`sp_borrow_book`** — validates customer status and book availability, inserts loan, decrements `available_copies`
- **`sp_return_book`** — sets `return_date`, computes `fine_amount` if overdue, increments `available_copies`

Direct SQL queries are written inline in repositories using Dapper raw SQL (no ORM). `DateOnly` mapping requires the custom `DateOnlyTypeHandler` registered in `Program.cs`.

### Pagination

All list pages (Dashboard, Books, Customers, Loans) use **server-side pagination** via MudBlazor's `MudTable ServerData` pattern. Each repository exposes a dedicated paged method alongside the original `GetAllAsync`:

| Repository method | Used by | Filter |
|---|---|---|
| `BookRepository.GetPagedAsync(page, pageSize, search?)` | BookList | LIKE on title / genre / authors |
| `CustomerRepository.GetPagedAsync(page, pageSize, search?)` | CustomerList | LIKE on full name / email |
| `LoanRepository.GetPagedAsync(page, pageSize, filter)` | LoanList | exact `status` match (`all` = no filter) |
| `LoanRepository.GetOverduePagedAsync(page, pageSize)` | Home dashboard | `status = 'overdue'` or active past due |

Each method runs **two SQL statements in one round-trip** via `QueryMultipleAsync`: a `COUNT(*)` and a `SELECT … OFFSET @Offset ROWS FETCH NEXT @PageSize ROWS ONLY`. The UI uses `MudTablePager` with options `[10, 20, 50, 100]` rows per page (default 20). Search fields trigger `ReloadServerData()` with a 300 ms debounce.

### Dark mode

Managed entirely client-side: `MudThemeProvider` with `@bind-IsDarkMode`; preference persisted across sessions via `localStorage` JS interop.

## DB connection string

`ConnectionStrings__LibraryDb` in `docker-compose.yml`. For local non-Docker development, override in `appsettings.Development.json`:
```json
"ConnectionStrings": {
  "LibraryDb": "Server=localhost,1433;Database=LibraryDB;User Id=sa;Password=StrongPass123!;TrustServerCertificate=True;"
}
```

## Stack

- .NET 8 / Blazor Server with Interactive Server render mode
- MudBlazor 9.4.0 (component library)
- Dapper 2.1 (micro-ORM)
- SQL Server 2022
