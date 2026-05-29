using Npgsql;

namespace LibraryApp.Data;

public class DapperContext
{
    private readonly string _connectionString;

    public DapperContext(IConfiguration configuration)
        => _connectionString = configuration.GetConnectionString("LibraryDb")!;

    public NpgsqlConnection CreateConnection() => new(_connectionString);
}
