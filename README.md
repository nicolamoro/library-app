# Library Management System

Applicazione web per la gestione di una biblioteca: catalogo libri, anagrafica utenti e ciclo di vita dei prestiti con calcolo automatico delle multe per ritardo.

## Stack tecnologico

| Layer | Tecnologia |
|---|---|
| Frontend | Blazor Server (.NET 10), MudBlazor 9.4.0 |
| Backend | ASP.NET Core 10, Dapper 2.1 |
| Database | SQL Server 2022 |
| Deploy | Docker Compose |

## Funzionalità

- **Dashboard** — mostra i prestiti scaduti o in ritardo con multa stimata e collegamento diretto alla restituzione
- **Catalogo libri** — lista ricercabile con filtro testuale e ordinamento per colonna, creazione e modifica libri (autori multipli, editore, genere, anno, lingua, copie)
- **Gestione autori** — lista ricercabile per nome e nazionalità, creazione e modifica con biografia e data di nascita; eliminazione bloccata se l'autore è associato a libri
- **Gestione generi** — lista ricercabile, creazione e modifica; eliminazione bloccata se il genere è associato a libri
- **Gestione editori** — lista ricercabile, creazione e modifica con recapiti completi; eliminazione bloccata se l'editore è associato a libri
- **Anagrafica utenti** — lista con badge di stato (attivo/sospeso) e ordinamento per colonna, creazione e modifica, sospensione bloccante per nuovi prestiti
- **Gestione prestiti** — lista filtrabile per stato con ordinamento per colonna, nuovo prestito con controllo disponibilità, restituzione con calcolo multa in tempo reale
- **I miei prestiti** — storico prestiti dell'utente con ordinamento per colonna
- **Autenticazione** — login con email e password (BCrypt), cookie session, due ruoli: `admin` (accesso completo) e `user` (solo "I miei prestiti")
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
  Dockerfile                    — build multistage .NET 10
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
      Authors/
        AuthorList.razor        — lista e ricerca autori
        AuthorForm.razor        — form creazione/modifica autore
      Genres/
        GenreList.razor         — lista e ricerca generi
        GenreForm.razor         — form creazione/modifica genere
      Publishers/
        PublisherList.razor     — lista e ricerca editori
        PublisherForm.razor     — form creazione/modifica editore
      Users/
        UserList.razor          — lista utenti
        UserForm.razor          — form creazione/modifica utente
      Loans/
        LoanList.razor          — lista prestiti con filtro stato
        BorrowBook.razor        — nuovo prestito
        ReturnBook.razor        — restituzione con calcolo multa
  Data/
    DapperContext.cs            — factory connessioni SQL Server
    BookRepository.cs           — CRUD libri + lookup autori/generi/editori per dropdown
    AuthorRepository.cs         — CRUD autori
    GenreRepository.cs          — CRUD generi
    PublisherRepository.cs      — CRUD editori
    UserRepository.cs           — CRUD utenti
    LoanRepository.cs           — CRUD prestiti, prestiti scaduti
  Models/
    Book.cs, Author.cs, Genre.cs, Publisher.cs
    User.cs
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
users ────── loans ──────────────┘
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

### users
| Colonna | Tipo | Note |
|---|---|---|
| `user_id` | INT IDENTITY | PK |
| `first_name` | NVARCHAR(100) | NOT NULL |
| `last_name` | NVARCHAR(100) | NOT NULL |
| `birth_date` | DATE | |
| `tax_code` | NVARCHAR(20) | UNIQUE |
| `address` | NVARCHAR(300) | |
| `phone` | NVARCHAR(20) | |
| `email` | NVARCHAR(150) | NOT NULL, UNIQUE — usato come login |
| `registration_date` | DATE | NOT NULL, DEFAULT GETDATE() |
| `status` | NVARCHAR(20) | `active` \| `suspended`, DEFAULT `active` |
| `password_hash` | NVARCHAR(100) | BCrypt work factor 12 |
| `is_admin` | BIT | NOT NULL, DEFAULT 0 |
| `last_login` | DATETIME2 | aggiornato ad ogni login |

### loans
| Colonna | Tipo | Note |
|---|---|---|
| `loan_id` | INT IDENTITY | PK |
| `user_id` | INT | FK → users |
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
    @user_id         = 1,    -- ID utente
    @book_id         = 3,    -- ID libro
    @loan_days       = 30,   -- durata prestito in giorni (default 30)
    @daily_fine_rate = 0.50  -- tariffa giornaliera multa (default 0.50)
```

Verifica che l'utente sia `active` e che esistano copie disponibili, inserisce il record in `loans` e decrementa `available_copies`.

| Errore | Messaggio |
|---|---|
| 50001 | `User not found.` |
| 50002 | `User account is suspended.` |
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
| `users` | 2.000 |
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
