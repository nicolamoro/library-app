using System.Data;
using Dapper;
using LibraryApp.Models;

namespace LibraryApp.Data;

public class LoanRepository(DapperContext ctx)
{
    private const string SelectCols = """
        l.loan_id LoanId, l.user_id UserId,
        u.first_name + ' ' + u.last_name UserFullName,
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
            JOIN users u ON u.user_id = l.user_id
            JOIN books b ON b.book_id = l.book_id
            ORDER BY l.loan_date DESC
            """);
    }

    public async Task<(IEnumerable<LoanDetail> Items, int Total)> GetPagedAsync(int page, int pageSize, string filter)
    {
        var offset = page * pageSize;
        var param = new
        {
            Filter = filter == "all" ? null : filter,
            Offset = offset,
            PageSize = pageSize
        };

        var sql = $"""
            SELECT COUNT(*)
            FROM loans l
            JOIN users u ON u.user_id = l.user_id
            JOIN books b ON b.book_id = l.book_id
            WHERE @Filter IS NULL OR l.status = @Filter;

            SELECT {SelectCols}
            FROM loans l
            JOIN users u ON u.user_id = l.user_id
            JOIN books b ON b.book_id = l.book_id
            WHERE @Filter IS NULL OR l.status = @Filter
            ORDER BY l.loan_date DESC
            OFFSET @Offset ROWS FETCH NEXT @PageSize ROWS ONLY;
            """;

        using var conn = ctx.CreateConnection();
        using var multi = await conn.QueryMultipleAsync(sql, param);
        var total = await multi.ReadFirstAsync<int>();
        var items = await multi.ReadAsync<LoanDetail>();
        return (items, total);
    }

    public async Task<(IEnumerable<LoanDetail> Items, int Total)> GetByUserIdPagedAsync(
        int userId, int page, int pageSize)
    {
        var offset = page * pageSize;
        var sql = $"""
            SELECT COUNT(*)
            FROM loans l
            JOIN users u ON u.user_id = l.user_id
            JOIN books b ON b.book_id = l.book_id
            WHERE l.user_id = @UserId;

            SELECT {SelectCols}
            FROM loans l
            JOIN users u ON u.user_id = l.user_id
            JOIN books b ON b.book_id = l.book_id
            WHERE l.user_id = @UserId
            ORDER BY l.loan_date DESC
            OFFSET @Offset ROWS FETCH NEXT @PageSize ROWS ONLY;
            """;

        using var conn = ctx.CreateConnection();
        using var multi = await conn.QueryMultipleAsync(sql,
            new { UserId = userId, Offset = offset, PageSize = pageSize });
        var total = await multi.ReadFirstAsync<int>();
        var items = await multi.ReadAsync<LoanDetail>();
        return (items, total);
    }

    public async Task<IEnumerable<LoanDetail>> GetOverdueAsync()
    {
        using var conn = ctx.CreateConnection();
        return await conn.QueryAsync<LoanDetail>($"""
            SELECT {SelectCols}
            FROM loans l
            JOIN users u ON u.user_id = l.user_id
            JOIN books b ON b.book_id = l.book_id
            WHERE l.status = 'overdue'
               OR (l.status = 'active' AND l.due_date < CAST(GETDATE() AS DATE))
            ORDER BY l.due_date
            """);
    }

    public async Task<(IEnumerable<LoanDetail> Items, int Total)> GetOverduePagedAsync(int page, int pageSize)
    {
        var offset = page * pageSize;
        var param = new { Offset = offset, PageSize = pageSize };

        var sql = $"""
            SELECT COUNT(*)
            FROM loans l
            JOIN users u ON u.user_id = l.user_id
            JOIN books b ON b.book_id = l.book_id
            WHERE l.status = 'overdue'
               OR (l.status = 'active' AND l.due_date < CAST(GETDATE() AS DATE));

            SELECT {SelectCols}
            FROM loans l
            JOIN users u ON u.user_id = l.user_id
            JOIN books b ON b.book_id = l.book_id
            WHERE l.status = 'overdue'
               OR (l.status = 'active' AND l.due_date < CAST(GETDATE() AS DATE))
            ORDER BY l.due_date
            OFFSET @Offset ROWS FETCH NEXT @PageSize ROWS ONLY;
            """;

        using var conn = ctx.CreateConnection();
        using var multi = await conn.QueryMultipleAsync(sql, param);
        var total = await multi.ReadFirstAsync<int>();
        var items = await multi.ReadAsync<LoanDetail>();
        return (items, total);
    }

    public async Task<IEnumerable<LoanDetail>> GetActiveAsync()
    {
        using var conn = ctx.CreateConnection();
        return await conn.QueryAsync<LoanDetail>($"""
            SELECT {SelectCols}
            FROM loans l
            JOIN users u ON u.user_id = l.user_id
            JOIN books b ON b.book_id = l.book_id
            WHERE l.status IN ('active', 'overdue')
            ORDER BY l.due_date
            """);
    }

    public async Task BorrowAsync(int userId, int bookId, int loanDays = 30)
    {
        using var conn = ctx.CreateConnection();
        await conn.ExecuteAsync("sp_borrow_book",
            new { user_id = userId, book_id = bookId, loan_days = loanDays },
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
            JOIN users u ON u.user_id = l.user_id
            JOIN books b ON b.book_id = l.book_id
            WHERE l.loan_id = @loanId
            """, new { loanId });
    }
}
