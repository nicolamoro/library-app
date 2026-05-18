-- ============================================================
-- Library Management System — High-Volume Seed (stress test)
-- ============================================================
:on error exit
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
--               (generates UNIQUE constraint violations on isbn and tax_code).
-- ============================================================

USE LibraryDB;
GO

SET QUOTED_IDENTIFIER ON;
GO

-- Guard: exit immediately if the volume seed has already been applied
IF EXISTS (SELECT 1 FROM books WHERE isbn LIKE 'VOL%')
BEGIN
    RAISERROR('Volume seed already applied (books with ISBN VOL* found). Exiting without changes.', 16, 1);
    RETURN;
END
GO

-- ============================================================
-- 1. GENRES  (14 additional → ~20 total in DB)
-- ============================================================
INSERT INTO genres (name, description) VALUES
    (N'Romance',          N'Romanzi incentrati su storie d''amore e relazioni'),
    (N'Mystery',          N'Narrativa poliziesca, gialli e noir'),
    (N'Biography',        N'Biografie e autobiografie di personaggi celebri'),
    (N'Self-Help',        N'Libri di crescita personale e sviluppo'),
    (N'Travel',           N'Diari di viaggio e letteratura geografica'),
    (N'Poetry',           N'Raccolta di poesie e componimenti lirici'),
    (N'Graphic Novel',    N'Romanzi grafici e fumetti per adulti'),
    (N'Young Adult',      N'Narrativa per giovani adulti'),
    (N'Children',         N'Libri per bambini e ragazzi'),
    (N'Essay',            N'Saggi e analisi su vari argomenti'),
    (N'Philosophy',       N'Testi filosofici e pensiero critico'),
    (N'History',          N'Saggistica storica e ricostruzione del passato'),
    (N'Science',          N'Divulgazione scientifica'),
    (N'Cooking',          N'Libri di cucina e ricettari');
GO

PRINT 'Genres inserted.';
GO

-- ============================================================
-- 2. PUBLISHERS  (50)
-- ============================================================
DECLARE @i INT = 1;
WHILE @i <= 50
BEGIN
    INSERT INTO publishers (name, address, phone, email, website)
    VALUES (
        N'Editore Volume ' + CAST(@i AS NVARCHAR(10)),
        N'Via Prova ' + CAST(@i AS NVARCHAR(10)) + N', Milano',
        N'0' + RIGHT(N'000000000' + CAST((@i * 111111) % 999999999 AS NVARCHAR(10)), 9),
        N'info@editvolume' + CAST(@i AS NVARCHAR(10)) + N'.it',
        N'www.editvolume' + CAST(@i AS NVARCHAR(10)) + N'.it'
    );
    SET @i = @i + 1;
END
GO

PRINT 'Publishers inserted.';
GO

-- ============================================================
-- 3. AUTHORS  (300)
-- ============================================================
DECLARE @i INT = 1;
WHILE @i <= 300
BEGIN
    INSERT INTO authors (first_name, last_name, birth_date, nationality, biography)
    VALUES (
        N'Nome' + CAST(@i AS NVARCHAR(10)),
        N'Cognome' + CAST(@i AS NVARCHAR(10)),
        DATEADD(DAY, -(((@i * 127) % 21900) + 7300), '2000-01-01'),
        CASE (@i % 10)
            WHEN 0 THEN N'Italiana'   WHEN 1 THEN N'Britannica'
            WHEN 2 THEN N'Americana'  WHEN 3 THEN N'Francese'
            WHEN 4 THEN N'Tedesca'    WHEN 5 THEN N'Spagnola'
            WHEN 6 THEN N'Portoghese' WHEN 7 THEN N'Giapponese'
            WHEN 8 THEN N'Russa'      ELSE         N'Argentina'
        END,
        N'Autore generato per test di volume. Indice: ' + CAST(@i AS NVARCHAR(10))
    );
    SET @i = @i + 1;
END
GO

PRINT 'Authors inserted.';
GO

-- ============================================================
-- 4. BOOKS  (1,000)  +  BOOK_AUTHORS
-- ============================================================
DECLARE @i          INT;
DECLARE @bookId     INT;
DECLARE @authId     INT;
DECLARE @auth2Id    INT;
DECLARE @totalGenres  INT;
DECLARE @totalPubs    INT;
DECLARE @firstAuth    INT;
DECLARE @lastAuth     INT;
DECLARE @authRange    INT;

SELECT @totalGenres = COUNT(*) FROM genres;
SELECT @totalPubs   = COUNT(*) FROM publishers;
-- the 300 authors just inserted are the last rows in the table
SELECT @lastAuth = MAX(author_id) FROM authors;
SET @firstAuth = @lastAuth - 299;   -- primo autore del batch volume
SET @authRange = 300;

SET @i = 1;
WHILE @i <= 1000
BEGIN
    INSERT INTO books
        (isbn, title, publisher_id, genre_id, publication_year, language, page_count, total_copies, available_copies)
    VALUES (
        N'VOL' + RIGHT(N'000000000' + CAST(@i AS NVARCHAR(9)), 9),
        N'Titolo Volume ' + CAST(@i AS NVARCHAR(10)),
        1 + (@i % @totalPubs),
        1 + (@i % @totalGenres),
        1900 + ((@i * 7) % 125),
        CASE (@i % 5)
            WHEN 0 THEN N'Italiano' WHEN 1 THEN N'Inglese'
            WHEN 2 THEN N'Francese' WHEN 3 THEN N'Tedesco'
            ELSE N'Spagnolo'
        END,
        100 + ((@i * 13) % 900),
        10,
        10
    );
    SET @bookId = SCOPE_IDENTITY();

    -- autore principale
    SET @authId = @firstAuth + (@i % @authRange);
    INSERT INTO book_authors (book_id, author_id) VALUES (@bookId, @authId);

    -- second author for every other book (if different from the first)
    IF @i % 2 = 0
    BEGIN
        SET @auth2Id = @firstAuth + ((@i + 151) % @authRange);
        IF @auth2Id <> @authId
            INSERT INTO book_authors (book_id, author_id) VALUES (@bookId, @auth2Id);
    END

    SET @i = @i + 1;
END
GO

PRINT 'Books and author associations inserted.';
GO

-- ============================================================
-- 5. USERS  (2,000)
-- ============================================================
DECLARE @i INT = 1;
WHILE @i <= 2000
BEGIN
    INSERT INTO users
        (first_name, last_name, birth_date, tax_code, address, phone, email, registration_date, status, password_hash)
    VALUES (
        N'Cliente' + CAST(@i AS NVARCHAR(10)),
        N'Rossi'   + CAST(@i AS NVARCHAR(10)),
        DATEADD(DAY, -(((@i * 97) % 18250) + 6570), '2000-01-01'),
        N'VCLTST' + RIGHT(N'000000' + CAST(@i AS NVARCHAR(6)), 6) + N'Z',
        N'Via Test ' + CAST(@i AS NVARCHAR(10)) + N', Milano',
        N'3' + RIGHT(N'000000000' + CAST((@i * 7919) % 1000000000 AS NVARCHAR(10)), 9),
        N'utente' + CAST(@i AS NVARCHAR(10)) + N'@testmail.it',
        DATEADD(DAY, -((@i * 3) % 1460), CAST(GETDATE() AS DATE)),
        CASE WHEN @i % 20 = 0 THEN N'suspended' ELSE N'active' END,
        N'$2a$12$vfhLGjT3/IzLIYn6O/w8Wu..bkp3qQj5vAfaXbgp3P90kTWk7gz6a'
    );
    SET @i = @i + 1;
END
GO

PRINT 'Users inserted.';
GO

-- ============================================================
-- 6. LOANS  (10,000)
--    70 % returned  (some with fine, some paid)
--    20 % active    (ongoing, within due date)
--    10 % overdue   (not returned, fine calculated)
-- ============================================================
DECLARE @i         INT;
DECLARE @userId    INT;
DECLARE @bookId    INT;
DECLARE @minUser   INT, @maxUser INT, @userRange INT;
DECLARE @minBook   INT, @maxBook INT, @bookRange INT;
DECLARE @loanDate  DATE;
DECLARE @dueDate   DATE;
DECLARE @retDate   DATE;
DECLARE @rate      DECIMAL(5,2);
DECLARE @fine      DECIMAL(8,2);
DECLARE @daysOver  INT;
DECLARE @finePaid  BIT;

SELECT @minUser = MIN(user_id), @maxUser = MAX(user_id) FROM users;
SELECT @minBook = MIN(book_id),     @maxBook = MAX(book_id)     FROM books;
SET @userRange = @maxUser - @minUser + 1;
SET @bookRange = @maxBook - @minBook + 1;

SET @i = 1;
WHILE @i <= 10000
BEGIN
    SET @userId = @minUser + (@i          % @userRange);
    SET @bookId = @minBook + ((@i * 3)    % @bookRange);
    SET @rate   = CASE (@i % 3) WHEN 0 THEN 0.75 WHEN 1 THEN 0.50 ELSE 1.00 END;

    IF @i % 10 < 7
    BEGIN
        -- 70 %: returned
        SET @loanDate = DATEADD(DAY, -((@i * 5) % 1825 + 35), CAST(GETDATE() AS DATE));
        SET @dueDate  = DATEADD(DAY, 30, @loanDate);
        SET @retDate  = DATEADD(DAY,  5 + (@i % 28), @loanDate);
        SET @daysOver = DATEDIFF(DAY, @dueDate, @retDate);
        IF @daysOver > 0
        BEGIN
            SET @fine     = @daysOver * @rate;
            SET @finePaid = CASE WHEN @i % 2 = 0 THEN 1 ELSE 0 END;
        END
        ELSE
        BEGIN
            SET @fine     = NULL;
            SET @finePaid = 0;
        END
        INSERT INTO loans
            (user_id, book_id, loan_date, due_date, return_date, status, daily_fine_rate, fine_amount, fine_paid)
        VALUES
            (@userId, @bookId, @loanDate, @dueDate, @retDate, N'returned', @rate, @fine, @finePaid);
    END
    ELSE IF @i % 10 < 9
    BEGIN
        -- 20 %: active (within due date)
        SET @loanDate = DATEADD(DAY, -(@i % 25), CAST(GETDATE() AS DATE));
        SET @dueDate  = DATEADD(DAY, 30, @loanDate);
        INSERT INTO loans
            (user_id, book_id, loan_date, due_date, return_date, status, daily_fine_rate, fine_amount, fine_paid)
        VALUES
            (@userId, @bookId, @loanDate, @dueDate, NULL, N'active', @rate, NULL, 0);
    END
    ELSE
    BEGIN
        -- 10 %: overdue (not returned past due date)
        SET @loanDate = DATEADD(DAY, -(35 + (@i * 3) % 60), CAST(GETDATE() AS DATE));
        SET @dueDate  = DATEADD(DAY, 30, @loanDate);
        SET @daysOver = DATEDIFF(DAY, @dueDate, CAST(GETDATE() AS DATE));
        SET @fine     = @daysOver * @rate;
        INSERT INTO loans
            (user_id, book_id, loan_date, due_date, return_date, status, daily_fine_rate, fine_amount, fine_paid)
        VALUES
            (@userId, @bookId, @loanDate, @dueDate, NULL, N'overdue', @rate, @fine, 0);
    END

    SET @i = @i + 1;
END
GO

-- ============================================================
-- Summary
-- ============================================================
PRINT '';
PRINT '=== Volume seed completed ===';
PRINT '';
SELECT 'genres'      AS table_name, COUNT(*) AS total_rows FROM genres      UNION ALL
SELECT 'publishers',                COUNT(*)               FROM publishers   UNION ALL
SELECT 'authors',                   COUNT(*)               FROM authors      UNION ALL
SELECT 'books',                     COUNT(*)               FROM books        UNION ALL
SELECT 'book_authors',              COUNT(*)               FROM book_authors UNION ALL
SELECT 'users',                     COUNT(*)               FROM users        UNION ALL
SELECT 'loans',                     COUNT(*)               FROM loans;
GO
