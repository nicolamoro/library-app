-- ============================================================
-- Library Management System — Seed Data
-- ============================================================

USE LibraryDB;
GO

-- genres
INSERT INTO genres (name, description) VALUES
    ('Fantasy',            'Narrativa fantastica con elementi magici e mondi immaginari'),
    ('Horror',             'Narrativa volta a suscitare paura e suspense'),
    ('Science Fiction',    'Narrativa speculativa basata su scienza e tecnologia'),
    ('Historical Fiction', 'Romanzi ambientati in epoche storiche reali'),
    ('Contemporary Fiction','Narrativa letteraria ambientata nel mondo contemporaneo'),
    ('Thriller',           'Narrativa ad alta tensione con colpi di scena');
GO

-- publishers
INSERT INTO publishers (name, address, email, website) VALUES
    ('Bompiani',    'Via Mecenate 91, Milano',       'info@bompiani.it',    'www.bompiani.it'),
    ('Einaudi',     'Via Umberto Biancamano 2, Torino','info@einaudi.it',   'www.einaudi.it'),
    ('Mondadori',   'Via Mondadori 1, Segrate',      'info@mondadori.it',   'www.mondadori.it'),
    ('Bloomsbury',  '50 Bedford Square, London',     'info@bloomsbury.com', 'www.bloomsbury.com');
GO

-- authors
INSERT INTO authors (first_name, last_name, birth_date, nationality, biography) VALUES
    ('Umberto', 'Eco',     '1932-01-05', 'Italiana',   'Semiologo, filosofo e romanziere. Noto per "Il nome della rosa".'),
    ('Italo',   'Calvino', '1923-10-15', 'Italiana',   'Scrittore e giornalista, tra i più importanti del Novecento italiano.'),
    ('Elena',   'Ferrante','1943-01-01', 'Italiana',   'Autrice pseudonima, celebre per la tetralogia dell''amica geniale.'),
    ('George',  'Orwell',  '1903-06-25', 'Britannica', 'Scrittore e giornalista britannico, autore di "1984" e "Animal Farm".'),
    ('Stephen', 'King',    '1947-09-21', 'Americana',  'Maestro del genere horror, autore prolifico con oltre 60 romanzi.'),
    ('J.K.',    'Rowling', '1965-07-31', 'Britannica', 'Autrice della saga di Harry Potter, una delle più vendute al mondo.');
GO

-- books
-- available_copies riflette i prestiti attivi/scaduti inseriti sotto
INSERT INTO books (isbn, title, publisher_id, genre_id, publication_year, language, page_count, total_copies, available_copies) VALUES
    ('978-8845221224', 'Il Nome della Rosa',                      1, 4, 1980, 'Italiano', 502, 3, 2),  -- 1 copia in prestito attivo
    ('978-8806924041', 'Se una notte d''inverno un viaggiatore',  2, 5, 1979, 'Italiano', 258, 2, 2),
    ('978-8866329626', 'L''Amica Geniale',                        2, 5, 2011, 'Italiano', 400, 3, 3),
    ('978-8804668237', '1984',                                    3, 3, 1949, 'Italiano', 328, 4, 4),
    ('978-8804668244', 'La Fattoria degli Animali',               3, 3, 1945, 'Italiano', 140, 3, 3),
    ('978-8804521018', 'It',                                      3, 2, 1986, 'Italiano', 1216, 2, 2),
    ('978-8804523369', 'The Shining',                             3, 2, 1977, 'Italiano', 432, 2, 1),  -- 1 copia in prestito scaduto
    ('978-0747532699', 'Harry Potter e la Pietra Filosofale',     4, 1, 1997, 'Italiano', 309, 5, 4); -- 1 copia in prestito attivo
GO

-- book_authors
INSERT INTO book_authors (book_id, author_id) VALUES
    (1, 1),  -- Il Nome della Rosa → Eco
    (2, 2),  -- Se una notte... → Calvino
    (3, 3),  -- L'Amica Geniale → Ferrante
    (4, 4),  -- 1984 → Orwell
    (5, 4),  -- La Fattoria degli Animali → Orwell
    (6, 5),  -- It → King
    (7, 5),  -- The Shining → King
    (8, 6);  -- Harry Potter → Rowling
GO

-- customers
INSERT INTO customers (first_name, last_name, birth_date, tax_code, address, phone, email, registration_date, status) VALUES
    ('Mario',     'Rossi',    '1985-03-12', 'RSSMRA85C12H501Z', 'Via Roma 10, Milano',       '3331234567', 'mario.rossi@email.it',     '2023-01-15', 'active'),
    ('Lucia',     'Bianchi',  '1992-07-24', 'BNCLCU92L64F205X', 'Corso Vittorio 5, Torino',  '3459876543', 'lucia.bianchi@email.it',   '2023-03-22', 'active'),
    ('Francesco', 'Conti',    '1978-11-03', 'CNTFNC78S03L219W', 'Via Garibaldi 33, Bologna', '3381122334', 'f.conti@email.it',         '2024-02-10', 'active'),
    ('Giulia',    'Marino',   '1990-05-17', 'MRNGLU90E57F839P', 'Piazza Dante 7, Napoli',    '3204455667', 'giulia.marino@email.it',   '2024-06-01', 'active'),
    ('Antonio',   'De Luca',  '1965-09-30', 'DLCNTN65P30H501B', 'Via Nazionale 88, Roma',    '3667788990', 'antonio.deluca@email.it',  '2022-11-05', 'suspended');
GO

-- loans
-- Prestito 1: Mario Rossi — Il Nome della Rosa — ATTIVO
-- Prestito 2: Lucia Bianchi — 1984 — RESTITUITO
-- Prestito 3: Francesco Conti — Harry Potter — ATTIVO
-- Prestito 4: Mario Rossi — The Shining — SCADUTO (33 giorni, multa 16.50)
-- Prestito 5: Giulia Marino — L'Amica Geniale — RESTITUITO
-- Prestito 6: Lucia Bianchi — La Fattoria degli Animali — RESTITUITO
INSERT INTO loans (customer_id, book_id, loan_date, due_date, return_date, status, daily_fine_rate, fine_amount, fine_paid) VALUES
    (1, 1, '2026-04-20', '2026-05-20', NULL,         'active',    0.50, NULL,  0),
    (2, 4, '2026-03-01', '2026-03-31', '2026-03-28', 'returned',  0.50, NULL,  0),
    (3, 8, '2026-05-01', '2026-05-31', NULL,         'active',    0.50, NULL,  0),
    (1, 7, '2026-03-10', '2026-04-10', NULL,         'overdue',   0.50, 16.50, 0),
    (4, 3, '2026-02-01', '2026-03-01', '2026-02-25', 'returned',  0.50, NULL,  0),
    (2, 5, '2026-01-10', '2026-02-10', '2026-02-08', 'returned',  0.50, NULL,  0);
GO
