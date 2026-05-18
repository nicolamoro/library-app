using System.Security.Claims;
using Bunit;
using LibraryApp.Components.Pages.Loans;
using LibraryApp.Data;
using LibraryApp.Models;
using Microsoft.AspNetCore.Components.Authorization;
using Microsoft.Extensions.DependencyInjection;
using MudBlazor;
using MudBlazor.Services;
using NSubstitute;

namespace LibraryApp.Tests.Component;

[Trait("Category", "Component")]
public class LoanListTests : IAsyncDisposable
{
    private readonly BunitContext _ctx;
    private readonly ILoanRepository _mockRepo;

    public LoanListTests()
    {
        _ctx = new BunitContext();
        _ctx.JSInterop.Mode = JSRuntimeMode.Loose;
        _ctx.Services.AddMudServices(config => config.PopoverOptions.CheckForPopoverProvider = false);
        _ctx.Services.AddAuthorizationCore();
        _ctx.Services.AddScoped<AuthenticationStateProvider>(_ =>
            new FakeAuthStateProvider("Test",
                new Claim(ClaimTypes.Name, "admin"),
                new Claim(ClaimTypes.Role, "admin")));

        _mockRepo = Substitute.For<ILoanRepository>();
        _mockRepo.GetPagedAsync(Arg.Any<int>(), Arg.Any<int>(), Arg.Any<string>(), Arg.Any<string?>(), Arg.Any<bool>())
            .Returns(Task.FromResult<(IEnumerable<LoanDetail>, int)>(([], 0)));

        _ctx.Services.AddScoped<ILoanRepository>(_ => _mockRepo);
    }

    public async ValueTask DisposeAsync() => await _ctx.DisposeAsync();

    [Fact]
    public void LoanList_Renders_WithoutException()
    {
        var exception = Record.Exception(() => _ctx.Render<LoanList>());
        Assert.Null(exception);
    }

    [Fact]
    public async Task LoanList_OnRender_CallsGetPagedAsyncWithAllFilter()
    {
        _ctx.Render<LoanList>();
        await Task.Delay(200);

        await _mockRepo.Received().GetPagedAsync(
            Arg.Any<int>(), Arg.Any<int>(),
            "all",
            Arg.Any<string?>(), Arg.Any<bool>());
    }

    [Fact]
    public void LoanList_WithKnownFineAmount_RendersFineValue()
    {
        var loans = new List<LoanDetail>
        {
            new()
            {
                LoanId = 1, UserId = 1, BookId = 1,
                UserFullName = "Mario Rossi", BookTitle = "Test Book",
                LoanDate = DateOnly.FromDateTime(DateTime.Today.AddDays(-40)),
                DueDate = DateOnly.FromDateTime(DateTime.Today.AddDays(-10)),
                ReturnDate = DateOnly.FromDateTime(DateTime.Today.AddDays(-2)),
                Status = "returned",
                DailyFineRate = 0.50m,
                FineAmount = 4.00m,
                FinePaid = false,
            }
        };

        _mockRepo.GetPagedAsync(Arg.Any<int>(), Arg.Any<int>(), Arg.Any<string>(), Arg.Any<string?>(), Arg.Any<bool>())
            .Returns(Task.FromResult<(IEnumerable<LoanDetail>, int)>((loans, loans.Count)));

        var cut = _ctx.Render<LoanList>();
        cut.WaitForState(() => cut.Markup.Contains("4,00") || cut.Markup.Contains("4.00"), TimeSpan.FromSeconds(3));

        Assert.True(cut.Markup.Contains("4,00") || cut.Markup.Contains("4.00"));
    }

    [Fact]
    public void LoanList_LoanWithEstimatedFine_RendersEstimatedFine()
    {
        var loans = new List<LoanDetail>
        {
            new()
            {
                LoanId = 2, UserId = 1, BookId = 1,
                UserFullName = "Luigi Bianchi", BookTitle = "Another Book",
                LoanDate = DateOnly.FromDateTime(DateTime.Today.AddDays(-40)),
                DueDate = DateOnly.FromDateTime(DateTime.Today.AddDays(-5)),
                ReturnDate = null,
                Status = "overdue",
                DailyFineRate = 0.50m,
                FineAmount = null,
            }
        };

        _mockRepo.GetPagedAsync(Arg.Any<int>(), Arg.Any<int>(), Arg.Any<string>(), Arg.Any<string?>(), Arg.Any<bool>())
            .Returns(Task.FromResult<(IEnumerable<LoanDetail>, int)>((loans, loans.Count)));

        var cut = _ctx.Render<LoanList>();
        cut.WaitForState(() => cut.Markup.Contains("~€"), TimeSpan.FromSeconds(3));

        Assert.Contains("~€", cut.Markup);
    }
}

file sealed class FakeAuthStateProvider : AuthenticationStateProvider
{
    private readonly AuthenticationState _state;

    public FakeAuthStateProvider(string authenticationType, params Claim[] claims) =>
        _state = new AuthenticationState(new ClaimsPrincipal(new ClaimsIdentity(claims, authenticationType)));

    public override Task<AuthenticationState> GetAuthenticationStateAsync() => Task.FromResult(_state);
}
