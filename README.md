# Library Management System

Web application for managing a library: book catalogue, user registry, and loan lifecycle with automatic late fine calculation.

## Tech stack

| Layer | Technology |
|---|---|
| Frontend | Blazor Server (.NET 10), MudBlazor 9.4.0 |
| Backend | ASP.NET Core 10, Dapper 2.1 |
| Database | SQL Server 2022 |
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
docker-compose.yml              — starts the full stack with a single command
db/
  Dockerfile                    — SQL Server image with automatic init
  entrypoint.sh                 — startup and database initialisation script
  init/
    01_schema.sql               — DDL: database and table creation
    02_procedures.sql           — borrow/return stored procedures
    03_seed.sql                 — sample data
  seed_volume.sql               — optional high-volume seed (stress test)
LibraryApp/
  Dockerfile                    — multistage .NET 10 build
  Program.cs                    — entry point, DI, HTTP pipeline
  Components/
    Layout/
      MainLayout.razor          — shell with AppBar, Drawer, dark mode toggle
      NavMenu.razor             — side navigation menu
    Pages/
      Home.razor                — overdue loans dashboard
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
  Data/
    DapperContext.cs            — SQL Server connection factory
    BookRepository.cs           — book CRUD + author/genre/publisher lookups for dropdowns
    AuthorRepository.cs         — author CRUD
    GenreRepository.cs          — genre CRUD
    PublisherRepository.cs      — publisher CRUD
    UserRepository.cs           — user CRUD
    LoanRepository.cs           — loan CRUD, overdue loans
  Models/
    Book.cs, Author.cs, Genre.cs, Publisher.cs
    User.cs
    LoanDetail.cs               — includes days overdue and estimated fine (computed)
  wwwroot/
    app.css                     — global styles and dark mode overrides for validation
```

## Application architecture

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

Blazor components communicate with SQL Server through Dapper repositories. The pattern is direct (no intermediate service layer): components inject the repository, call `async` methods, and update local state with `StateHasChanged()` where needed.

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
| `genre_id` | INT IDENTITY | PK |
| `name` | NVARCHAR(100) | NOT NULL, UNIQUE |
| `description` | NVARCHAR(500) | optional |

### publishers
| Column | Type | Notes |
|---|---|---|
| `publisher_id` | INT IDENTITY | PK |
| `name` | NVARCHAR(200) | NOT NULL |
| `address` | NVARCHAR(300) | |
| `phone` | NVARCHAR(20) | |
| `email` | NVARCHAR(150) | |
| `website` | NVARCHAR(200) | |

### authors
| Column | Type | Notes |
|---|---|---|
| `author_id` | INT IDENTITY | PK |
| `first_name` | NVARCHAR(100) | NOT NULL |
| `last_name` | NVARCHAR(100) | NOT NULL |
| `birth_date` | DATE | |
| `nationality` | NVARCHAR(100) | |
| `biography` | NVARCHAR(MAX) | |

### books
Book catalogue. `total_copies` / `available_copies` track physical copies.

| Column | Type | Notes |
|---|---|---|
| `book_id` | INT IDENTITY | PK |
| `isbn` | NVARCHAR(20) | UNIQUE |
| `title` | NVARCHAR(300) | NOT NULL |
| `publisher_id` | INT | FK → publishers |
| `genre_id` | INT | FK → genres |
| `publication_year` | SMALLINT | |
| `language` | NVARCHAR(50) | |
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
| `user_id` | INT IDENTITY | PK |
| `first_name` | NVARCHAR(100) | NOT NULL |
| `last_name` | NVARCHAR(100) | NOT NULL |
| `birth_date` | DATE | |
| `tax_code` | NVARCHAR(20) | UNIQUE |
| `address` | NVARCHAR(300) | |
| `phone` | NVARCHAR(20) | |
| `email` | NVARCHAR(150) | NOT NULL, UNIQUE — used as login identifier |
| `registration_date` | DATE | NOT NULL, DEFAULT GETDATE() |
| `status` | NVARCHAR(20) | `active` \| `suspended`, DEFAULT `active` |
| `password_hash` | NVARCHAR(100) | BCrypt work factor 12 |
| `is_admin` | BIT | NOT NULL, DEFAULT 0 |
| `last_login` | DATETIME2 | updated on each login |

### loans
| Column | Type | Notes |
|---|---|---|
| `loan_id` | INT IDENTITY | PK |
| `user_id` | INT | FK → users |
| `book_id` | INT | FK → books |
| `loan_date` | DATE | NOT NULL, DEFAULT GETDATE() |
| `due_date` | DATE | NOT NULL, must be > `loan_date` |
| `return_date` | DATE | NULL while the loan is still open |
| `status` | NVARCHAR(20) | `active` \| `returned` \| `overdue`, DEFAULT `active` |
| `daily_fine_rate` | DECIMAL(5,2) | daily fine rate, DEFAULT 0.50 |
| `fine_amount` | DECIMAL(8,2) | NULL until calculated |
| `fine_paid` | BIT | 0 = unpaid, 1 = paid, DEFAULT 0 |

Constraints: `due_date > loan_date`; `return_date ≥ loan_date` if set.

---

## Stored Procedures

### sp_borrow_book — Records a new loan

```sql
EXEC sp_borrow_book
    @user_id         = 1,    -- user ID
    @book_id         = 3,    -- book ID
    @loan_days       = 30,   -- loan duration in days (default 30)
    @daily_fine_rate = 0.50  -- daily fine rate (default 0.50)
```

Verifies that the user is `active` and that available copies exist, inserts the record into `loans`, and decrements `available_copies`.

| Error | Message |
|---|---|
| 50001 | `User not found.` |
| 50002 | `User account is suspended.` |
| 50003 | `Book not found.` |
| 50004 | `No copies available for this book.` |

### sp_return_book — Records a return

```sql
EXEC sp_return_book
    @loan_id = 7   -- ID of the loan to close
```

Sets `return_date` to today, changes `status` to `returned`, calculates `fine_amount` if overdue, and increments `available_copies`.

| Error | Message |
|---|---|
| 50010 | `Loan not found.` |
| 50011 | `This loan has already been returned.` |

---

## Testing

### Running the tests

```sh
# All tests (Docker required for integration tests — Testcontainers pulls SQL Server automatically)
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

Integration tests start an ephemeral SQL Server container via **Testcontainers**: no manual setup required, but Docker must be running. The container is shared within a test run and torn down automatically.

---

## Quick start

```sh
docker compose up --build
```

The app is available at **http://localhost:8080**. See [LOCAL_DEPLOY.md](LOCAL_DEPLOY.md) for full details.

## Running SQL scripts manually

```sh
sqlcmd -S localhost -U sa -P "StrongPass123!" -No -i db/init/01_schema.sql
sqlcmd -S localhost -U sa -P "StrongPass123!" -No -d LibraryDB -i db/init/02_procedures.sql
sqlcmd -S localhost -U sa -P "StrongPass123!" -No -d LibraryDB -i db/init/03_seed.sql
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
docker exec -i $(docker compose ps -q db) \
  /opt/mssql-tools18/bin/sqlcmd -S localhost -U sa -P "StrongPass123!" -C \
  -i /dev/stdin < db/seed_volume.sql

# Or with sqlcmd installed locally
sqlcmd -S localhost -U sa -P "StrongPass123!" -No -d LibraryDB -i db/seed_volume.sql
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
    u.first_name + ' ' + u.last_name AS user_full_name,
    b.title,
    l.due_date,
    DATEDIFF(DAY, l.due_date, CAST(GETDATE() AS DATE)) AS days_overdue,
    DATEDIFF(DAY, l.due_date, CAST(GETDATE() AS DATE)) * l.daily_fine_rate AS calculated_fine
FROM loans l
JOIN users u ON u.user_id = l.user_id
JOIN books b ON b.book_id = l.book_id
WHERE l.status = 'overdue'
   OR (l.status = 'active' AND l.due_date < CAST(GETDATE() AS DATE));
```

**All books with their authors:**
```sql
SELECT
    b.title,
    STRING_AGG(a.first_name + ' ' + a.last_name, ', ') AS authors
FROM books b
JOIN book_authors ba ON ba.book_id   = b.book_id
JOIN authors      a  ON a.author_id  = ba.author_id
GROUP BY b.book_id, b.title;
```
