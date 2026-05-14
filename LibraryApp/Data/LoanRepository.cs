using System.Data;
using Dapper;
using LibraryApp.Models;

namespace LibraryApp.Data;

public class LoanRepository(DapperContext ctx)
{
    private const string SelectCols = """
        l.loan_id LoanId, l.customer_id CustomerId,
        c.first_name + ' ' + c.last_name CustomerName,
        l.book_id BookId, b.title BookTitle,
        l.loan_date LoanDate, l.due_date DueDate, l.return_date ReturnDate,
        l.status Status, l.daily_fine_rate DailyFineRate,
        l.fine_amount FineAmount, l.fine_paid FinePaid
        """;

    public async Task<IEnumerable<LoanDetail>> GetAllAsync()
    {
        using var conn = ctx.CreateConnection();
        return await conn.QueryAsync<LoanDetail>($"""
            SELECT {SelectCols}
            FROM loans l
            JOIN customers c ON c.customer_id = l.customer_id
            JOIN books     b ON b.book_id     = l.book_id
            ORDER BY l.loan_date DESC
            """);
    }

    public async Task<IEnumerable<LoanDetail>> GetOverdueAsync()
    {
        using var conn = ctx.CreateConnection();
        return await conn.QueryAsync<LoanDetail>($"""
            SELECT {SelectCols}
            FROM loans l
            JOIN customers c ON c.customer_id = l.customer_id
            JOIN books     b ON b.book_id     = l.book_id
            WHERE l.status = 'overdue'
               OR (l.status = 'active' AND l.due_date < CAST(GETDATE() AS DATE))
            ORDER BY l.due_date
            """);
    }

    public async Task<IEnumerable<LoanDetail>> GetActiveAsync()
    {
        using var conn = ctx.CreateConnection();
        return await conn.QueryAsync<LoanDetail>($"""
            SELECT {SelectCols}
            FROM loans l
            JOIN customers c ON c.customer_id = l.customer_id
            JOIN books     b ON b.book_id     = l.book_id
            WHERE l.status IN ('active', 'overdue')
            ORDER BY l.due_date
            """);
    }

    public async Task BorrowAsync(int customerId, int bookId, int loanDays = 30)
    {
        using var conn = ctx.CreateConnection();
        await conn.ExecuteAsync("sp_borrow_book",
            new { customer_id = customerId, book_id = bookId, loan_days = loanDays },
            commandType: CommandType.StoredProcedure);
    }

    public async Task<LoanDetail?> ReturnAsync(int loanId)
    {
        using var conn = ctx.CreateConnection();
        await conn.ExecuteAsync("sp_return_book",
            new { loan_id = loanId },
            commandType: CommandType.StoredProcedure);
        return await conn.QueryFirstOrDefaultAsync<LoanDetail>($"""
            SELECT {SelectCols}
            FROM loans l
            JOIN customers c ON c.customer_id = l.customer_id
            JOIN books     b ON b.book_id     = l.book_id
            WHERE l.loan_id = @loanId
            """, new { loanId });
    }
}
