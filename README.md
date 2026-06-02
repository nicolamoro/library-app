# Library Management System

Web application for managing a library: book catalogue, user registry, and loan lifecycle with automatic late fine calculation.

## Tech stack

| Layer | Technology |
|---|---|
| Frontend | Blazor Server (.NET 10), MudBlazor 9.4.0 |
| Backend | ASP.NET Core 10, Dapper 2.1, Npgsql |
| Database | PostgreSQL 16 |
| Deploy | Docker Compose |

## Features

- **Dashboard** — shows overdue or late loans with estimated fine and a direct link to return them
- **Book catalogue** — searchable list with text filter and column sorting, book creation and editing (multiple authors, publisher, genre, year, language, copies)
- **Author management** — searchable list by name and nationality, creation and editing with biography and birth date; deletion blocked if the author is linked to books
- **Genre management** — searchable list, creation and editing; deletion blocked if the genre is linked to books
- **Publisher management** — searchable list, creation and editing with full contact details; deletion blocked if the publisher is linked to books
- **User registry** — list with status badge (active/suspended) and column sorting, creation and editing, suspension blocks new loans
- **Loan management** — status-filterable list with column sorting, new loan with availability check, return with real-time fine calculation
- **My loans** — user's loan history with column sorting
- **User profile** — self-service page (`/profile`) for all authenticated users: edit personal data (first name, last name, phone, tax code, address, birth date) and change password; email is read-only and `status`/`is_admin` are never modifiable (enforced server-side)
- **Authentication** — email and password login (BCrypt), cookie session, two roles: `admin` (full access) and `user` (My Loans + Profile)
- **AppBar user menu** — clicking the account icon/email opens a dropdown with Profile, dark/light mode toggle, and Logout
- **Dark mode** — toggle in the AppBar user menu; preference persisted in `localStorage`

## Project structure

```
docker-compose.yml              — local dev: full stack with a single command
docker-compose.prod.yml         — production compose (pulled from S3 by CD pipeline)
db/
  init/                         — run by the postgres image on first start (/docker-entrypoint-initdb.d)
    01_schema.sql               — DDL: table creation
    02_procedures.sql           — borrow/return PL/pgSQL functions
    03_seed.sql                 — sample data
  seed_volume.sql               — optional high-volume seed (stress test)
infra/
  terraform/
    main.tf                     — EC2, Security Group, Elastic IP
    ecr.tf                      — ECR repository + lifecycle policy
    iam.tf                      — instance profile with SSM/ECR/S3/SSM-params permissions
    backend.tf                  — S3 + DynamoDB remote state backend
    variables.tf                — input variables
    outputs.tf                  — elastic_ip, instance_id, ecr_registry
    user_data.sh                — EC2 bootstrap: Docker install, swap, SSM agent
.github/
  workflows/
    ci.yml                      — build, test, push sha-<hash> image to GHCR
    cd.yml                      — promote image to ECR, sync assets to S3, deploy via SSM
    infra.yml                   — manual: bootstrap / apply / destroy infrastructure
    ghcr-cleanup.yml            — prune old GHCR images
  actions/
    tf-setup/                   — composite action: AWS credentials + Terraform init
LibraryApp/
  Dockerfile                    — multistage .NET 10 build
  Program.cs                    — entry point, DI, HTTP pipeline
  Components/
    Routes.razor                — AuthorizeRouteView with cascading auth state
    RedirectToLogin.razor       — redirects unauthenticated users to /login
    RedirectToAccessDenied.razor — redirects unauthorized users to /access-denied
    Layout/
      MainLayout.razor          — shell with AppBar (user dropdown), Drawer, dark mode toggle
      NavMenu.razor             — side navigation menu
    Pages/
      Home.razor                — overdue loans dashboard
      AccessDenied.razor        — 403 page
      Error.razor               — error page
      Books/
        BookList.razor          — book list and search
        BookForm.razor          — book create/edit form
      Authors/
        AuthorList.razor        — author list and search
        AuthorForm.razor        — author create/edit form
      Genres/
        GenreList.razor         — genre list and search
        GenreForm.razor         — genre create/edit form
      Publishers/
        PublisherList.razor     — publisher list and search
        PublisherForm.razor     — publisher create/edit form
      Users/
        UserList.razor          — user list
        UserForm.razor          — user create/edit form
      Loans/
        LoanList.razor          — loan list with status filter
        BorrowBook.razor        — new loan
        ReturnBook.razor        — return with fine calculation
      MyLoans.razor             — user's own loan history
      UserProfile.razor         — self-service profile editing (all authenticated users)
  Pages/
    Login.cshtml / .cs          — cookie login (plain HTTP response for Set-Cookie)
    Logout.cshtml / .cs         — cookie logout
  Data/
    DapperContext.cs            — PostgreSQL (Npgsql) connection factory
    DateOnlyTypeHandler.cs      — Dapper type handler: Npgsql date → DateOnly
    IBookRepository.cs          — book repository interface
    BookRepository.cs           — book CRUD + author/genre/publisher lookups for dropdowns
    IAuthorRepository.cs        — author repository interface
    AuthorRepository.cs         — author CRUD
    IGenreRepository.cs         — genre repository interface
    GenreRepository.cs          — genre CRUD
    IPublisherRepository.cs     — publisher repository interface
    PublisherRepository.cs      — publisher CRUD
    IUserRepository.cs          — user repository interface
    UserRepository.cs           — user CRUD
    ILoanRepository.cs          — loan repository interface
    LoanRepository.cs           — loan CRUD, overdue loans
  Models/
    Book.cs, Author.cs, Genre.cs, Publisher.cs
    User.cs
    LoanDetail.cs               — includes days overdue and estimated fine (computed)
  wwwroot/
    app.css                     — global styles and dark mode overrides for validation
LibraryApp.Tests/
  Unit/
    LoanDetailTests.cs          — model unit tests (xUnit)
  Integration/
    Fixtures/
      PostgresFixture.cs        — shared Testcontainers PostgreSQL fixture
    BookRepositoryTests.cs      — repository integration tests
    LoanRepositoryTests.cs
    UserRepositoryTests.cs
  Component/
    BookListTests.cs            — Blazor component tests (bUnit + NSubstitute)
    LoanListTests.cs
    MyLoansTests.cs
    UserProfileTests.cs
```

## Application architecture

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

Blazor components communicate with PostgreSQL through Dapper repositories. The pattern is direct (no intermediate service layer): components inject the repository, call `async` methods, and update local state with `StateHasChanged()` where needed.

Dark mode is managed entirely client-side: `MudThemeProvider` with `@bind-IsDarkMode` updates MudBlazor CSS variables; `localStorage` persists the preference across sessions via JS interop.

---

## Database schema

### Relationship diagram

```
genres ──────────┐
                 │ FK
publishers ──────┤
                 │ FK
                 ▼
authors ──── book_authors ──── books
                                 │ FK
users ────── loans ──────────────┘
```

### genres
| Column | Type | Notes |
|---|---|---|
| `genre_id` | INT GENERATED ALWAYS AS IDENTITY | PK |
| `name` | VARCHAR(100) | NOT NULL, UNIQUE |
| `description` | VARCHAR(500) | optional |

### publishers
| Column | Type | Notes |
|---|---|---|
| `publisher_id` | INT GENERATED ALWAYS AS IDENTITY | PK |
| `name` | VARCHAR(200) | NOT NULL |
| `address` | VARCHAR(300) | |
| `phone` | VARCHAR(20) | |
| `email` | VARCHAR(150) | |
| `website` | VARCHAR(200) | |

### authors
| Column | Type | Notes |
|---|---|---|
| `author_id` | INT GENERATED ALWAYS AS IDENTITY | PK |
| `first_name` | VARCHAR(100) | NOT NULL |
| `last_name` | VARCHAR(100) | NOT NULL |
| `birth_date` | DATE | |
| `nationality` | VARCHAR(100) | |
| `biography` | TEXT | |

### books
Book catalogue. `total_copies` / `available_copies` track physical copies.

| Column | Type | Notes |
|---|---|---|
| `book_id` | INT GENERATED ALWAYS AS IDENTITY | PK |
| `isbn` | VARCHAR(20) | UNIQUE |
| `title` | VARCHAR(300) | NOT NULL |
| `publisher_id` | INT | FK → publishers |
| `genre_id` | INT | FK → genres |
| `publication_year` | SMALLINT | |
| `language` | VARCHAR(50) | |
| `page_count` | SMALLINT | |
| `total_copies` | SMALLINT | NOT NULL, DEFAULT 1 |
| `available_copies` | SMALLINT | NOT NULL, DEFAULT 1 |

Constraint: `0 ≤ available_copies ≤ total_copies`.

### book_authors
N:N relationship between `books` and `authors`.

| Column | Type | Notes |
|---|---|---|
| `book_id` | INT | PK, FK → books |
| `author_id` | INT | PK, FK → authors |

### users
| Column | Type | Notes |
|---|---|---|
| `user_id` | INT GENERATED ALWAYS AS IDENTITY | PK |
| `first_name` | VARCHAR(100) | NOT NULL |
| `last_name` | VARCHAR(100) | NOT NULL |
| `birth_date` | DATE | |
| `tax_code` | VARCHAR(20) | UNIQUE |
| `address` | VARCHAR(300) | |
| `phone` | VARCHAR(20) | |
| `email` | VARCHAR(150) | NOT NULL, UNIQUE — used as login identifier |
| `registration_date` | DATE | NOT NULL, DEFAULT CURRENT_DATE |
| `status` | VARCHAR(20) | `active` \| `suspended`, DEFAULT `active` |
| `password_hash` | VARCHAR(100) | BCrypt work factor 12 |
| `is_admin` | BOOLEAN | NOT NULL, DEFAULT FALSE |
| `last_login` | TIMESTAMP | updated on each login |

### loans
| Column | Type | Notes |
|---|---|---|
| `loan_id` | INT GENERATED ALWAYS AS IDENTITY | PK |
| `user_id` | INT | FK → users |
| `book_id` | INT | FK → books |
| `loan_date` | DATE | NOT NULL, DEFAULT CURRENT_DATE |
| `due_date` | DATE | NOT NULL, must be > `loan_date` |
| `return_date` | DATE | NULL while the loan is still open |
| `status` | VARCHAR(20) | `active` \| `returned` \| `overdue`, DEFAULT `active` |
| `daily_fine_rate` | NUMERIC(5,2) | daily fine rate, DEFAULT 0.50 |
| `fine_amount` | NUMERIC(8,2) | NULL until calculated |
| `fine_paid` | BOOLEAN | FALSE = unpaid, TRUE = paid, DEFAULT FALSE |

Constraints: `due_date > loan_date`; `return_date ≥ loan_date` if set.

---

## Database functions (PL/pgSQL)

### sp_borrow_book — Records a new loan

```sql
SELECT * FROM sp_borrow_book(
    1,     -- user ID
    3,     -- book ID
    30,    -- loan duration in days (default 30)
    0.50   -- daily fine rate (default 0.50)
);
```

Verifies that the user is `active` and that available copies exist, inserts the record into `loans`, and decrements `available_copies`. On failure it raises an exception whose message is surfaced to the UI:

| Raised message |
|---|
| `User not found.` |
| `User account is suspended.` |
| `Book not found.` |
| `No copies available for this book.` |

### sp_return_book — Records a return

```sql
SELECT * FROM sp_return_book(7);   -- ID of the loan to close
```

Sets `return_date` to today, changes `status` to `returned`, calculates `fine_amount` if overdue, and increments `available_copies`.

| Raised message |
|---|
| `Loan not found.` |
| `This loan has already been returned.` |

---

## Testing

### Running the tests

```sh
# All tests (Docker required for integration tests — Testcontainers pulls PostgreSQL automatically)
dotnet test LibraryApp.Tests/

# Unit tests only (no Docker required)
dotnet test LibraryApp.Tests/ --filter "Category=Unit"

# Integration tests only
dotnet test LibraryApp.Tests/ --filter "Category=Integration"

# Component tests only (bUnit, no Docker required)
dotnet test LibraryApp.Tests/ --filter "Category=Component"
```

### Test structure

| Folder | Type | Frameworks |
|---|---|---|
| `Unit/` | Model unit tests | xUnit |
| `Integration/` | Repository integration tests | xUnit + Testcontainers |
| `Component/` | Blazor component tests | bUnit + NSubstitute |

Integration tests start an ephemeral PostgreSQL container via **Testcontainers**: no manual setup required, but Docker must be running. The container is shared within a test run and torn down automatically.

---

## Quick start

```sh
docker compose up --build
```

The app is available at **http://localhost:8080**. See [LOCAL_DEPLOY.md](LOCAL_DEPLOY.md) for full details.

## Running SQL scripts manually

```sh
# Against the running container (psql is bundled in the postgres image)
docker exec -i $(docker compose ps -q db) psql -U postgres -d librarydb < db/init/01_schema.sql
docker exec -i $(docker compose ps -q db) psql -U postgres -d librarydb < db/init/02_procedures.sql
docker exec -i $(docker compose ps -q db) psql -U postgres -d librarydb < db/init/03_seed.sql

# Or with psql installed locally
psql "host=localhost port=5432 dbname=librarydb user=postgres password=StrongPass123!" -f db/init/01_schema.sql
```

### Volume seed (stress test)

`db/seed_volume.sql` inserts ~13,350 additional rows for load testing:

| Table | Rows added |
|---|---|
| `genres` | 14 |
| `publishers` | 50 |
| `authors` | 300 |
| `books` | 1,000 + ~1,500 author associations |
| `users` | 2,000 |
| `loans` | 10,000 (70% returned, 20% active, 10% overdue with fine) |

Run **after** the base seed, on demand:

```sh
# Via Docker (DB container must be running)
docker exec -i $(docker compose ps -q db) psql -U postgres -d librarydb < db/seed_volume.sql

# Or with psql installed locally
psql "host=localhost port=5432 dbname=librarydb user=postgres password=StrongPass123!" -f db/seed_volume.sql
```

The script is **not idempotent**: running it a second time raises an error and exits without changes (guard on `isbn LIKE 'VOL%'`). To start from scratch: `docker compose down -v && docker compose up --build`.

## Useful queries

**Books available for borrowing:**
```sql
SELECT book_id, title, available_copies
FROM books
WHERE available_copies > 0;
```

**Overdue loans with calculated fine:**
```sql
SELECT
    l.loan_id,
    u.first_name || ' ' || u.last_name AS user_full_name,
    b.title,
    l.due_date,
    (CURRENT_DATE - l.due_date) AS days_overdue,
    (CURRENT_DATE - l.due_date) * l.daily_fine_rate AS calculated_fine
FROM loans l
JOIN users u ON u.user_id = l.user_id
JOIN books b ON b.book_id = l.book_id
WHERE l.status = 'overdue'
   OR (l.status = 'active' AND l.due_date < CURRENT_DATE);
```

**All books with their authors:**
```sql
SELECT
    b.title,
    STRING_AGG(a.first_name || ' ' || a.last_name, ', ') AS authors
FROM books b
JOIN book_authors ba ON ba.book_id   = b.book_id
JOIN authors      a  ON a.author_id  = ba.author_id
GROUP BY b.book_id, b.title;
```
