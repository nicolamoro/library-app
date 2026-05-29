using Dapper;
using LibraryApp.Models;

namespace LibraryApp.Data;

public class PublisherRepository(DapperContext ctx) : IPublisherRepository
{
    private static readonly Dictionary<string, string> _sortMap = new()
    {
        ["name"] = "Name",
    };

    public async Task<(IEnumerable<Publisher> Items, int Total)> GetPagedAsync(
        int page, int pageSize, string? search, string? sortBy = null, bool sortDescending = false)
    {
        var offset = page * pageSize;
        var col = _sortMap.GetValueOrDefault(sortBy ?? "", "Name");
        var dir = sortDescending ? "DESC" : "ASC";
        var param = new
        {
            Search = string.IsNullOrWhiteSpace(search) ? "" : search,
            Offset = offset,
            PageSize = pageSize
        };

        const string cte = """
            WITH cte AS (
                SELECT publisher_id PublisherId, name Name, address Address,
                       phone Phone, email Email, website Website
                FROM publishers
            )
            """;

        const string where = """
            WHERE @Search = ''
               OR Name ILIKE '%' || @Search || '%'
            """;

        var sql = $"""
            {cte} SELECT COUNT(*) FROM cte {where};
            {cte} SELECT * FROM cte {where}
            ORDER BY {col} {dir}
            OFFSET @Offset LIMIT @PageSize;
            """;

        using var conn = ctx.CreateConnection();
        using var multi = await conn.QueryMultipleAsync(sql, param);
        var total = await multi.ReadFirstAsync<int>();
        var items = await multi.ReadAsync<Publisher>();
        return (items, total);
    }

    public async Task<Publisher?> GetByIdAsync(int id)
    {
        using var conn = ctx.CreateConnection();
        return await conn.QueryFirstOrDefaultAsync<Publisher>(
            """
            SELECT publisher_id PublisherId, name Name, address Address,
                   phone Phone, email Email, website Website
            FROM publishers WHERE publisher_id = @id
            """, new { id });
    }

    public async Task<int> CreateAsync(Publisher publisher)
    {
        const string sql = """
            INSERT INTO publishers (name, address, phone, email, website)
            VALUES (@Name, @Address, @Phone, @Email, @Website)
            RETURNING publisher_id;
            """;
        using var conn = ctx.CreateConnection();
        return await conn.ExecuteScalarAsync<int>(sql, publisher);
    }

    public async Task UpdateAsync(Publisher publisher)
    {
        const string sql = """
            UPDATE publishers SET name=@Name, address=@Address, phone=@Phone,
                email=@Email, website=@Website
            WHERE publisher_id=@PublisherId
            """;
        using var conn = ctx.CreateConnection();
        await conn.ExecuteAsync(sql, publisher);
    }

    public async Task DeleteAsync(int id)
    {
        using var conn = ctx.CreateConnection();
        var refCount = await conn.ExecuteScalarAsync<int>(
            "SELECT COUNT(*) FROM books WHERE publisher_id = @id", new { id });
        if (refCount > 0)
            throw new InvalidOperationException("L'editore è associato a uno o più libri e non può essere eliminato.");
        await conn.ExecuteAsync("DELETE FROM publishers WHERE publisher_id = @id", new { id });
    }
}
