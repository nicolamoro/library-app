-- ============================================================
-- Library Management System — PostgreSQL DDL
-- ============================================================
-- Runs inside the database created from POSTGRES_DB (the postgres
-- image executes this file via /docker-entrypoint-initdb.d on first
-- init). No CREATE DATABASE / USE needed.

CREATE TABLE genres (
    genre_id    INT           GENERATED ALWAYS AS IDENTITY,
    name        VARCHAR(100)  NOT NULL,
    description VARCHAR(500)  NULL,
    CONSTRAINT PK_genres      PRIMARY KEY (genre_id),
    CONSTRAINT UQ_genres_name UNIQUE      (name)
);

CREATE TABLE publishers (
    publisher_id INT           GENERATED ALWAYS AS IDENTITY,
    name         VARCHAR(200)  NOT NULL,
    address      VARCHAR(300)  NULL,
    phone        VARCHAR(20)   NULL,
    email        VARCHAR(150)  NULL,
    website      VARCHAR(200)  NULL,
    CONSTRAINT PK_publishers PRIMARY KEY (publisher_id)
);

CREATE TABLE authors (
    author_id   INT            GENERATED ALWAYS AS IDENTITY,
    first_name  VARCHAR(100)   NOT NULL,
    last_name   VARCHAR(100)   NOT NULL,
    birth_date  DATE           NULL,
    nationality VARCHAR(100)   NULL,
    biography   TEXT           NULL,
    CONSTRAINT PK_authors PRIMARY KEY (author_id)
);

CREATE TABLE books (
    book_id          INT          GENERATED ALWAYS AS IDENTITY,
    isbn             VARCHAR(20)  NULL,
    title            VARCHAR(300) NOT NULL,
    publisher_id     INT          NULL,
    genre_id         INT          NULL,
    publication_year SMALLINT     NULL,
    language         VARCHAR(50)  NULL,
    page_count       SMALLINT     NULL,
    total_copies     SMALLINT     NOT NULL DEFAULT 1,
    available_copies SMALLINT     NOT NULL DEFAULT 1,
    CONSTRAINT PK_books                 PRIMARY KEY (book_id),
    CONSTRAINT UQ_books_isbn            UNIQUE      (isbn),
    CONSTRAINT FK_books_publisher       FOREIGN KEY (publisher_id) REFERENCES publishers (publisher_id),
    CONSTRAINT FK_books_genre           FOREIGN KEY (genre_id)     REFERENCES genres     (genre_id),
    CONSTRAINT CK_books_copies_positive CHECK (total_copies >= 0 AND available_copies >= 0),
    CONSTRAINT CK_books_copies_range    CHECK (available_copies <= total_copies)
);

CREATE TABLE book_authors (
    book_id   INT NOT NULL,
    author_id INT NOT NULL,
    CONSTRAINT PK_book_authors        PRIMARY KEY (book_id, author_id),
    CONSTRAINT FK_book_authors_book   FOREIGN KEY (book_id)   REFERENCES books   (book_id),
    CONSTRAINT FK_book_authors_author FOREIGN KEY (author_id) REFERENCES authors (author_id)
);

CREATE TABLE users (
    user_id           INT          GENERATED ALWAYS AS IDENTITY,
    first_name        VARCHAR(100) NOT NULL,
    last_name         VARCHAR(100) NOT NULL,
    birth_date        DATE         NULL,
    tax_code          VARCHAR(20)  NULL,
    address           VARCHAR(300) NULL,
    phone             VARCHAR(20)  NULL,
    email             VARCHAR(150) NOT NULL,
    registration_date DATE         NOT NULL DEFAULT CURRENT_DATE,
    status            VARCHAR(20)  NOT NULL DEFAULT 'active',
    password_hash     VARCHAR(100) NULL,
    is_admin          BOOLEAN      NOT NULL DEFAULT FALSE,
    last_login        TIMESTAMP    NULL,
    CONSTRAINT PK_users          PRIMARY KEY (user_id),
    CONSTRAINT UQ_users_email    UNIQUE      (email),
    CONSTRAINT UQ_users_taxcode  UNIQUE      (tax_code),
    CONSTRAINT CK_users_status   CHECK       (status IN ('active', 'suspended'))
);

CREATE TABLE loans (
    loan_id         INT          GENERATED ALWAYS AS IDENTITY,
    user_id         INT          NOT NULL,
    book_id         INT          NOT NULL,
    loan_date       DATE         NOT NULL DEFAULT CURRENT_DATE,
    due_date        DATE         NOT NULL,
    return_date     DATE         NULL,  -- NULL means loan is still open
    status          VARCHAR(20)  NOT NULL DEFAULT 'active',
    daily_fine_rate NUMERIC(5,2) NOT NULL DEFAULT 0.50,
    fine_amount     NUMERIC(8,2) NULL,
    fine_paid       BOOLEAN      NOT NULL DEFAULT FALSE,
    CONSTRAINT PK_loans             PRIMARY KEY (loan_id),
    CONSTRAINT FK_loans_user        FOREIGN KEY (user_id)     REFERENCES users (user_id),
    CONSTRAINT FK_loans_book        FOREIGN KEY (book_id)     REFERENCES books     (book_id),
    CONSTRAINT CK_loans_status      CHECK (status IN ('active', 'returned', 'overdue')),
    CONSTRAINT CK_loans_return_date CHECK (return_date IS NULL OR return_date >= loan_date),
    CONSTRAINT CK_loans_due_date    CHECK (due_date > loan_date)
);
