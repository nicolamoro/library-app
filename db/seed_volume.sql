-- ============================================================
-- Library Management System — High-Volume Seed (stress test)
-- ============================================================
-- Run MANUALLY to populate the DB with a large number of rows
-- and test behaviour under load.
--
-- Volumes inserted:
--   Genres:    14   (DB total ~20)
--   Publishers: 50
--   Authors:   300
--   Books:     1,000 (+ ~1,500 author associations)
--   Users:     2,000
--   Loans:    10,000 (70% returned, 20% active, 10% overdue)
--
-- PREREQUISITE: 03_seed.sql already applied.
-- WARNING:      script is not idempotent — do not run twice
--               (the guard below aborts on a second run).
-- ============================================================

-- The whole seed runs inside one transaction so it is atomic: if the guard
-- below raises (or any insert fails), the transaction aborts and NOTHING is
-- written — no partial / duplicate rows — regardless of psql's ON_ERROR_STOP.
BEGIN;

-- Guard: abort immediately if the volume seed has already been applied
DO $$
BEGIN
    IF EXISTS (SELECT 1 FROM books WHERE isbn LIKE 'VOL%') THEN
        RAISE EXCEPTION 'Volume seed already applied (books with ISBN VOL* found). Exiting without changes.';
    END IF;
END $$;

-- ============================================================
-- 1. GENRES  (14 additional → ~20 total in DB)
-- ============================================================
INSERT INTO genres (name, description) VALUES
    ('Romance',          'Romanzi incentrati su storie d''amore e relazioni'),
    ('Mystery',          'Narrativa poliziesca, gialli e noir'),
    ('Biography',        'Biografie e autobiografie di personaggi celebri'),
    ('Self-Help',        'Libri di crescita personale e sviluppo'),
    ('Travel',           'Diari di viaggio e letteratura geografica'),
    ('Poetry',           'Raccolta di poesie e componimenti lirici'),
    ('Graphic Novel',    'Romanzi grafici e fumetti per adulti'),
    ('Young Adult',      'Narrativa per giovani adulti'),
    ('Children',         'Libri per bambini e ragazzi'),
    ('Essay',            'Saggi e analisi su vari argomenti'),
    ('Philosophy',       'Testi filosofici e pensiero critico'),
    ('History',          'Saggistica storica e ricostruzione del passato'),
    ('Science',          'Divulgazione scientifica'),
    ('Cooking',          'Libri di cucina e ricettari');

-- ============================================================
-- 2. PUBLISHERS  (50)
-- ============================================================
INSERT INTO publishers (name, address, phone, email, website)
SELECT
    'Editore Volume ' || i,
    'Via Prova ' || i || ', Milano',
    '0' || lpad(((i * 111111) % 999999999)::text, 9, '0'),
    'info@editvolume' || i || '.it',
    'www.editvolume' || i || '.it'
FROM generate_series(1, 50) AS i;

-- ============================================================
-- 3. AUTHORS  (300)
-- ============================================================
INSERT INTO authors (first_name, last_name, birth_date, nationality, biography)
SELECT
    'Nome' || i,
    'Cognome' || i,
    DATE '2000-01-01' - (((i * 127) % 21900) + 7300),
    CASE (i % 10)
        WHEN 0 THEN 'Italiana'   WHEN 1 THEN 'Britannica'
        WHEN 2 THEN 'Americana'  WHEN 3 THEN 'Francese'
        WHEN 4 THEN 'Tedesca'    WHEN 5 THEN 'Spagnola'
        WHEN 6 THEN 'Portoghese' WHEN 7 THEN 'Giapponese'
        WHEN 8 THEN 'Russa'      ELSE         'Argentina'
    END,
    'Autore generato per test di volume. Indice: ' || i
FROM generate_series(1, 300) AS i;

-- ============================================================
-- 4. BOOKS  (1,000)  +  BOOK_AUTHORS
--    The volume index "i" is recoverable from the ISBN ('VOL' + 9 digits),
--    which lets the author associations be derived set-based.
-- ============================================================
DO $$
DECLARE
    v_total_genres INT;
    v_total_pubs   INT;
    v_first_auth   INT;   -- first author_id of the 300-row volume batch
BEGIN
    SELECT COUNT(*) INTO v_total_genres FROM genres;
    SELECT COUNT(*) INTO v_total_pubs   FROM publishers;
    SELECT MAX(author_id) - 299 INTO v_first_auth FROM authors;

    INSERT INTO books
        (isbn, title, publisher_id, genre_id, publication_year, language, page_count, total_copies, available_copies)
    SELECT
        'VOL' || lpad(i::text, 9, '0'),
        'Titolo Volume ' || i,
        1 + (i % v_total_pubs),
        1 + (i % v_total_genres),
        1900 + ((i * 7) % 125),
        CASE (i % 5)
            WHEN 0 THEN 'Italiano' WHEN 1 THEN 'Inglese'
            WHEN 2 THEN 'Francese' WHEN 3 THEN 'Tedesco'
            ELSE 'Spagnolo'
        END,
        100 + ((i * 13) % 900),
        10,
        10
    FROM generate_series(1, 1000) AS i;

    -- primary author for every book
    INSERT INTO book_authors (book_id, author_id)
    SELECT b.book_id, v_first_auth + (x.i % 300)
    FROM books b
    CROSS JOIN LATERAL (SELECT substring(b.isbn FROM 4)::int AS i) x
    WHERE b.isbn LIKE 'VOL%';

    -- second author for every other book (if different from the first)
    INSERT INTO book_authors (book_id, author_id)
    SELECT b.book_id, v_first_auth + ((x.i + 151) % 300)
    FROM books b
    CROSS JOIN LATERAL (SELECT substring(b.isbn FROM 4)::int AS i) x
    WHERE b.isbn LIKE 'VOL%'
      AND x.i % 2 = 0
      AND ((x.i + 151) % 300) <> (x.i % 300);
END $$;

-- ============================================================
-- 5. USERS  (2,000)
-- ============================================================
INSERT INTO users
    (first_name, last_name, birth_date, tax_code, address, phone, email, registration_date, status, password_hash)
SELECT
    'Cliente' || i,
    'Rossi'   || i,
    DATE '2000-01-01' - (((i * 97) % 18250) + 6570),
    'VCLTST' || lpad(i::text, 6, '0') || 'Z',
    'Via Test ' || i || ', Milano',
    '3' || lpad(((i * 7919) % 1000000000)::text, 9, '0'),
    'utente' || i || '@testmail.it',
    CURRENT_DATE - ((i * 3) % 1460),
    CASE WHEN i % 20 = 0 THEN 'suspended' ELSE 'active' END,
    '$2a$12$vfhLGjT3/IzLIYn6O/w8Wu..bkp3qQj5vAfaXbgp3P90kTWk7gz6a'
FROM generate_series(1, 2000) AS i;

-- ============================================================
-- 6. LOANS  (10,000)
--    70 % returned  (some with fine, some paid)
--    20 % active    (ongoing, within due date)
--    10 % overdue   (not returned, fine calculated)
-- ============================================================
DO $$
DECLARE
    v_min_user   INT;
    v_max_user   INT;
    v_user_range INT;
    v_min_book   INT;
    v_max_book   INT;
    v_book_range INT;
BEGIN
    SELECT MIN(user_id), MAX(user_id) INTO v_min_user, v_max_user FROM users;
    SELECT MIN(book_id), MAX(book_id) INTO v_min_book, v_max_book FROM books;
    v_user_range := v_max_user - v_min_user + 1;
    v_book_range := v_max_book - v_min_book + 1;

    INSERT INTO loans
        (user_id, book_id, loan_date, due_date, return_date, status, daily_fine_rate, fine_amount, fine_paid)
    SELECT
        v_min_user + (i % v_user_range),
        v_min_book + ((i * 3) % v_book_range),
        ld.loan_date,
        dd.due_date,
        rd.return_date,
        CASE WHEN b.branch < 7 THEN 'returned'
             WHEN b.branch < 9 THEN 'active'
             ELSE 'overdue' END,
        b.rate,
        CASE
            WHEN b.branch < 7 THEN
                CASE WHEN (rd.return_date - dd.due_date) > 0
                     THEN (rd.return_date - dd.due_date) * b.rate
                     ELSE NULL END
            WHEN b.branch < 9 THEN NULL
            ELSE (CURRENT_DATE - dd.due_date) * b.rate
        END,
        CASE WHEN b.branch < 7 AND (rd.return_date - dd.due_date) > 0 AND (i % 2 = 0)
             THEN TRUE ELSE FALSE END
    FROM generate_series(1, 10000) AS i
    CROSS JOIN LATERAL (
        SELECT (i % 10) AS branch,
               (CASE (i % 3) WHEN 0 THEN 0.75 WHEN 1 THEN 0.50 ELSE 1.00 END)::numeric(5,2) AS rate
    ) b
    CROSS JOIN LATERAL (
        SELECT CASE
                   WHEN b.branch < 7 THEN CURRENT_DATE - (((i * 5) % 1825) + 35)
                   WHEN b.branch < 9 THEN CURRENT_DATE - (i % 25)
                   ELSE CURRENT_DATE - (35 + ((i * 3) % 60))
               END AS loan_date
    ) ld
    CROSS JOIN LATERAL (SELECT ld.loan_date + 30 AS due_date) dd
    CROSS JOIN LATERAL (
        SELECT CASE WHEN b.branch < 7 THEN ld.loan_date + (5 + (i % 28))
                    ELSE NULL END AS return_date
    ) rd;
END $$;

-- ============================================================
-- Summary
-- ============================================================
SELECT 'genres'      AS table_name, COUNT(*) AS total_rows FROM genres      UNION ALL
SELECT 'publishers',                COUNT(*)               FROM publishers   UNION ALL
SELECT 'authors',                   COUNT(*)               FROM authors      UNION ALL
SELECT 'books',                     COUNT(*)               FROM books        UNION ALL
SELECT 'book_authors',              COUNT(*)               FROM book_authors UNION ALL
SELECT 'users',                     COUNT(*)               FROM users        UNION ALL
SELECT 'loans',                     COUNT(*)               FROM loans;

COMMIT;
