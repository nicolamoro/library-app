using System.Security.Claims;
using Bunit;
using LibraryApp.Components.Pages.Books;
using LibraryApp.Data;
using LibraryApp.Models;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Components.Authorization;
using Microsoft.Extensions.DependencyInjection;
using MudBlazor;
using MudBlazor.Services;
using NSubstitute;

namespace LibraryApp.Tests.Component;

[Trait("Category", "Component")]
public class BookListTests : IAsyncDisposable
{
    private readonly BunitContext _ctx;
    private readonly IBookRepository _mockRepo;

    public BookListTests()
    {
        _ctx = new BunitContext();
        _ctx.JSInterop.Mode = JSRuntimeMode.Loose;
        _ctx.Services.AddMudServices(config => config.PopoverOptions.CheckForPopoverProvider = false);
        _ctx.Services.AddAuthorizationCore();
        _ctx.Services.AddScoped<AuthenticationStateProvider>(_ =>
            new FakeAuthStateProvider("Test",
                new Claim(ClaimTypes.Name, "admin"),
                new Claim(ClaimTypes.Role, "admin")));

        _mockRepo = Substitute.For<IBookRepository>();
        _mockRepo.GetPagedAsync(Arg.Any<int>(), Arg.Any<int>(), Arg.Any<string?>(), Arg.Any<string?>(), Arg.Any<bool>())
            .Returns(Task.FromResult<(IEnumerable<Book>, int)>(([], 0)));

        _ctx.Services.AddScoped<IBookRepository>(_ => _mockRepo);
    }

    public async ValueTask DisposeAsync() => await _ctx.DisposeAsync();

    [Fact]
    public void BookList_Renders_WithoutException()
    {
        var exception = Record.Exception(() => _ctx.Render<BookList>());
        Assert.Null(exception);
    }

    [Fact]
    public async Task BookList_OnRender_CallsGetPagedAsync()
    {
        _ctx.Render<BookList>();
        await Task.Delay(200);

        await _mockRepo.Received().GetPagedAsync(
            Arg.Any<int>(), Arg.Any<int>(), Arg.Any<string?>(), Arg.Any<string?>(), Arg.Any<bool>());
    }

    [Fact]
    public void BookList_WithBooks_RendersBookTitles()
    {
        var books = new List<Book>
        {
            new() { BookId = 1, Title = "Il Nome della Rosa", TotalCopies = 2, AvailableCopies = 1 },
            new() { BookId = 2, Title = "1984",              TotalCopies = 3, AvailableCopies = 3 },
        };

        _mockRepo.GetPagedAsync(Arg.Any<int>(), Arg.Any<int>(), Arg.Any<string?>(), Arg.Any<string?>(), Arg.Any<bool>())
            .Returns(Task.FromResult<(IEnumerable<Book>, int)>((books, books.Count)));

        var cut = _ctx.Render<BookList>();
        cut.WaitForState(() => cut.Markup.Contains("Il Nome della Rosa"), TimeSpan.FromSeconds(3));

        Assert.Contains("Il Nome della Rosa", cut.Markup);
        Assert.Contains("1984", cut.Markup);
    }
}

file sealed class FakeAuthStateProvider : AuthenticationStateProvider
{
    private readonly AuthenticationState _state;

    public FakeAuthStateProvider(string authenticationType, params Claim[] claims) =>
        _state = new AuthenticationState(new ClaimsPrincipal(new ClaimsIdentity(claims, authenticationType)));

    public override Task<AuthenticationState> GetAuthenticationStateAsync() => Task.FromResult(_state);
}
