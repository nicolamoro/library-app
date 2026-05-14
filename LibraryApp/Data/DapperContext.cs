using Microsoft.Data.SqlClient;

namespace LibraryApp.Data;

public class DapperContext
{
    private readonly string _connectionString;

    public DapperContext(IConfiguration configuration)
        => _connectionString = configuration.GetConnectionString("LibraryDb")!;

    public SqlConnection CreateConnection() => new(_connectionString);
}
