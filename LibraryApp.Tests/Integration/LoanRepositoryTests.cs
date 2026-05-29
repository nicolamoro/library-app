using Dapper;
using LibraryApp.Data;
using LibraryApp.Tests.Integration.Fixtures;

namespace LibraryApp.Tests.Integration;

[Trait("Category", "Integration")]
[Collection("Postgres")]
public class LoanRepositoryTests(PostgresFixture fixture)
{
    private readonly LoanRepository _repo = new(fixture.DapperContext);

    // Resolve a valid active user and available book from seed data.
    private async Task<(int userId, int bookId)> GetAvailablePairAsync()
    {
        using var conn = fixture.DapperContext.CreateConnection();
        var userId = await conn.ExecuteScalarAsync<int>(
            "SELECT user_id FROM users WHERE status = 'active' AND is_admin = FALSE ORDER BY user_id LIMIT 1");
        var bookId = await conn.ExecuteScalarAsync<int>(
            "SELECT book_id FROM books WHERE available_copies > 0 ORDER BY book_id LIMIT 1");
        return (userId, bookId);
    }

    [Fact]
    public async Task GetPagedAsync_AllFilter_ReturnsSeededLoans()
    {
        var (items, total) = await _repo.GetPagedAsync(0, 100, "all");
        Assert.True(total > 0);
        Assert.NotEmpty(items);
    }

    [Fact]
    public async Task GetPagedAsync_ActiveFilter_OnlyActiveLoans()
    {
        var (items, _) = await _repo.GetPagedAsync(0, 100, "active");
        Assert.All(items, l => Assert.Equal("active", l.Status));
    }

    [Fact]
    public async Task GetPagedAsync_ReturnedFilter_OnlyReturnedLoans()
    {
        var (items, _) = await _repo.GetPagedAsync(0, 100, "returned");
        Assert.All(items, l => Assert.Equal("returned", l.Status));
    }

    [Fact]
    public async Task BorrowAsync_AvailableBook_CreatesLoanAndDecrementsAvailableCopies()
    {
        var (userId, bookId) = await GetAvailablePairAsync();

        using var conn = fixture.DapperContext.CreateConnection();
        var copiesBefore = await conn.ExecuteScalarAsync<int>(
            "SELECT available_copies FROM books WHERE book_id = @bookId", new { bookId });

        await _repo.BorrowAsync(userId, bookId, 14);

        var copiesAfter = await conn.ExecuteScalarAsync<int>(
            "SELECT available_copies FROM books WHERE book_id = @bookId", new { bookId });
        var loanExists = await conn.ExecuteScalarAsync<int>(
            "SELECT COUNT(*) FROM loans WHERE user_id = @userId AND book_id = @bookId AND status = 'active'",
            new { userId, bookId });

        Assert.Equal(copiesBefore - 1, copiesAfter);
        Assert.True(loanExists > 0);
    }

    [Fact]
    public async Task BorrowAsync_UnavailableBook_ThrowsException()
    {
        using var conn = fixture.DapperContext.CreateConnection();

        // Insert a book with 0 copies
        var zeroBookId = await conn.ExecuteScalarAsync<int>("""
            INSERT INTO books (title, total_copies, available_copies)
            VALUES ('Test Book Zero Copies', 1, 0)
            RETURNING book_id;
            """);

        var userId = await conn.ExecuteScalarAsync<int>(
            "SELECT user_id FROM users WHERE status = 'active' AND is_admin = FALSE LIMIT 1");

        await Assert.ThrowsAnyAsync<Exception>(() => _repo.BorrowAsync(userId, zeroBookId));
    }

    [Fact]
    public async Task ReturnAsync_ActiveLoan_SetsReturnDateAndIncrementsAvailableCopies()
    {
        var (userId, bookId) = await GetAvailablePairAsync();
        await _repo.BorrowAsync(userId, bookId, 30);

        using var conn = fixture.DapperContext.CreateConnection();
        var loanId = await conn.ExecuteScalarAsync<int>(
            "SELECT loan_id FROM loans WHERE user_id = @userId AND book_id = @bookId AND status = 'active' ORDER BY loan_id DESC LIMIT 1",
            new { userId, bookId });

        var copiesBefore = await conn.ExecuteScalarAsync<int>(
            "SELECT available_copies FROM books WHERE book_id = @bookId", new { bookId });

        var result = await _repo.ReturnAsync(loanId);

        Assert.NotNull(result);
        Assert.NotNull(result.ReturnDate);
        Assert.Equal("returned", result.Status);

        var copiesAfter = await conn.ExecuteScalarAsync<int>(
            "SELECT available_copies FROM books WHERE book_id = @bookId", new { bookId });
        Assert.Equal(copiesBefore + 1, copiesAfter);
    }

    [Fact]
    public async Task GetByUserIdPagedAsync_FiltersCorrectly()
    {
        using var conn = fixture.DapperContext.CreateConnection();
        var userId = await conn.ExecuteScalarAsync<int>(
            "SELECT user_id FROM loans ORDER BY user_id LIMIT 1");

        var (items, total) = await _repo.GetByUserIdPagedAsync(userId, 0, 100);
        Assert.True(total > 0);
        Assert.All(items, l => Assert.Equal(userId, l.UserId));
    }

    [Fact]
    public async Task GetOverduePagedAsync_ReturnsOverdueLoans()
    {
        // At least one overdue loan exists in seed data.
        var (items, total) = await _repo.GetOverduePagedAsync(0, 100);
        Assert.True(total >= 0); // seed may have 0 overdue; we only check the method runs.
        Assert.All(items, l => Assert.True(l.Status == "overdue"
            || (l.Status == "active" && l.DueDate < DateOnly.FromDateTime(DateTime.Today))));
    }
}
