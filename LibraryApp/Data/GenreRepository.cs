using Dapper;
using LibraryApp.Models;

namespace LibraryApp.Data;

public class GenreRepository(DapperContext ctx)
{
    private static readonly Dictionary<string, string> _sortMap = new()
    {
        ["name"] = "Name",
    };

    public async Task<(IEnumerable<Genre> Items, int Total)> GetPagedAsync(
        int page, int pageSize, string? search, string? sortBy = null, bool sortDescending = false)
    {
        var offset = page * pageSize;
        var col = _sortMap.GetValueOrDefault(sortBy ?? "", "Name");
        var dir = sortDescending ? "DESC" : "ASC";
        var param = new
        {
            Search = string.IsNullOrWhiteSpace(search) ? null : search,
            Offset = offset,
            PageSize = pageSize
        };

        const string cte = """
            WITH cte AS (
                SELECT genre_id GenreId, name Name, description Description
                FROM genres
            )
            """;

        const string where = """
            WHERE @Search IS NULL
               OR Name                        LIKE '%' + @Search + '%'
               OR ISNULL(Description,'')      LIKE '%' + @Search + '%'
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
        var items = await multi.ReadAsync<Genre>();
        return (items, total);
    }

    public async Task<Genre?> GetByIdAsync(int id)
    {
        using var conn = ctx.CreateConnection();
        return await conn.QueryFirstOrDefaultAsync<Genre>(
            "SELECT genre_id GenreId, name Name, description Description FROM genres WHERE genre_id = @id",
            new { id });
    }

    public async Task<int> CreateAsync(Genre genre)
    {
        const string sql = """
            INSERT INTO genres (name, description) VALUES (@Name, @Description);
            SELECT CAST(SCOPE_IDENTITY() AS INT);
            """;
        using var conn = ctx.CreateConnection();
        return await conn.ExecuteScalarAsync<int>(sql, genre);
    }

    public async Task UpdateAsync(Genre genre)
    {
        using var conn = ctx.CreateConnection();
        await conn.ExecuteAsync(
            "UPDATE genres SET name=@Name, description=@Description WHERE genre_id=@GenreId",
            genre);
    }

    public async Task DeleteAsync(int id)
    {
        using var conn = ctx.CreateConnection();
        var refCount = await conn.ExecuteScalarAsync<int>(
            "SELECT COUNT(*) FROM books WHERE genre_id = @id", new { id });
        if (refCount > 0)
            throw new InvalidOperationException("Il genere è associato a uno o più libri e non può essere eliminato.");
        await conn.ExecuteAsync("DELETE FROM genres WHERE genre_id = @id", new { id });
    }
}
