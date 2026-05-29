using Dapper;
using LibraryApp.Data;
using Microsoft.Extensions.Configuration;
using Npgsql;
using Testcontainers.PostgreSql;

namespace LibraryApp.Tests.Integration.Fixtures;

public class PostgresFixture : IAsyncLifetime
{
    private readonly PostgreSqlContainer _container = new PostgreSqlBuilder("postgres:16-alpine")
        .WithDatabase("librarydb")
        .WithUsername("postgres")
        .WithPassword("StrongPass123!")
        .Build();

    public DapperContext DapperContext { get; private set; } = null!;

    public async Task InitializeAsync()
    {
        await _container.StartAsync();

        // Npgsql returns `date` columns as DateTime; map them to DateOnly.
        SqlMapper.AddTypeHandler(new DateOnlyTypeHandler());

        var connStr = _container.GetConnectionString();

        // The database already exists (created by the container). Run the init
        // scripts in order against it. Each file is executed as a single script:
        // PostgreSQL has no GO batch separator, and the PL/pgSQL function bodies
        // contain semicolons inside $$...$$ so they must not be split on ';'.
        await ExecuteScriptAsync(connStr, ScriptPath("01_schema.sql"));
        await ExecuteScriptAsync(connStr, ScriptPath("02_procedures.sql"));
        await ExecuteScriptAsync(connStr, ScriptPath("03_seed.sql"));

        var config = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?> { ["ConnectionStrings:LibraryDb"] = connStr })
            .Build();

        DapperContext = new DapperContext(config);
    }

    public async Task DisposeAsync() => await _container.DisposeAsync();

    // --- helpers ---

    private static string ScriptPath(string fileName)
    {
        var dir = new DirectoryInfo(AppContext.BaseDirectory);
        while (dir is not null && !File.Exists(Path.Combine(dir.FullName, "library-app.sln")))
            dir = dir.Parent;
        return Path.Combine(dir?.FullName
            ?? throw new InvalidOperationException("Cannot find solution root"), "db", "init", fileName);
    }

    private static async Task ExecuteScriptAsync(string connectionString, string scriptPath)
    {
        var sql = await File.ReadAllTextAsync(scriptPath);

        await using var conn = new NpgsqlConnection(connectionString);
        await conn.OpenAsync();
        await conn.ExecuteAsync(sql, commandTimeout: 60);
    }
}

[CollectionDefinition("Postgres")]
public class PostgresCollection : ICollectionFixture<PostgresFixture> { }
