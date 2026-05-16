using Dapper;
using LibraryApp.Models;

namespace LibraryApp.Data;

public class BookRepository(DapperContext ctx)
{
    public async Task<IEnumerable<Book>> GetAllAsync()
    {
        const string sql = """
            SELECT b.book_id BookId, b.isbn Isbn, b.title Title,
                   b.publisher_id PublisherId, p.name PublisherName,
                   b.genre_id GenreId, g.name GenreName,
                   b.publication_year PublicationYear, b.language Language,
                   b.page_count PageCount, b.total_copies TotalCopies,
                   b.available_copies AvailableCopies,
                   STRING_AGG(a.first_name + ' ' + a.last_name, ', ') AuthorsDisplay
            FROM books b
            LEFT JOIN publishers p  ON p.publisher_id = b.publisher_id
            LEFT JOIN genres g      ON g.genre_id     = b.genre_id
            LEFT JOIN book_authors ba ON ba.book_id   = b.book_id
            LEFT JOIN authors a     ON a.author_id    = ba.author_id
            GROUP BY b.book_id, b.isbn, b.title, b.publisher_id, p.name,
                     b.genre_id, g.name, b.publication_year, b.language,
                     b.page_count, b.total_copies, b.available_copies
            ORDER BY b.title
            """;
        using var conn = ctx.CreateConnection();
        return await conn.QueryAsync<Book>(sql);
    }

    private static readonly Dictionary<string, string> _bookSortMap = new()
    {
        ["title"]     = "Title",
        ["publisher"] = "PublisherName",
        ["genre"]     = "GenreName",
        ["year"]      = "PublicationYear",
        ["copies"]    = "AvailableCopies",
    };

    public async Task<(IEnumerable<Book> Items, int Total)> GetPagedAsync(
        int page, int pageSize, string? search, string? sortBy = null, bool sortDescending = false)
    {
        var offset = page * pageSize;
        var col = _bookSortMap.GetValueOrDefault(sortBy ?? "", "Title");
        var dir = sortDescending ? "DESC" : "ASC";
        var orderBy = string.Join(", ", col.Split(',').Select(c => $"{c.Trim()} {dir}"));
        var param = new
        {
            Search = string.IsNullOrWhiteSpace(search) ? null : search,
            Offset = offset,
            PageSize = pageSize
        };

        const string cte = """
            WITH cte AS (
                SELECT b.book_id BookId, b.isbn Isbn, b.title Title,
                       b.publisher_id PublisherId, p.name PublisherName,
                       b.genre_id GenreId, g.name GenreName,
                       b.publication_year PublicationYear, b.language Language,
                       b.page_count PageCount, b.total_copies TotalCopies,
                       b.available_copies AvailableCopies,
                       STRING_AGG(a.first_name + ' ' + a.last_name, ', ') AuthorsDisplay
                FROM books b
                LEFT JOIN publishers p  ON p.publisher_id = b.publisher_id
                LEFT JOIN genres g      ON g.genre_id     = b.genre_id
                LEFT JOIN book_authors ba ON ba.book_id   = b.book_id
                LEFT JOIN authors a     ON a.author_id    = ba.author_id
                GROUP BY b.book_id, b.isbn, b.title, b.publisher_id, p.name,
                         b.genre_id, g.name, b.publication_year, b.language,
                         b.page_count, b.total_copies, b.available_copies
            )
            """;

        const string where = """
            WHERE @Search IS NULL
               OR Title                       LIKE '%' + @Search + '%'
               OR ISNULL(GenreName,'')        LIKE '%' + @Search + '%'
               OR ISNULL(AuthorsDisplay,'')   LIKE '%' + @Search + '%'
            """;

        var sql = $"""
            {cte} SELECT COUNT(*) FROM cte {where};
            {cte} SELECT * FROM cte {where}
            ORDER BY {orderBy}
            OFFSET @Offset ROWS FETCH NEXT @PageSize ROWS ONLY;
            """;

        using var conn = ctx.CreateConnection();
        using var multi = await conn.QueryMultipleAsync(sql, param);
        var total = await multi.ReadFirstAsync<int>();
        var items = await multi.ReadAsync<Book>();
        return (items, total);
    }

    public async Task<Book?> GetByIdAsync(int id)
    {
        const string sql = """
            SELECT b.book_id BookId, b.isbn Isbn, b.title Title,
                   b.publisher_id PublisherId, b.genre_id GenreId,
                   b.publication_year PublicationYear, b.language Language,
                   b.page_count PageCount, b.total_copies TotalCopies,
                   b.available_copies AvailableCopies
            FROM books b WHERE b.book_id = @id
            """;
        using var conn = ctx.CreateConnection();
        return await conn.QueryFirstOrDefaultAsync<Book>(sql, new { id });
    }

    public async Task<IEnumerable<int>> GetAuthorIdsAsync(int bookId)
    {
        using var conn = ctx.CreateConnection();
        return await conn.QueryAsync<int>(
            "SELECT author_id FROM book_authors WHERE book_id = @bookId", new { bookId });
    }

    public async Task<int> CreateAsync(Book book)
    {
        const string sql = """
            INSERT INTO books (isbn, title, publisher_id, genre_id, publication_year,
                               language, page_count, total_copies, available_copies)
            VALUES (@Isbn, @Title, @PublisherId, @GenreId, @PublicationYear,
                    @Language, @PageCount, @TotalCopies, @TotalCopies);
            SELECT CAST(SCOPE_IDENTITY() AS INT);
            """;
        using var conn = ctx.CreateConnection();
        await conn.OpenAsync();
        using var tx = conn.BeginTransaction();
        int bookId = await conn.ExecuteScalarAsync<int>(sql, book, tx);
        foreach (var authorId in book.SelectedAuthorIds)
            await conn.ExecuteAsync(
                "INSERT INTO book_authors (book_id, author_id) VALUES (@bookId, @authorId)",
                new { bookId, authorId }, tx);
        tx.Commit();
        return bookId;
    }

    public async Task UpdateAsync(Book book)
    {
        const string sql = """
            UPDATE books SET isbn=@Isbn, title=@Title, publisher_id=@PublisherId,
                genre_id=@GenreId, publication_year=@PublicationYear, language=@Language,
                page_count=@PageCount, total_copies=@TotalCopies
            WHERE book_id=@BookId
            """;
        using var conn = ctx.CreateConnection();
        await conn.OpenAsync();
        using var tx = conn.BeginTransaction();
        await conn.ExecuteAsync(sql, book, tx);
        await conn.ExecuteAsync("DELETE FROM book_authors WHERE book_id=@BookId", book, tx);
        foreach (var authorId in book.SelectedAuthorIds)
            await conn.ExecuteAsync(
                "INSERT INTO book_authors (book_id, author_id) VALUES (@BookId, @authorId)",
                new { book.BookId, authorId }, tx);
        tx.Commit();
    }

    public async Task DeleteAsync(int id)
    {
        using var conn = ctx.CreateConnection();
        await conn.ExecuteAsync(
            "DELETE FROM book_authors WHERE book_id=@id; DELETE FROM books WHERE book_id=@id",
            new { id });
    }

    public async Task<IEnumerable<Author>> GetAllAuthorsAsync()
    {
        using var conn = ctx.CreateConnection();
        return await conn.QueryAsync<Author>(
            "SELECT author_id AuthorId, first_name FirstName, last_name LastName FROM authors ORDER BY last_name");
    }

    public async Task<IEnumerable<Publisher>> GetAllPublishersAsync()
    {
        using var conn = ctx.CreateConnection();
        return await conn.QueryAsync<Publisher>(
            "SELECT publisher_id PublisherId, name Name FROM publishers ORDER BY name");
    }

    public async Task<IEnumerable<Genre>> GetAllGenresAsync()
    {
        using var conn = ctx.CreateConnection();
        return await conn.QueryAsync<Genre>(
            "SELECT genre_id GenreId, name Name FROM genres ORDER BY name");
    }
}
