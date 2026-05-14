# Library Management System

Applicazione web per la gestione di una biblioteca: catalogo libri, anagrafica clienti e ciclo di vita dei prestiti con calcolo automatico delle multe per ritardo.

## Stack tecnologico

| Layer | Tecnologia |
|---|---|
| Frontend | Blazor Server (.NET 8), MudBlazor 9.4.0 |
| Backend | ASP.NET Core 8, Dapper 2.1 |
| Database | SQL Server 2022 |
| Deploy | Docker Compose |

## Funzionalità

- **Dashboard** — mostra i prestiti scaduti o in ritardo con multa stimata e collegamento diretto alla restituzione
- **Catalogo libri** — lista ricercabile con filtro testuale, creazione e modifica libri (autori multipli, editore, genere, anno, lingua, copie)
- **Anagrafica clienti** — lista con badge di stato (attivo/sospeso), creazione e modifica, sospensione bloccante per nuovi prestiti
- **Gestione prestiti** — lista filtrabile per stato, nuovo prestito con controllo disponibilità, restituzione con calcolo multa in tempo reale
- **Dark mode** — toggle luna/sole nella barra superiore; la preferenza è persistita in `localStorage`

## Struttura del progetto

```
docker-compose.yml              — avvia l'intero stack con un comando
db/
  Dockerfile                    — immagine SQL Server con init automatico
  entrypoint.sh                 — script di avvio e inizializzazione db
  init/
    01_schema.sql               — DDL: creazione database e tabelle
    02_procedures.sql           — stored procedure prestito/restituzione
    03_seed.sql                 — dati di esempio
  seed_volume.sql               — seed opzionale ad alto volume (stress test)
LibraryApp/
  Dockerfile                    — build multistage .NET 8
  Program.cs                    — entry point, DI, pipeline HTTP
  Components/
    Layout/
      MainLayout.razor          — shell con AppBar, Drawer, dark mode toggle
      NavMenu.razor             — menu di navigazione laterale
    Pages/
      Home.razor                — dashboard prestiti scaduti
      Books/
        BookList.razor          — lista e ricerca libri
        BookForm.razor          — form creazione/modifica libro
      Customers/
        CustomerList.razor      — lista clienti
        CustomerForm.razor      — form creazione/modifica cliente
      Loans/
        LoanList.razor          — lista prestiti con filtro stato
        BorrowBook.razor        — nuovo prestito
        ReturnBook.razor        — restituzione con calcolo multa
  Data/
    DapperContext.cs            — factory connessioni SQL Server
    BookRepository.cs           — CRUD libri, autori, generi, editori
    CustomerRepository.cs       — CRUD clienti
    LoanRepository.cs           — CRUD prestiti, prestiti scaduti
  Models/
    Book.cs, Author.cs, Genre.cs, Publisher.cs
    Customer.cs
    LoanDetail.cs               — include giorni ritardo e multa stimata (computed)
  wwwroot/
    app.css                     — stili globali e overrides dark mode per validation
```

## Architettura applicativa

```
Browser ──WebSocket──► Blazor Server Circuit
                              │
                    Component Rendering
                              │
                    Repository (Dapper)
                              │
                         SQL Server
                    (stored procedure)
```

I componenti Blazor comunicano con SQL Server tramite repository Dapper. Il pattern è diretto (nessun service layer intermedio): i componenti iniettano il repository, chiamano metodi `async`, e aggiornano lo stato locale con `StateHasChanged()` dove necessario.

Il dark mode è gestito interamente lato client: `MudThemeProvider` con `@bind-IsDarkMode` aggiorna le CSS variables MudBlazor; `localStorage` persiste la scelta tra sessioni via JS interop.

---

## Schema del database

### Diagramma delle relazioni

```
genres ──────────┐
                 │ FK
publishers ──────┤
                 │ FK
                 ▼
authors ──── book_authors ──── books
                                 │ FK
customers ──── loans ────────────┘
```

### genres
| Colonna | Tipo | Note |
|---|---|---|
| `genre_id` | INT IDENTITY | PK |
| `name` | NVARCHAR(100) | NOT NULL, UNIQUE |
| `description` | NVARCHAR(500) | opzionale |

### publishers
| Colonna | Tipo | Note |
|---|---|---|
| `publisher_id` | INT IDENTITY | PK |
| `name` | NVARCHAR(200) | NOT NULL |
| `address` | NVARCHAR(300) | |
| `phone` | NVARCHAR(20) | |
| `email` | NVARCHAR(150) | |
| `website` | NVARCHAR(200) | |

### authors
| Colonna | Tipo | Note |
|---|---|---|
| `author_id` | INT IDENTITY | PK |
| `first_name` | NVARCHAR(100) | NOT NULL |
| `last_name` | NVARCHAR(100) | NOT NULL |
| `birth_date` | DATE | |
| `nationality` | NVARCHAR(100) | |
| `biography` | NVARCHAR(MAX) | |

### books
Catalogo dei titoli. `total_copies` / `available_copies` tracciano le copie fisiche.

| Colonna | Tipo | Note |
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

Vincolo: `0 ≤ available_copies ≤ total_copies`.

### book_authors
Relazione N:N tra `books` e `authors`.

| Colonna | Tipo | Note |
|---|---|---|
| `book_id` | INT | PK, FK → books |
| `author_id` | INT | PK, FK → authors |

### customers
| Colonna | Tipo | Note |
|---|---|---|
| `customer_id` | INT IDENTITY | PK |
| `first_name` | NVARCHAR(100) | NOT NULL |
| `last_name` | NVARCHAR(100) | NOT NULL |
| `birth_date` | DATE | |
| `tax_code` | NVARCHAR(20) | UNIQUE |
| `address` | NVARCHAR(300) | |
| `phone` | NVARCHAR(20) | |
| `email` | NVARCHAR(150) | |
| `registration_date` | DATE | NOT NULL, DEFAULT GETDATE() |
| `status` | NVARCHAR(20) | `active` \| `suspended`, DEFAULT `active` |

### loans
| Colonna | Tipo | Note |
|---|---|---|
| `loan_id` | INT IDENTITY | PK |
| `customer_id` | INT | FK → customers |
| `book_id` | INT | FK → books |
| `loan_date` | DATE | NOT NULL, DEFAULT GETDATE() |
| `due_date` | DATE | NOT NULL, deve essere > `loan_date` |
| `return_date` | DATE | NULL se il prestito è ancora aperto |
| `status` | NVARCHAR(20) | `active` \| `returned` \| `overdue`, DEFAULT `active` |
| `daily_fine_rate` | DECIMAL(5,2) | tariffa giornaliera multa, DEFAULT 0.50 |
| `fine_amount` | DECIMAL(8,2) | NULL finché non calcolata |
| `fine_paid` | BIT | 0 = non pagata, 1 = pagata, DEFAULT 0 |

Vincoli: `due_date > loan_date`; `return_date ≥ loan_date` se valorizzata.

---

## Stored Procedure

### sp_borrow_book — Registra un nuovo prestito

```sql
EXEC sp_borrow_book
    @customer_id     = 1,    -- ID cliente
    @book_id         = 3,    -- ID libro
    @loan_days       = 30,   -- durata prestito in giorni (default 30)
    @daily_fine_rate = 0.50  -- tariffa giornaliera multa (default 0.50)
```

Verifica che il cliente sia `active` e che esistano copie disponibili, inserisce il record in `loans` e decrementa `available_copies`.

| Errore | Messaggio |
|---|---|
| 50001 | `Customer not found.` |
| 50002 | `Customer account is suspended.` |
| 50003 | `Book not found.` |
| 50004 | `No copies available for this book.` |

### sp_return_book — Registra la restituzione

```sql
EXEC sp_return_book
    @loan_id = 7   -- ID del prestito da chiudere
```

Imposta `return_date` a oggi, porta lo `status` a `returned`, calcola `fine_amount` se in ritardo e incrementa `available_copies`.

| Errore | Messaggio |
|---|---|
| 50010 | `Loan not found.` |
| 50011 | `This loan has already been returned.` |

---

## Avvio rapido

```sh
docker compose up --build
```

L'app è disponibile su **http://localhost:8080**. Vedi [LOCAL_DEPLOY.md](LOCAL_DEPLOY.md) per tutti i dettagli.

## Eseguire gli script SQL manualmente

```sh
sqlcmd -S localhost -U sa -P "StrongPass123!" -No -i db/init/01_schema.sql
sqlcmd -S localhost -U sa -P "StrongPass123!" -No -d LibraryDB -i db/init/02_procedures.sql
sqlcmd -S localhost -U sa -P "StrongPass123!" -No -d LibraryDB -i db/init/03_seed.sql
```

### Volume seed (stress test)

`db/seed_volume.sql` inserisce ~13.350 righe aggiuntive per test di carico:

| Tabella | Righe aggiunte |
|---|---|
| `genres` | 14 |
| `publishers` | 50 |
| `authors` | 300 |
| `books` | 1.000 + ~1.500 associazioni autori |
| `customers` | 2.000 |
| `loans` | 10.000 (70% restituiti, 20% attivi, 10% scaduti con multa) |

Eseguire **dopo** il seed base, a richiesta:

```sh
# Tramite Docker (il container db deve essere in esecuzione)
docker exec -i $(docker compose ps -q db) \
  /opt/mssql-tools18/bin/sqlcmd -S localhost -U sa -P "StrongPass123!" -C \
  -i /dev/stdin < db/seed_volume.sql

# Oppure con sqlcmd installato localmente
sqlcmd -S localhost -U sa -P "StrongPass123!" -No -d LibraryDB -i db/seed_volume.sql
```

Lo script **non è idempotente**: eseguirlo una seconda volta genera un errore e termina senza modifiche (guardia su `isbn LIKE 'VOL%'`). Per ripartire da zero: `docker compose down -v && docker compose up --build`.

## Query utili

**Libri disponibili per il prestito:**
```sql
SELECT book_id, title, available_copies
FROM books
WHERE available_copies > 0;
```

**Prestiti scaduti con multa calcolata:**
```sql
SELECT
    l.loan_id,
    c.first_name + ' ' + c.last_name AS customer,
    b.title,
    l.due_date,
    DATEDIFF(DAY, l.due_date, CAST(GETDATE() AS DATE)) AS days_overdue,
    DATEDIFF(DAY, l.due_date, CAST(GETDATE() AS DATE)) * l.daily_fine_rate AS calculated_fine
FROM loans l
JOIN customers c ON c.customer_id = l.customer_id
JOIN books     b ON b.book_id     = l.book_id
WHERE l.status = 'overdue'
   OR (l.status = 'active' AND l.due_date < CAST(GETDATE() AS DATE));
```

**Tutti i libri con i rispettivi autori:**
```sql
SELECT
    b.title,
    STRING_AGG(a.first_name + ' ' + a.last_name, ', ') AS authors
FROM books b
JOIN book_authors ba ON ba.book_id   = b.book_id
JOIN authors      a  ON a.author_id  = ba.author_id
GROUP BY b.book_id, b.title;
```
