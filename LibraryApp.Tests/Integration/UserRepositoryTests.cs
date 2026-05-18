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
}
