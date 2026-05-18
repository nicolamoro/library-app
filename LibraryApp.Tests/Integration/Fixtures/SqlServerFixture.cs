using System.Text.RegularExpressions;
using Dapper;
using LibraryApp.Data;
using Microsoft.Data.SqlClient;
using Microsoft.Extensions.Configuration;
using Testcontainers.MsSql;

namespace LibraryApp.Tests.Integration.Fixtures;

public class SqlServerFixture : IAsyncLifetime
{
    private readonly MsSqlContainer _container = new MsSqlBuilder("mcr.microsoft.com/mssql/server:2022-latest")
        .Build();

    public DapperContext DapperContext { get; private set; } = null!;

    public async Task InitializeAsync()
    {
        await _container.StartAsync();

        SqlMapper.AddTypeHandler(new DateOnlyTypeHandler());

        var masterConnStr = _container.GetConnectionString();

        // Run DDL (creates LibraryDB and all tables)
        await ExecuteScriptAsync(masterConnStr, ScriptPath("01_schema.sql"));

        // Subsequent scripts run against LibraryDB
        var libraryConnStr = new SqlConnectionStringBuilder(masterConnStr)
        {
            InitialCatalog = "LibraryDB"
        }.ConnectionString;

        await ExecuteScriptAsync(libraryConnStr, ScriptPath("02_procedures.sql"));
        await ExecuteScriptAsync(libraryConnStr, ScriptPath("03_seed.sql"));

        var config = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?> { ["ConnectionStrings:LibraryDb"] = libraryConnStr })
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
        var batches = Regex.Split(sql, @"^\s*GO\s*$", RegexOptions.Multiline | RegexOptions.IgnoreCase);

        await using var conn = new SqlConnection(connectionString);
        await conn.OpenAsync();

        foreach (var batch in batches)
        {
            var trimmed = batch.Trim();
            if (!string.IsNullOrWhiteSpace(trimmed))
                await conn.ExecuteAsync(trimmed, commandTimeout: 60);
        }
    }
}

[CollectionDefinition("SqlServer")]
public class SqlServerCollection : ICollectionFixture<SqlServerFixture> { }
