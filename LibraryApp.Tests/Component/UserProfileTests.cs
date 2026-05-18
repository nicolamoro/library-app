using System.Security.Claims;
using Bunit;
using LibraryApp.Components.Pages;
using LibraryApp.Data;
using LibraryApp.Models;
using Microsoft.AspNetCore.Components.Authorization;
using Microsoft.Extensions.DependencyInjection;
using MudBlazor;
using MudBlazor.Services;
using NSubstitute;

namespace LibraryApp.Tests.Component;

[Trait("Category", "Component")]
public class UserProfileTests : IAsyncDisposable
{
    private readonly BunitContext _ctx;
    private readonly IUserRepository _mockRepo;

    private static readonly User SeedUser = new()
    {
        UserId = 7,
        FirstName = "Mario",
        LastName = "Rossi",
        Email = "mario.rossi@email.it",
        Status = "active",
        IsAdmin = false,
        RegistrationDate = new DateOnly(2024, 1, 1),
    };

    public UserProfileTests()
    {
        _ctx = new BunitContext();
        _ctx.JSInterop.Mode = JSRuntimeMode.Loose;
        _ctx.Services.AddMudServices(config => config.PopoverOptions.CheckForPopoverProvider = false);
        _ctx.Services.AddAuthorizationCore();

        _mockRepo = Substitute.For<IUserRepository>();
        _mockRepo.GetByIdAsync(SeedUser.UserId)
            .Returns(Task.FromResult<User?>(SeedUser));

        _ctx.Services.AddScoped<IUserRepository>(_ => _mockRepo);
    }

    public async ValueTask DisposeAsync() => await _ctx.DisposeAsync();

    private void SetUser(string email, string role, params Claim[] extraClaims)
    {
        var claims = new List<Claim>
        {
            new(ClaimTypes.Name, email),
            new(ClaimTypes.Role, role),
        };
        claims.AddRange(extraClaims);
        _ctx.Services.AddScoped<AuthenticationStateProvider>(_ =>
            new FakeAuthStateProvider("Test", [.. claims]));
    }

    [Fact]
    public async Task UserProfile_WithUserId_CallsGetByIdAsync()
    {
        SetUser(SeedUser.Email, "user", new Claim("user_id", SeedUser.UserId.ToString()));

        _ctx.Render<UserProfile>();
        await Task.Delay(200);

        await _mockRepo.Received().GetByIdAsync(SeedUser.UserId);
    }

    [Fact]
    public async Task UserProfile_WithoutUserId_DoesNotCallRepository()
    {
        SetUser(SeedUser.Email, "user");

        _ctx.Render<UserProfile>();
        await Task.Delay(200);

        await _mockRepo.DidNotReceiveWithAnyArgs().GetByIdAsync(default);
    }

    [Fact]
    public async Task UserProfile_OnSave_CallsUpdateProfileAsync()
    {
        SetUser(SeedUser.Email, "user", new Claim("user_id", SeedUser.UserId.ToString()));

        var cut = _ctx.Render<UserProfile>();
        cut.WaitForState(() => cut.Markup.Contains("Salva"), TimeSpan.FromSeconds(3));

        var saveButton = cut.FindAll("button").FirstOrDefault(b => b.TextContent.Contains("Salva"));
        Assert.NotNull(saveButton);
        saveButton.Click();
        await Task.Delay(200);

        await _mockRepo.Received().UpdateProfileAsync(
            SeedUser.UserId,
            Arg.Any<string>(), Arg.Any<string>(),
            Arg.Any<DateOnly?>(), Arg.Any<string?>(), Arg.Any<string?>(), Arg.Any<string?>(),
            Arg.Any<string?>());
    }

    [Fact]
    public async Task UserProfile_PasswordMismatch_DoesNotCallUpdateProfileAsync()
    {
        SetUser(SeedUser.Email, "user", new Claim("user_id", SeedUser.UserId.ToString()));

        var cut = _ctx.Render<UserProfile>();
        cut.WaitForState(() => cut.Markup.Contains("Salva"), TimeSpan.FromSeconds(3));

        var passwordInputs = cut.FindAll("input[type='password']");
        Assert.True(passwordInputs.Count >= 2);
        passwordInputs[0].Change("password123");
        passwordInputs[1].Change("different456");

        var saveButton = cut.FindAll("button").FirstOrDefault(b => b.TextContent.Contains("Salva"));
        Assert.NotNull(saveButton);
        saveButton.Click();
        await Task.Delay(200);

        await _mockRepo.DidNotReceiveWithAnyArgs().UpdateProfileAsync(
            default, default!, default!, default, default, default, default, default);
    }
}

file sealed class FakeAuthStateProvider : AuthenticationStateProvider
{
    private readonly AuthenticationState _state;

    public FakeAuthStateProvider(string authenticationType, params Claim[] claims) =>
        _state = new AuthenticationState(new ClaimsPrincipal(new ClaimsIdentity(claims, authenticationType)));

    public override Task<AuthenticationState> GetAuthenticationStateAsync() => Task.FromResult(_state);
}
