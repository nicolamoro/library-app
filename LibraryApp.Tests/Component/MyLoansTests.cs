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
public class MyLoansTests : IAsyncDisposable
{
    private readonly BunitContext _ctx;
    private readonly ILoanRepository _mockRepo;

    public MyLoansTests()
    {
        _ctx = new BunitContext();
        _ctx.JSInterop.Mode = JSRuntimeMode.Loose;
        _ctx.Services.AddMudServices(config => config.PopoverOptions.CheckForPopoverProvider = false);
        _ctx.Services.AddAuthorizationCore();

        _mockRepo = Substitute.For<ILoanRepository>();
        _mockRepo.GetByUserIdPagedAsync(Arg.Any<int>(), Arg.Any<int>(), Arg.Any<int>(), Arg.Any<string>(), Arg.Any<string?>(), Arg.Any<bool>())
            .Returns(Task.FromResult<(IEnumerable<LoanDetail>, int)>(([], 0)));

        _ctx.Services.AddScoped<ILoanRepository>(_ => _mockRepo);
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
    public void MyLoans_Renders_WithoutException()
    {
        SetUser("mario.rossi@email.it", "user", new Claim("user_id", "2"));
        var exception = Record.Exception(() => _ctx.Render<MyLoans>());
        Assert.Null(exception);
    }

    [Fact]
    public async Task MyLoans_WithUserId_CallsGetByUserIdPagedAsync()
    {
        const int userId = 42;
        SetUser("test@email.it", "user", new Claim("user_id", userId.ToString()));

        _ctx.Render<MyLoans>();
        await Task.Delay(200);

        await _mockRepo.Received().GetByUserIdPagedAsync(
            userId,
            Arg.Any<int>(), Arg.Any<int>(), Arg.Any<string>(), Arg.Any<string?>(), Arg.Any<bool>());
    }

    [Fact]
    public async Task MyLoans_WithoutUserId_DoesNotCallRepository()
    {
        // No "user_id" claim → _userId will be 0
        SetUser("test@email.it", "user");

        _ctx.Render<MyLoans>();
        await Task.Delay(200);

        await _mockRepo.DidNotReceiveWithAnyArgs().GetByUserIdPagedAsync(
            default, default, default, "all", string.Empty, default);
    }

    [Fact]
    public void MyLoans_WithLoans_RendersBookTitles()
    {
        const int userId = 5;
        var loans = new List<LoanDetail>
        {
            new()
            {
                LoanId = 1, UserId = userId, BookId = 1,
                BookTitle = "Il Signore degli Anelli",
                UserFullName = "Test User",
                LoanDate = DateOnly.FromDateTime(DateTime.Today.AddDays(-20)),
                DueDate = DateOnly.FromDateTime(DateTime.Today.AddDays(10)),
                Status = "active",
                DailyFineRate = 0.50m,
            }
        };

        _mockRepo.GetByUserIdPagedAsync(userId, Arg.Any<int>(), Arg.Any<int>(), Arg.Any<string>(), Arg.Any<string?>(), Arg.Any<bool>())
            .Returns(Task.FromResult<(IEnumerable<LoanDetail>, int)>((loans, loans.Count)));

        SetUser("user@email.it", "user", new Claim("user_id", userId.ToString()));

        var cut = _ctx.Render<MyLoans>();
        cut.WaitForState(() => cut.Markup.Contains("Il Signore degli Anelli"), TimeSpan.FromSeconds(3));

        Assert.Contains("Il Signore degli Anelli", cut.Markup);
    }
}

file sealed class FakeAuthStateProvider : AuthenticationStateProvider
{
    private readonly AuthenticationState _state;

    public FakeAuthStateProvider(string authenticationType, params Claim[] claims) =>
        _state = new AuthenticationState(new ClaimsPrincipal(new ClaimsIdentity(claims, authenticationType)));

    public override Task<AuthenticationState> GetAuthenticationStateAsync() => Task.FromResult(_state);
}
