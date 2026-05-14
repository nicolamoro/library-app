-- ============================================================
-- Library Management System — SQL Server DDL
-- ============================================================

CREATE DATABASE LibraryDB;
GO

USE LibraryDB;
GO

CREATE TABLE genres (
    genre_id    INT           IDENTITY(1,1) NOT NULL,
    name        NVARCHAR(100) NOT NULL,
    description NVARCHAR(500) NULL,
    CONSTRAINT PK_genres      PRIMARY KEY (genre_id),
    CONSTRAINT UQ_genres_name UNIQUE      (name)
);

CREATE TABLE publishers (
    publisher_id INT           IDENTITY(1,1) NOT NULL,
    name         NVARCHAR(200) NOT NULL,
    address      NVARCHAR(300) NULL,
    phone        NVARCHAR(20)  NULL,
    email        NVARCHAR(150) NULL,
    website      NVARCHAR(200) NULL,
    CONSTRAINT PK_publishers PRIMARY KEY (publisher_id)
);

CREATE TABLE authors (
    author_id   INT            IDENTITY(1,1) NOT NULL,
    first_name  NVARCHAR(100)  NOT NULL,
    last_name   NVARCHAR(100)  NOT NULL,
    birth_date  DATE           NULL,
    nationality NVARCHAR(100)  NULL,
    biography   NVARCHAR(MAX)  NULL,
    CONSTRAINT PK_authors PRIMARY KEY (author_id)
);

CREATE TABLE books (
    book_id          INT           IDENTITY(1,1) NOT NULL,
    isbn             NVARCHAR(20)  NULL,
    title            NVARCHAR(300) NOT NULL,
    publisher_id     INT           NULL,
    genre_id         INT           NULL,
    publication_year SMALLINT      NULL,
    language         NVARCHAR(50)  NULL,
    page_count       SMALLINT      NULL,
    total_copies     SMALLINT      NOT NULL CONSTRAINT DF_books_total_copies     DEFAULT 1,
    available_copies SMALLINT      NOT NULL CONSTRAINT DF_books_available_copies DEFAULT 1,
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

CREATE TABLE customers (
    customer_id       INT           IDENTITY(1,1) NOT NULL,
    first_name        NVARCHAR(100) NOT NULL,
    last_name         NVARCHAR(100) NOT NULL,
    birth_date        DATE          NULL,
    tax_code          NVARCHAR(20)  NULL,
    address           NVARCHAR(300) NULL,
    phone             NVARCHAR(20)  NULL,
    email             NVARCHAR(150) NULL,
    registration_date DATE          NOT NULL CONSTRAINT DF_customers_registration_date DEFAULT CAST(GETDATE() AS DATE),
    status            NVARCHAR(20)  NOT NULL CONSTRAINT DF_customers_status            DEFAULT 'active',
    CONSTRAINT PK_customers        PRIMARY KEY (customer_id),
    CONSTRAINT UQ_customers_taxcode UNIQUE     (tax_code),
    CONSTRAINT CK_customers_status  CHECK      (status IN ('active', 'suspended'))
);

CREATE TABLE loans (
    loan_id         INT          IDENTITY(1,1) NOT NULL,
    customer_id     INT          NOT NULL,
    book_id         INT          NOT NULL,
    loan_date       DATE         NOT NULL CONSTRAINT DF_loans_loan_date       DEFAULT CAST(GETDATE() AS DATE),
    due_date        DATE         NOT NULL,
    return_date     DATE         NULL,  -- NULL means loan is still open
    status          NVARCHAR(20) NOT NULL CONSTRAINT DF_loans_status          DEFAULT 'active',
    daily_fine_rate DECIMAL(5,2) NOT NULL CONSTRAINT DF_loans_daily_fine_rate DEFAULT 0.50,
    fine_amount     DECIMAL(8,2) NULL,
    fine_paid       BIT          NOT NULL CONSTRAINT DF_loans_fine_paid       DEFAULT 0,
    CONSTRAINT PK_loans             PRIMARY KEY (loan_id),
    CONSTRAINT FK_loans_customer    FOREIGN KEY (customer_id) REFERENCES customers (customer_id),
    CONSTRAINT FK_loans_book        FOREIGN KEY (book_id)     REFERENCES books     (book_id),
    CONSTRAINT CK_loans_status      CHECK (status IN ('active', 'returned', 'overdue')),
    CONSTRAINT CK_loans_return_date CHECK (return_date IS NULL OR return_date >= loan_date),
    CONSTRAINT CK_loans_due_date    CHECK (due_date > loan_date)
);
