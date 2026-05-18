using Dapper;
using LibraryApp.Data;
using LibraryApp.Tests.Integration.Fixtures;

namespace LibraryApp.Tests.Integration;

[Trait("Category", "Integration")]
[Collection("SqlServer")]
public class UserRepositoryTests(SqlServerFixture fixture)
{
    private readonly UserRepository _repo = new(fixture.DapperContext);

    [Fact]
    public async Task GetByEmailAsync_ExistingAdmin_ReturnsUserWithHash()
    {
        var user = await _repo.GetByEmailAsync("admin@email.it");
        Assert.NotNull(user);
        Assert.True(user.IsAdmin);
        Assert.NotNull(user.PasswordHash);
    }

    [Fact]
    public async Task GetByEmailAsync_ExistingRegularUser_ReturnsUser()
    {
        var user = await _repo.GetByEmailAsync("mario.rossi@email.it");
        Assert.NotNull(user);
        Assert.False(user.IsAdmin);
        Assert.Equal("active", user.Status);
    }

    [Fact]
    public async Task GetByEmailAsync_NonExistingEmail_ReturnsNull()
    {
        var user = await _repo.GetByEmailAsync("nobody@nowhere.test");
        Assert.Null(user);
    }

    [Fact]
    public async Task GetPagedAsync_NoSearch_ReturnsAllSeededUsers()
    {
        var (items, total) = await _repo.GetPagedAsync(0, 100, null);
        Assert.True(total > 0);
        Assert.NotEmpty(items);
    }

    [Fact]
    public async Task GetPagedAsync_SearchByEmail_FiltersResults()
    {
        var (items, total) = await _repo.GetPagedAsync(0, 100, "mario.rossi");
        Assert.True(total >= 1);
        Assert.Contains(items, u => u.Email.Contains("mario.rossi"));
    }

    [Fact]
    public async Task UpdateProfileAsync_ChangesAllowedFields_PersistsCorrectly()
    {
        var user = await _repo.GetByEmailAsync("mario.rossi@email.it");
        Assert.NotNull(user);

        await _repo.UpdateProfileAsync(
            user.UserId,
            firstName: "Marietto",
            lastName: "Rossini",
            birthDate: new DateOnly(1990, 6, 15),
            taxCode: "RSSXXX90H15",
            address: "Via Roma 99",
            phone: "3331234567");

        var updated = await _repo.GetByIdAsync(user.UserId);
        Assert.NotNull(updated);
        Assert.Equal("Marietto", updated.FirstName);
        Assert.Equal("Rossini", updated.LastName);
        Assert.Equal(new DateOnly(1990, 6, 15), updated.BirthDate);
        Assert.Equal("RSSXXX90H15", updated.TaxCode);
        Assert.Equal("Via Roma 99", updated.Address);
        Assert.Equal("3331234567", updated.Phone);
    }

    [Fact]
    public async Task UpdateProfileAsync_DoesNotModify_StatusOrIsAdminOrEmail()
    {
        var user = await _repo.GetByEmailAsync("mario.rossi@email.it");
        Assert.NotNull(user);

        using var conn = fixture.DapperContext.CreateConnection();
        var before = await conn.QueryFirstAsync(
            "SELECT status, is_admin, email FROM users WHERE user_id = @id",
            new { id = user.UserId });

        await _repo.UpdateProfileAsync(
            user.UserId,
            firstName: "Test",
            lastName: "Test",
            birthDate: user.BirthDate,
            taxCode: user.TaxCode,
            address: user.Address,
            phone: user.Phone);

        var after = await conn.QueryFirstAsync(
            "SELECT status, is_admin, email FROM users WHERE user_id = @id",
            new { id = user.UserId });

        Assert.Equal((string)before.status, (string)after.status);
        Assert.Equal((bool)before.is_admin, (bool)after.is_admin);
        Assert.Equal((string)before.email, (string)after.email);
    }
}
