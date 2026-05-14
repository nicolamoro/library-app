-- ============================================================
-- Library Management System — Stored Procedures
-- ============================================================

USE LibraryDB;
GO

-- ------------------------------------------------------------
-- sp_borrow_book
--   Registra un nuovo prestito per un cliente.
--
--   @customer_id     : ID del cliente
--   @book_id         : ID del libro da prendere in prestito
--   @loan_days       : durata del prestito in giorni (default 30)
--   @daily_fine_rate : tariffa giornaliera multa (default 0.50)
--
--   Errori:
--     50001 – cliente non trovato
--     50002 – cliente sospeso
--     50003 – libro non trovato
--     50004 – nessuna copia disponibile
-- ------------------------------------------------------------
CREATE OR ALTER PROCEDURE sp_borrow_book
    @customer_id     INT,
    @book_id         INT,
    @loan_days       INT          = 30,
    @daily_fine_rate DECIMAL(5,2) = 0.50
AS
BEGIN
    SET NOCOUNT ON;

    DECLARE @customer_status NVARCHAR(20);
    SELECT @customer_status = status FROM customers WHERE customer_id = @customer_id;

    IF @customer_status IS NULL
        THROW 50001, 'Customer not found.', 1;

    IF @customer_status <> 'active'
        THROW 50002, 'Customer account is suspended.', 1;

    DECLARE @available SMALLINT;
    SELECT @available = available_copies FROM books WHERE book_id = @book_id;

    IF @available IS NULL
        THROW 50003, 'Book not found.', 1;

    IF @available = 0
        THROW 50004, 'No copies available for this book.', 1;

    BEGIN TRANSACTION;
    BEGIN TRY
        DECLARE @loan_date DATE = CAST(GETDATE() AS DATE);
        DECLARE @due_date  DATE = DATEADD(DAY, @loan_days, @loan_date);

        INSERT INTO loans (customer_id, book_id, loan_date, due_date, daily_fine_rate)
        VALUES (@customer_id, @book_id, @loan_date, @due_date, @daily_fine_rate);

        DECLARE @loan_id INT = SCOPE_IDENTITY();

        UPDATE books
        SET available_copies = available_copies - 1
        WHERE book_id = @book_id;

        COMMIT TRANSACTION;

        SELECT
            l.loan_id,
            c.first_name + ' ' + c.last_name AS customer,
            b.title,
            l.loan_date,
            l.due_date,
            l.status
        FROM loans l
        JOIN customers c ON c.customer_id = l.customer_id
        JOIN books     b ON b.book_id     = l.book_id
        WHERE l.loan_id = @loan_id;
    END TRY
    BEGIN CATCH
        IF @@TRANCOUNT > 0 ROLLBACK TRANSACTION;
        THROW;
    END CATCH;
END;
GO


-- ------------------------------------------------------------
-- sp_return_book
--   Registra la restituzione di un prestito.
--   Calcola automaticamente la multa se restituito in ritardo.
--
--   @loan_id : ID del prestito da chiudere
--
--   Errori:
--     50010 – prestito non trovato
--     50011 – prestito già restituito
-- ------------------------------------------------------------
CREATE OR ALTER PROCEDURE sp_return_book
    @loan_id INT
AS
BEGIN
    SET NOCOUNT ON;

    DECLARE @status     NVARCHAR(20);
    DECLARE @book_id    INT;
    DECLARE @due_date   DATE;
    DECLARE @daily_rate DECIMAL(5,2);

    SELECT
        @status     = status,
        @book_id    = book_id,
        @due_date   = due_date,
        @daily_rate = daily_fine_rate
    FROM loans
    WHERE loan_id = @loan_id;

    IF @status IS NULL
        THROW 50010, 'Loan not found.', 1;

    IF @status = 'returned'
        THROW 50011, 'This loan has already been returned.', 1;

    BEGIN TRANSACTION;
    BEGIN TRY
        DECLARE @return_date DATE          = CAST(GETDATE() AS DATE);
        DECLARE @fine_amount DECIMAL(8,2)  = NULL;

        IF @return_date > @due_date
        BEGIN
            DECLARE @days_overdue INT = DATEDIFF(DAY, @due_date, @return_date);
            SET @fine_amount = @days_overdue * @daily_rate;
        END

        UPDATE loans
        SET
            return_date = @return_date,
            status      = 'returned',
            fine_amount = @fine_amount
        WHERE loan_id = @loan_id;

        UPDATE books
        SET available_copies = available_copies + 1
        WHERE book_id = @book_id;

        COMMIT TRANSACTION;

        SELECT
            l.loan_id,
            c.first_name + ' ' + c.last_name                             AS customer,
            b.title,
            l.loan_date,
            l.due_date,
            l.return_date,
            l.status,
            ISNULL(CAST(l.fine_amount AS NVARCHAR(20)), 'none')           AS fine_amount,
            CASE WHEN l.fine_amount IS NULL THEN 'on time'
                 WHEN l.fine_paid = 1       THEN 'paid'
                 ELSE 'unpaid'
            END                                                           AS fine_status
        FROM loans l
        JOIN customers c ON c.customer_id = l.customer_id
        JOIN books     b ON b.book_id     = l.book_id
        WHERE l.loan_id = @loan_id;
    END TRY
    BEGIN CATCH
        IF @@TRANCOUNT > 0 ROLLBACK TRANSACTION;
        THROW;
    END CATCH;
END;
GO
