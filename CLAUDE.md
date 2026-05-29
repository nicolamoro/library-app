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
docker exec -i $(docker compose ps -q db) psql -U postgres -d librarydb < db/init/01_schema.sql
docker exec -i $(docker compose ps -q db) psql -U postgres -d librarydb < db/init/02_procedures.sql
docker exec -i $(docker compose ps -q db) psql -U postgres -d librarydb < db/init/03_seed.sql
```

### Volume seed (stress test)

`db/seed_volume.sql` inserts ~13,350 additional rows for load testing (14 genres, 50 publishers, 300 authors, 1,000 books, 2,000 users, 10,000 loans). Run it **after** the base seed, on demand:

```sh
# Via Docker (DB container must be running)
docker exec -i $(docker compose ps -q db) psql -U postgres -d librarydb < db/seed_volume.sql

# Or if psql is available locally
psql "host=localhost port=5432 dbname=librarydb user=postgres password=StrongPass123!" -f db/seed_volume.sql
```

The script is **not idempotent**: running it twice raises an error and exits without changes (guard on `isbn LIKE 'VOL%'`). To reset, wipe the volume and restart: `docker compose down -v && docker compose up --build`.

## Architecture

**Blazor Server → Dapper → PostgreSQL** (no service layer).

Razor components inject repositories directly, call `async` methods, and call `StateHasChanged()` where needed. There is no intermediate service or CQRS layer.

```
Browser ──WebSocket──► Blazor Server Circuit
                             │
                   Component Rendering
                             │
                   Repository (Dapper)
                             │
                        PostgreSQL
                   (PL/pgSQL functions)
```

### Key directories

| Path | Purpose |
|---|---|
| `LibraryApp/Components/Pages/` | Razor pages: Home (dashboard), Books/, Users/, Loans/, MyLoans, UserProfile |
| `LibraryApp/Components/Layout/` | Shell: `MainLayout.razor` (AppBar con dropdown utente, Drawer), `NavMenu.razor` |
| `LibraryApp/Data/` | `DapperContext` (connection factory), repositories for Book/Author/Genre/Publisher/User/Loan |
| `LibraryApp/Models/` | POCOs: `Book`, `Author`, `Genre`, `Publisher`, `User`, `LoanDetail` |
| `LibraryApp/Pages/` | Razor Pages: `Login.cshtml`, `Logout.cshtml` (cookie auth) |
| `db/init/` | SQL scripts run by the postgres image on first init: `01_schema.sql` (DDL), `02_procedures.sql` (functions), `03_seed.sql` |

### Database

PostgreSQL 16 (containerized; runs init scripts via `/docker-entrypoint-initdb.d` on first start). Loan lifecycle is managed by two PL/pgSQL functions (`RETURNS TABLE`, invoked via `SELECT * FROM sp_xxx(...)`):
- **`sp_borrow_book`** — validates user status and book availability, inserts loan, decrements `available_copies`
- **`sp_return_book`** — sets `return_date`, computes `fine_amount` if overdue, increments `available_copies`

Both raise their validation errors via `RAISE EXCEPTION` with the same human-readable message text the UI surfaces (e.g. `User account is suspended.`).

Direct SQL queries are written inline in repositories using Dapper raw SQL (no ORM) via the **Npgsql** driver. Npgsql returns `date` columns as `DateTime`, so the custom `DateOnlyTypeHandler` (registered in `Program.cs`) maps them to the models' `DateOnly` properties. Search filters use `ILIKE` (case-insensitive, matching the old SQL Server collation behaviour) and nullable filter/search params use an empty-string sentinel (`@Search = ''`) to avoid Npgsql's untyped-NULL inference errors.

### Pagination and sorting

All list pages use **server-side pagination and sorting** via MudBlazor's `MudTable ServerData` pattern. Each repository exposes a dedicated paged method alongside the original `GetAllAsync`:

| Repository method | Used by | Filter |
|---|---|---|
| `BookRepository.GetPagedAsync(page, pageSize, search?, sortBy?, sortDescending)` | BookList | ILIKE on title / genre / authors |
| `AuthorRepository.GetPagedAsync(page, pageSize, search?, sortBy?, sortDescending)` | AuthorList | ILIKE on first/last name / nationality |
| `GenreRepository.GetPagedAsync(page, pageSize, search?, sortBy?, sortDescending)` | GenreList | ILIKE on name / description |
| `PublisherRepository.GetPagedAsync(page, pageSize, search?, sortBy?, sortDescending)` | PublisherList | ILIKE on name |
| `UserRepository.GetPagedAsync(page, pageSize, search?, sortBy?, sortDescending)` | UserList | ILIKE on full name / email |
| `LoanRepository.GetPagedAsync(page, pageSize, filter, sortBy?, sortDescending)` | LoanList | exact `status` match (`all` = no filter) |
| `LoanRepository.GetOverduePagedAsync(page, pageSize, sortBy?, sortDescending)` | Home dashboard | `status = 'overdue'` or active past due |
| `LoanRepository.GetByUserIdPagedAsync(userId, page, pageSize, sortBy?, sortDescending)` | MyLoans | filter by user_id |

Each method runs **two SQL statements in one round-trip** via `QueryMultipleAsync`: a `COUNT(*)` and a `SELECT … OFFSET @Offset LIMIT @PageSize`. The UI uses `MudTablePager` with options `[10, 20, 50, 100]` rows per page (default 20). Search fields trigger `ReloadServerData()` with a 300 ms debounce.

**Sorting** is implemented via `MudTableSortLabel` on each sortable column header. `TableState.SortLabel` and `TableState.SortDirection` are passed to the repository. Each repository validates the label against an internal whitelist dictionary (`_sortMap`) before interpolating into `ORDER BY`, preventing SQL injection. The `Autori` column (computed `STRING_AGG`) and action columns are not sortable.

### Authentication

Cookie-based authentication (ASP.NET Core `AddCookie`) — **not** ASP.NET Identity.

**Roles:**
- `admin` — full access: Dashboard, Books, Authors, Genres, Publishers, Users CRUD, Loans
- `user` — può visualizzare i propri prestiti a `/my-loans` e modificare il proprio profilo a `/profile`

**Login/logout via Razor Pages** (`LibraryApp/Pages/Login.cshtml`, `Logout.cshtml`). Blazor runs on WebSocket so `Set-Cookie` headers must be issued on a plain HTTP response; Razor Pages handle this. All navigation to `/login` and `/logout` from Blazor uses `NavigationManager.NavigateTo(..., forceLoad: true)`.

**`users` table:**
- `user_id` — PK, used as `NameIdentifier` claim
- `email` — NOT NULL, UNIQUE; used as login identifier; immutable after creation
- `password_hash` — BCrypt work factor 12 (via BCrypt.Net-Next 4.0.3)
- `is_admin` — `TRUE` for admins, `FALSE` for users
- `last_login` — updated on each successful login

**Seed credentials:**
| Email | Password | Role |
|---|---|---|
| `admin@email.it` | `admin123` | admin |
| `mario.rossi@email.it` | `user123` | user |
| `lucia.bianchi@email.it` | `user123` | user |
| `f.conti@email.it` | `user123` | user |
| `giulia.marino@email.it` | `user123` | user |
| `antonio.deluca@email.it` | `user123` | user (suspended) |

**Claims issued at login:** `NameIdentifier` (user_id), `Name` (email), `Role` (admin/user), `user_id` (used by `MyLoans` and `UserProfile` to filter/load data).

**Authorization in Blazor:** `Routes.razor` wraps everything in `<CascadingAuthenticationState>` and uses `<AuthorizeRouteView>`. Unauthenticated users are redirected to `/login`; authenticated users with wrong role see `/access-denied`. All admin pages carry `@attribute [Authorize(Roles = "admin")]`. The `/profile` page uses `@attribute [Authorize]` (no role restriction).

**Self-service profile (`/profile`):** Accessible to all authenticated users. Allows editing of `first_name`, `last_name`, `birth_date`, `tax_code`, `address`, `phone`, and password change. Email, `status`, and `is_admin` are never modifiable via this page — enforced server-side by `UserRepository.UpdateProfileAsync` whose SQL does not include those columns.

### AppBar dropdown menu

The top-right `MudMenu` is triggered by clicking the account icon + email. Menu items: **Profilo** (→ `/profile`), **dark/light mode toggle**, **Esci** (logout). `MudPopoverProvider` in `MainLayout.razor` is required for `MudMenu` to render.

### Dark mode

Managed entirely client-side: `MudThemeProvider` with `@bind-IsDarkMode`; preference persisted across sessions via `localStorage` JS interop.

## DB connection string

Credentials are stored in `.env` (gitignored). Copy `.env.example` to `.env` and fill in the values before starting the stack:

```
DB_USER=postgres
DB_PASSWORD=StrongPass123!
DB_NAME=librarydb
DB_SERVER=db
```

`docker-compose.yml` reads `.env` automatically and injects the variables into both containers:
- `db` receives `POSTGRES_USER` / `POSTGRES_PASSWORD` / `POSTGRES_DB`
- `app` receives `ConnectionStrings__LibraryDb` (built from the four vars above)

ASP.NET Core maps `ConnectionStrings__LibraryDb` to `IConfiguration.GetConnectionString("LibraryDb")` automatically — no code changes needed.

For local non-Docker development, `appsettings.Development.json` provides a localhost fallback (already set to `Host=localhost;Port=5432`). Override it by setting the `ConnectionStrings__LibraryDb` environment variable in your shell if needed.

## Stack

- .NET 10 / Blazor Server with Interactive Server render mode
- MudBlazor 9.4.0 (component library)
- Dapper 2.1 (micro-ORM) + Npgsql
- PostgreSQL 16
