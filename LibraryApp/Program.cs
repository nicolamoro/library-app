using Dapper;
using LibraryApp.Components;
using LibraryApp.Data;
using Microsoft.AspNetCore.Authentication.Cookies;
using MudBlazor.Services;

SqlMapper.AddTypeHandler(new DateOnlyTypeHandler());

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddRazorComponents()
    .AddInteractiveServerComponents();

builder.Services.AddMudServices();

builder.Services.AddScoped<DapperContext>();
builder.Services.AddScoped<BookRepository>();
builder.Services.AddScoped<UserRepository>();
builder.Services.AddScoped<LoanRepository>();
builder.Services.AddScoped<AuthorRepository>();
builder.Services.AddScoped<GenreRepository>();
builder.Services.AddScoped<PublisherRepository>();

builder.Services.AddRazorPages();

builder.Services.AddAuthentication(CookieAuthenticationDefaults.AuthenticationScheme)
    .AddCookie(options =>
    {
        options.LoginPath         = "/login";
        options.AccessDeniedPath  = "/access-denied";
        options.Cookie.HttpOnly   = true;
        options.Cookie.SameSite   = SameSiteMode.Strict;
        options.ExpireTimeSpan    = TimeSpan.FromHours(8);
        options.SlidingExpiration = true;
    });

builder.Services.AddAuthorization();

var app = builder.Build();

if (!app.Environment.IsDevelopment())
{
    app.UseExceptionHandler("/Error", createScopeForErrors: true);
    app.UseHsts();
}

app.UseHttpsRedirection();
app.UseStaticFiles();

app.UseAuthentication();
app.UseAuthorization();

app.UseAntiforgery();

app.MapRazorPages();

app.MapRazorComponents<App>()
    .AddInteractiveServerRenderMode();

app.Run();
