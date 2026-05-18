using Dapper;
using LibraryApp.Models;

namespace LibraryApp.Data;

public class UserRepository(DapperContext ctx) : IUserRepository
{
    private const string SelectCols = """
        user_id UserId, first_name FirstName, last_name LastName,
        birth_date BirthDate, tax_code TaxCode, address Address,
        phone Phone, email Email, registration_date RegistrationDate, status Status,
        is_admin IsAdmin, last_login LastLogin
        """;

    // --- Auth ---

    public async Task<User?> GetByEmailAsync(string email)
    {
        using var conn = ctx.CreateConnection();
        return await conn.QueryFirstOrDefaultAsync<User>($"""
            SELECT {SelectCols}, password_hash PasswordHash
            FROM users WHERE email = @email
            """, new { email });
    }

    public async Task UpdateLastLoginAsync(int userId)
    {
        using var conn = ctx.CreateConnection();
        await conn.ExecuteAsync(
            "UPDATE users SET last_login = SYSUTCDATETIME() WHERE user_id = @userId",
            new { userId });
    }

    // --- CRUD ---

    public async Task<IEnumerable<User>> GetAllAsync()
    {
        using var conn = ctx.CreateConnection();
        return await conn.QueryAsync<User>(
            $"SELECT {SelectCols} FROM users ORDER BY last_name, first_name");
    }

    private static readonly Dictionary<string, string> _userSortMap = new()
    {
        ["name"] = "last_name, first_name",
        ["email"] = "email",
        ["role"] = "is_admin",
        ["status"] = "status",
        ["lastlogin"] = "last_login",
    };

    public async Task<(IEnumerable<User> Items, int Total)> GetPagedAsync(
        int page, int pageSize, string? search, string? sortBy = null, bool sortDescending = false)
    {
        var offset = page * pageSize;
        var col = _userSortMap.GetValueOrDefault(sortBy ?? "", "last_name, first_name");
        var dir = sortDescending ? "DESC" : "ASC";
        var orderBy = string.Join(", ", col.Split(',').Select(c => $"{c.Trim()} {dir}"));
        var param = new
        {
            Search = string.IsNullOrWhiteSpace(search) ? null : search,
            Offset = offset,
            PageSize = pageSize
        };

        const string where = """
            WHERE @Search IS NULL
               OR first_name + ' ' + last_name  LIKE '%' + @Search + '%'
               OR last_name  + ' ' + first_name  LIKE '%' + @Search + '%'
               OR email                          LIKE '%' + @Search + '%'
            """;

        var sql = $"""
            SELECT COUNT(*) FROM users {where};
            SELECT {SelectCols} FROM users {where}
            ORDER BY {orderBy}
            OFFSET @Offset ROWS FETCH NEXT @PageSize ROWS ONLY;
            """;

        using var conn = ctx.CreateConnection();
        using var multi = await conn.QueryMultipleAsync(sql, param);
        var total = await multi.ReadFirstAsync<int>();
        var items = await multi.ReadAsync<User>();
        return (items, total);
    }

    public async Task<User?> GetByIdAsync(int id)
    {
        using var conn = ctx.CreateConnection();
        return await conn.QueryFirstOrDefaultAsync<User>(
            $"SELECT {SelectCols} FROM users WHERE user_id = @id", new { id });
    }

    public async Task<int> CreateAsync(User u, string plainPassword)
    {
        var hash = BCrypt.Net.BCrypt.HashPassword(plainPassword, workFactor: 12);
        const string sql = """
            INSERT INTO users
                (first_name, last_name, birth_date, tax_code, address, phone, email, status,
                 password_hash, is_admin)
            VALUES
                (@FirstName, @LastName, @BirthDate, @TaxCode, @Address, @Phone, @Email, @Status,
                 @Hash, @IsAdmin);
            SELECT CAST(SCOPE_IDENTITY() AS INT);
            """;
        using var conn = ctx.CreateConnection();
        return await conn.ExecuteScalarAsync<int>(sql,
            new
            {
                u.FirstName,
                u.LastName,
                u.BirthDate,
                u.TaxCode,
                u.Address,
                u.Phone,
                u.Email,
                u.Status,
                Hash = hash,
                u.IsAdmin
            });
    }

    public async Task UpdateAsync(User u, string? newPassword = null)
    {
        const string sql = """
            UPDATE users SET
                first_name = @FirstName, last_name = @LastName,
                birth_date = @BirthDate, tax_code  = @TaxCode,
                address    = @Address,  phone      = @Phone,
                status     = @Status,   is_admin   = @IsAdmin
            WHERE user_id = @UserId
            """;
        using var conn = ctx.CreateConnection();
        await conn.ExecuteAsync(sql, u);

        if (newPassword is not null)
        {
            var hash = BCrypt.Net.BCrypt.HashPassword(newPassword, workFactor: 12);
            await conn.ExecuteAsync(
                "UPDATE users SET password_hash = @hash WHERE user_id = @id",
                new { hash, id = u.UserId });
        }
    }

    public async Task DeleteAsync(int id)
    {
        using var conn = ctx.CreateConnection();
        await conn.ExecuteAsync("DELETE FROM users WHERE user_id = @id", new { id });
    }
}
