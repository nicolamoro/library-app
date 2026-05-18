using Dapper;
using LibraryApp.Data;
using LibraryApp.Models;
using LibraryApp.Tests.Integration.Fixtures;

namespace LibraryApp.Tests.Integration;

[Trait("Category", "Integration")]
[Collection("SqlServer")]
public class BookRepositoryTests(SqlServerFixture fixture)
{
    private readonly BookRepository _repo = new(fixture.DapperContext);

    [Fact]
    public async Task GetPagedAsync_NoSearch_ReturnsSeedBooks()
    {
        var (items, total) = await _repo.GetPagedAsync(0, 20, null);
        Assert.True(total > 0);
        Assert.NotEmpty(items);
    }

    [Fact]
    public async Task GetPagedAsync_SearchByTitle_FiltersResults()
    {
        // Get any real title from seed, then search for part of it.
        using var conn = fixture.DapperContext.CreateConnection();
        var title = await conn.ExecuteScalarAsync<string>("SELECT TOP 1 title FROM books ORDER BY book_id");

        var (items, total) = await _repo.GetPagedAsync(0, 20, title![..3]);
        Assert.True(total >= 1);
        Assert.Contains(items, b => b.Title.Contains(title[..3], StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public async Task GetPagedAsync_SortByTitle_OrdersAscending()
    {
        var (items, _) = await _repo.GetPagedAsync(0, 20, null, "title", false);
        var titles = items.Select(b => b.Title).ToList();
        Assert.Equal(titles.OrderBy(t => t, StringComparer.OrdinalIgnoreCase).ToList(), titles);
    }

    [Fact]
    public async Task GetPagedAsync_SortByTitle_OrdersDescending()
    {
        var (items, _) = await _repo.GetPagedAsync(0, 20, null, "title", true);
        var titles = items.Select(b => b.Title).ToList();
        Assert.Equal(titles.OrderByDescending(t => t, StringComparer.OrdinalIgnoreCase).ToList(), titles);
    }

    [Fact]
    public async Task GetPagedAsync_Paging_ReturnsCorrectPage()
    {
        var (page0, total) = await _repo.GetPagedAsync(0, 5, null);
        var (page1, _) = await _repo.GetPagedAsync(1, 5, null);

        Assert.Equal(5, page0.Count());
        if (total > 5)
        {
            Assert.NotEmpty(page1);
            Assert.Empty(page0.Select(b => b.BookId).Intersect(page1.Select(b => b.BookId)));
        }
    }

    [Fact]
    public async Task CreateAsync_InsertsBookWithAuthors()
    {
        using var conn = fixture.DapperContext.CreateConnection();
        var authorId = await conn.ExecuteScalarAsync<int>("SELECT TOP 1 author_id FROM authors");

        var book = new Book
        {
            Title = $"Test Book {Guid.NewGuid():N}",
            TotalCopies = 2,
            SelectedAuthorIds = [authorId]
        };

        var bookId = await _repo.CreateAsync(book);
        Assert.True(bookId > 0);

        var authorLink = await conn.ExecuteScalarAsync<int>(
            "SELECT COUNT(*) FROM book_authors WHERE book_id = @bookId AND author_id = @authorId",
            new { bookId, authorId });
        Assert.Equal(1, authorLink);
    }

    [Fact]
    public async Task DeleteAsync_RemovesBookAndAuthorLinks()
    {
        using var conn = fixture.DapperContext.CreateConnection();
        var authorId = await conn.ExecuteScalarAsync<int>("SELECT TOP 1 author_id FROM authors");

        var book = new Book { Title = $"ToDelete {Guid.NewGuid():N}", TotalCopies = 1, SelectedAuthorIds = [authorId] };
        var bookId = await _repo.CreateAsync(book);

        await _repo.DeleteAsync(bookId);

        var bookCount = await conn.ExecuteScalarAsync<int>("SELECT COUNT(*) FROM books WHERE book_id = @bookId", new { bookId });
        var linkCount = await conn.ExecuteScalarAsync<int>("SELECT COUNT(*) FROM book_authors WHERE book_id = @bookId", new { bookId });
        Assert.Equal(0, bookCount);
        Assert.Equal(0, linkCount);
    }

    [Fact]
    public async Task GetByIdAsync_ExistingBook_ReturnsBook()
    {
        using var conn = fixture.DapperContext.CreateConnection();
        var bookId = await conn.ExecuteScalarAsync<int>("SELECT TOP 1 book_id FROM books");

        var book = await _repo.GetByIdAsync(bookId);
        Assert.NotNull(book);
        Assert.Equal(bookId, book.BookId);
    }

    [Fact]
    public async Task GetByIdAsync_NonExistingBook_ReturnsNull()
    {
        var book = await _repo.GetByIdAsync(int.MaxValue);
        Assert.Null(book);
    }
}
