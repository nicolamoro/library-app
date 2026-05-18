using Dapper;
using LibraryApp.Models;

namespace LibraryApp.Data;

public class AuthorRepository(DapperContext ctx) : IAuthorRepository
{
    private static readonly Dictionary<string, string> _sortMap = new()
    {
        ["lastname"] = "LastName",
        ["firstname"] = "FirstName",
        ["nationality"] = "Nationality",
        ["birthdate"] = "BirthDate",
    };

    public async Task<(IEnumerable<Author> Items, int Total)> GetPagedAsync(
        int page, int pageSize, string? search, string? sortBy = null, bool sortDescending = false)
    {
        var offset = page * pageSize;
        var col = _sortMap.GetValueOrDefault(sortBy ?? "", "LastName");
        var dir = sortDescending ? "DESC" : "ASC";
        var param = new
        {
            Search = string.IsNullOrWhiteSpace(search) ? null : search,
            Offset = offset,
            PageSize = pageSize
        };

        const string cte = """
            WITH cte AS (
                SELECT author_id AuthorId, first_name FirstName, last_name LastName,
                       birth_date BirthDate, nationality Nationality, biography Biography
                FROM authors
            )
            """;

        const string where = """
            WHERE @Search IS NULL
               OR FirstName               LIKE '%' + @Search + '%'
               OR LastName                LIKE '%' + @Search + '%'
               OR ISNULL(Nationality,'')  LIKE '%' + @Search + '%'
            """;

        var sql = $"""
            {cte} SELECT COUNT(*) FROM cte {where};
            {cte} SELECT * FROM cte {where}
            ORDER BY {col} {dir}
            OFFSET @Offset ROWS FETCH NEXT @PageSize ROWS ONLY;
            """;

        using var conn = ctx.CreateConnection();
        using var multi = await conn.QueryMultipleAsync(sql, param);
        var total = await multi.ReadFirstAsync<int>();
        var items = await multi.ReadAsync<Author>();
        return (items, total);
    }

    public async Task<Author?> GetByIdAsync(int id)
    {
        using var conn = ctx.CreateConnection();
        return await conn.QueryFirstOrDefaultAsync<Author>(
            """
            SELECT author_id AuthorId, first_name FirstName, last_name LastName,
                   birth_date BirthDate, nationality Nationality, biography Biography
            FROM authors WHERE author_id = @id
            """, new { id });
    }

    public async Task<int> CreateAsync(Author author)
    {
        const string sql = """
            INSERT INTO authors (first_name, last_name, birth_date, nationality, biography)
            VALUES (@FirstName, @LastName, @BirthDate, @Nationality, @Biography);
            SELECT CAST(SCOPE_IDENTITY() AS INT);
            """;
        using var conn = ctx.CreateConnection();
        return await conn.ExecuteScalarAsync<int>(sql, author);
    }

    public async Task UpdateAsync(Author author)
    {
        const string sql = """
            UPDATE authors SET first_name=@FirstName, last_name=@LastName,
                birth_date=@BirthDate, nationality=@Nationality, biography=@Biography
            WHERE author_id=@AuthorId
            """;
        using var conn = ctx.CreateConnection();
        await conn.ExecuteAsync(sql, author);
    }

    public async Task DeleteAsync(int id)
    {
        using var conn = ctx.CreateConnection();
        var refCount = await conn.ExecuteScalarAsync<int>(
            "SELECT COUNT(*) FROM book_authors WHERE author_id = @id", new { id });
        if (refCount > 0)
            throw new InvalidOperationException("L'autore è associato a uno o più libri e non può essere eliminato.");
        await conn.ExecuteAsync("DELETE FROM authors WHERE author_id = @id", new { id });
    }
}
