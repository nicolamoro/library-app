using Dapper;
using LibraryApp.Models;

namespace LibraryApp.Data;

public class CustomerRepository(DapperContext ctx)
{
    private const string SelectCols = """
        customer_id CustomerId, first_name FirstName, last_name LastName,
        birth_date BirthDate, tax_code TaxCode, address Address,
        phone Phone, email Email, registration_date RegistrationDate, status Status
        """;

    public async Task<IEnumerable<Customer>> GetAllAsync()
    {
        using var conn = ctx.CreateConnection();
        return await conn.QueryAsync<Customer>(
            $"SELECT {SelectCols} FROM customers ORDER BY last_name, first_name");
    }

    public async Task<(IEnumerable<Customer> Items, int Total)> GetPagedAsync(int page, int pageSize, string? search)
    {
        var offset = page * pageSize;
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
               OR ISNULL(email,'')               LIKE '%' + @Search + '%'
            """;

        var sql = $"""
            SELECT COUNT(*) FROM customers {where};
            SELECT {SelectCols} FROM customers {where}
            ORDER BY last_name, first_name
            OFFSET @Offset ROWS FETCH NEXT @PageSize ROWS ONLY;
            """;

        using var conn = ctx.CreateConnection();
        using var multi = await conn.QueryMultipleAsync(sql, param);
        var total = await multi.ReadFirstAsync<int>();
        var items = await multi.ReadAsync<Customer>();
        return (items, total);
    }

    public async Task<Customer?> GetByIdAsync(int id)
    {
        using var conn = ctx.CreateConnection();
        return await conn.QueryFirstOrDefaultAsync<Customer>(
            $"SELECT {SelectCols} FROM customers WHERE customer_id=@id", new { id });
    }

    public async Task<int> CreateAsync(Customer c)
    {
        const string sql = """
            INSERT INTO customers (first_name, last_name, birth_date, tax_code, address, phone, email, status)
            VALUES (@FirstName, @LastName, @BirthDate, @TaxCode, @Address, @Phone, @Email, @Status);
            SELECT CAST(SCOPE_IDENTITY() AS INT);
            """;
        using var conn = ctx.CreateConnection();
        return await conn.ExecuteScalarAsync<int>(sql, c);
    }

    public async Task UpdateAsync(Customer c)
    {
        const string sql = """
            UPDATE customers SET first_name=@FirstName, last_name=@LastName,
                birth_date=@BirthDate, tax_code=@TaxCode, address=@Address,
                phone=@Phone, email=@Email, status=@Status
            WHERE customer_id=@CustomerId
            """;
        using var conn = ctx.CreateConnection();
        await conn.ExecuteAsync(sql, c);
    }

    public async Task DeleteAsync(int id)
    {
        using var conn = ctx.CreateConnection();
        await conn.ExecuteAsync("DELETE FROM customers WHERE customer_id=@id", new { id });
    }
}
