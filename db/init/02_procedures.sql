-- ============================================================
-- Library Management System — Functions (PL/pgSQL)
-- ============================================================
-- Ported from the original SQL Server stored procedures. Each
-- function runs inside the caller's transaction, so RAISE EXCEPTION
-- aborts and rolls back automatically (no explicit BEGIN/COMMIT/
-- TRY-CATCH needed). They RETURN TABLE so the app can read the
-- resulting row via "SELECT * FROM sp_xxx(...)".


-- ------------------------------------------------------------
-- sp_borrow_book
--   Records a new loan for a user.
--
--   p_user_id         : user ID
--   p_book_id         : ID of the book to borrow
--   p_loan_days       : loan duration in days (default 30)
--   p_daily_fine_rate : daily fine rate (default 0.50)
--
--   Errors:
--     user not found / suspended / book not found / no copies
-- ------------------------------------------------------------
CREATE OR REPLACE FUNCTION sp_borrow_book(
    p_user_id         INT,
    p_book_id         INT,
    p_loan_days       INT          DEFAULT 30,
    p_daily_fine_rate NUMERIC      DEFAULT 0.50
)
RETURNS TABLE (
    loan_id        INT,
    user_full_name TEXT,
    title          VARCHAR,
    loan_date      DATE,
    due_date       DATE,
    status         VARCHAR
)
LANGUAGE plpgsql
AS $$
DECLARE
    v_user_status VARCHAR(20);
    v_available   SMALLINT;
    v_loan_date   DATE := CURRENT_DATE;
    v_due_date    DATE;
    v_loan_id     INT;
BEGIN
    SELECT u.status INTO v_user_status FROM users u WHERE u.user_id = p_user_id;

    IF v_user_status IS NULL THEN
        RAISE EXCEPTION 'User not found.';
    END IF;

    IF v_user_status <> 'active' THEN
        RAISE EXCEPTION 'User account is suspended.';
    END IF;

    SELECT b.available_copies INTO v_available FROM books b WHERE b.book_id = p_book_id;

    IF v_available IS NULL THEN
        RAISE EXCEPTION 'Book not found.';
    END IF;

    IF v_available = 0 THEN
        RAISE EXCEPTION 'No copies available for this book.';
    END IF;

    v_due_date := v_loan_date + (p_loan_days || ' days')::INTERVAL;

    INSERT INTO loans (user_id, book_id, loan_date, due_date, daily_fine_rate)
    VALUES (p_user_id, p_book_id, v_loan_date, v_due_date, p_daily_fine_rate)
    RETURNING loans.loan_id INTO v_loan_id;

    UPDATE books
    SET available_copies = available_copies - 1
    WHERE book_id = p_book_id;

    RETURN QUERY
        SELECT
            l.loan_id,
            (u.first_name || ' ' || u.last_name)::TEXT AS user_full_name,
            b.title,
            l.loan_date,
            l.due_date,
            l.status
        FROM loans l
        JOIN users u ON u.user_id = l.user_id
        JOIN books b ON b.book_id = l.book_id
        WHERE l.loan_id = v_loan_id;
END;
$$;


-- ------------------------------------------------------------
-- sp_return_book
--   Records the return of a loan.
--   Automatically calculates the fine if returned late.
--
--   p_loan_id : ID of the loan to close
--
--   Errors:
--     loan not found / loan already returned
-- ------------------------------------------------------------
CREATE OR REPLACE FUNCTION sp_return_book(
    p_loan_id INT
)
RETURNS TABLE (
    loan_id        INT,
    user_full_name TEXT,
    title          VARCHAR,
    loan_date      DATE,
    due_date       DATE,
    return_date    DATE,
    status         VARCHAR,
    fine_amount    TEXT,
    fine_status    TEXT
)
LANGUAGE plpgsql
AS $$
DECLARE
    v_status      VARCHAR(20);
    v_book_id     INT;
    v_due_date    DATE;
    v_daily_rate  NUMERIC(5,2);
    v_return_date DATE := CURRENT_DATE;
    v_fine_amount NUMERIC(8,2) := NULL;
    v_days_overdue INT;
BEGIN
    SELECT l.status, l.book_id, l.due_date, l.daily_fine_rate
      INTO v_status, v_book_id, v_due_date, v_daily_rate
    FROM loans l
    WHERE l.loan_id = p_loan_id;

    IF v_status IS NULL THEN
        RAISE EXCEPTION 'Loan not found.';
    END IF;

    IF v_status = 'returned' THEN
        RAISE EXCEPTION 'This loan has already been returned.';
    END IF;

    IF v_return_date > v_due_date THEN
        v_days_overdue := v_return_date - v_due_date;
        v_fine_amount := v_days_overdue * v_daily_rate;
    END IF;

    UPDATE loans
    SET
        return_date = v_return_date,
        status      = 'returned',
        fine_amount = v_fine_amount
    WHERE loans.loan_id = p_loan_id;

    UPDATE books
    SET available_copies = available_copies + 1
    WHERE book_id = v_book_id;

    RETURN QUERY
        SELECT
            l.loan_id,
            (u.first_name || ' ' || u.last_name)::TEXT                  AS user_full_name,
            b.title,
            l.loan_date,
            l.due_date,
            l.return_date,
            l.status,
            COALESCE(CAST(l.fine_amount AS TEXT), 'none')               AS fine_amount,
            CASE WHEN l.fine_amount IS NULL THEN 'on time'
                 WHEN l.fine_paid             THEN 'paid'
                 ELSE 'unpaid'
            END                                                         AS fine_status
        FROM loans l
        JOIN users u ON u.user_id = l.user_id
        JOIN books b ON b.book_id = l.book_id
        WHERE l.loan_id = p_loan_id;
END;
$$;
