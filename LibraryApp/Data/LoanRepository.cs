using Dapper;
using LibraryApp.Models;

namespace LibraryApp.Data;

public class LoanRepository(DapperContext ctx) : ILoanRepository
{
    private const string SelectCols = """
        l.loan_id LoanId, l.user_id UserId,
        u.first_name || ' ' || u.last_name UserFullName,
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

    private static readonly Dictionary<string, string> _loanSortMap = new()
    {
        ["id"] = "l.loan_id",
        ["user"] = "u.last_name, u.first_name",
        ["book"] = "b.title",
        ["loandate"] = "l.loan_date",
        ["duedate"] = "l.due_date",
        ["returndate"] = "l.return_date",
        ["status"] = "l.status",
        ["fine"] = "l.fine_amount",
    };

    public async Task<(IEnumerable<LoanDetail> Items, int Total)> GetPagedAsync(
        int page, int pageSize, string filter, string? sortBy = null, bool sortDescending = false)
    {
        var offset = page * pageSize;
        var col = _loanSortMap.GetValueOrDefault(sortBy ?? "", "l.loan_date");
        var dir = sortDescending ? "DESC" : "ASC";
        var orderBy = string.Join(", ", col.Split(',').Select(c => $"{c.Trim()} {dir}"));
        var param = new
        {
            Filter = filter == "all" ? "" : filter,
            Offset = offset,
            PageSize = pageSize
        };

        var sql = $"""
            SELECT COUNT(*)
            FROM loans l
            JOIN users u ON u.user_id = l.user_id
            JOIN books b ON b.book_id = l.book_id
            WHERE @Filter = '' OR l.status = @Filter;

            SELECT {SelectCols}
            FROM loans l
            JOIN users u ON u.user_id = l.user_id
            JOIN books b ON b.book_id = l.book_id
            WHERE @Filter = '' OR l.status = @Filter
            ORDER BY {orderBy}
            OFFSET @Offset LIMIT @PageSize;
            """;

        using var conn = ctx.CreateConnection();
        using var multi = await conn.QueryMultipleAsync(sql, param);
        var total = await multi.ReadFirstAsync<int>();
        var items = await multi.ReadAsync<LoanDetail>();
        return (items, total);
    }

    public async Task<(IEnumerable<LoanDetail> Items, int Total)> GetByUserIdPagedAsync(
        int userId, int page, int pageSize, string filter = "all", string? sortBy = null, bool sortDescending = false)
    {
        var offset = page * pageSize;
        var col = _loanSortMap.GetValueOrDefault(sortBy ?? "", "l.loan_date");
        var dir = sortDescending ? "DESC" : "ASC";
        var orderBy = string.Join(", ", col.Split(',').Select(c => $"{c.Trim()} {dir}"));
        var param = new
        {
            UserId = userId,
            Filter = filter == "all" ? "" : filter,
            Offset = offset,
            PageSize = pageSize
        };
        var sql = $"""
            SELECT COUNT(*)
            FROM loans l
            JOIN users u ON u.user_id = l.user_id
            JOIN books b ON b.book_id = l.book_id
            WHERE l.user_id = @UserId
              AND (@Filter = '' OR l.status = @Filter);

            SELECT {SelectCols}
            FROM loans l
            JOIN users u ON u.user_id = l.user_id
            JOIN books b ON b.book_id = l.book_id
            WHERE l.user_id = @UserId
              AND (@Filter = '' OR l.status = @Filter)
            ORDER BY {orderBy}
            OFFSET @Offset LIMIT @PageSize;
            """;

        using var conn = ctx.CreateConnection();
        using var multi = await conn.QueryMultipleAsync(sql, param);
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
               OR (l.status = 'active' AND l.due_date < CURRENT_DATE)
            ORDER BY l.due_date
            """);
    }

    private static readonly Dictionary<string, string> _overdueSortMap = new()
    {
        ["id"] = "l.loan_id",
        ["user"] = "u.last_name, u.first_name",
        ["book"] = "b.title",
        ["duedate"] = "l.due_date",
        ["fine"] = "l.fine_amount",
    };

    public async Task<(IEnumerable<LoanDetail> Items, int Total)> GetOverduePagedAsync(
        int page, int pageSize, string? sortBy = null, bool sortDescending = false)
    {
        var offset = page * pageSize;
        var col = _overdueSortMap.GetValueOrDefault(sortBy ?? "", "l.due_date");
        var dir = sortDescending ? "DESC" : "ASC";
        var orderBy = string.Join(", ", col.Split(',').Select(c => $"{c.Trim()} {dir}"));
        var param = new { Offset = offset, PageSize = pageSize };

        var sql = $"""
            SELECT COUNT(*)
            FROM loans l
            JOIN users u ON u.user_id = l.user_id
            JOIN books b ON b.book_id = l.book_id
            WHERE l.status = 'overdue'
               OR (l.status = 'active' AND l.due_date < CURRENT_DATE);

            SELECT {SelectCols}
            FROM loans l
            JOIN users u ON u.user_id = l.user_id
            JOIN books b ON b.book_id = l.book_id
            WHERE l.status = 'overdue'
               OR (l.status = 'active' AND l.due_date < CURRENT_DATE)
            ORDER BY {orderBy}
            OFFSET @Offset LIMIT @PageSize;
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
        await conn.ExecuteAsync(
            "SELECT * FROM sp_borrow_book(@user_id, @book_id, @loan_days)",
            new { user_id = userId, book_id = bookId, loan_days = loanDays });
    }

    public async Task<LoanDetail?> ReturnAsync(int loanId)
    {
        using var conn = ctx.CreateConnection();
        await conn.ExecuteAsync(
            "SELECT * FROM sp_return_book(@loan_id)",
            new { loan_id = loanId });
        return await conn.QueryFirstOrDefaultAsync<LoanDetail>($"""
            SELECT {SelectCols}
            FROM loans l
            JOIN users u ON u.user_id = l.user_id
            JOIN books b ON b.book_id = l.book_id
            WHERE l.loan_id = @loanId
            """, new { loanId });
    }
}
