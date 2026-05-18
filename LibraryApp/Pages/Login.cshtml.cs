using System.Security.Claims;
using LibraryApp.Data;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;

namespace LibraryApp.Pages;

public class LoginModel(IUserRepository userRepo) : PageModel
{
    [BindProperty] public string Email { get; set; } = string.Empty;
    [BindProperty] public string Password { get; set; } = string.Empty;
    public string ErrorMessage { get; set; } = string.Empty;

    public IActionResult OnGet()
    {
        if (User.Identity?.IsAuthenticated == true)
            return Redirect(User.IsInRole("admin") ? "/" : "/my-loans");
        return Page();
    }

    public async Task<IActionResult> OnPostAsync()
    {
        var user = await userRepo.GetByEmailAsync(Email);

        if (user is null
            || user.Status != "active"
            || user.PasswordHash is null
            || !BCrypt.Net.BCrypt.Verify(Password, user.PasswordHash))
        {
            ErrorMessage = "Credenziali non valide.";
            return Page();
        }

        var role = user.IsAdmin ? "admin" : "user";

        var claims = new List<Claim>
        {
            new(ClaimTypes.NameIdentifier, user.UserId.ToString()),
            new(ClaimTypes.Name,           user.Email),
            new(ClaimTypes.Role,           role),
            new("user_id",                 user.UserId.ToString()),
        };

        var identity = new ClaimsIdentity(claims, CookieAuthenticationDefaults.AuthenticationScheme);
        var principal = new ClaimsPrincipal(identity);

        await HttpContext.SignInAsync(CookieAuthenticationDefaults.AuthenticationScheme, principal);
        await userRepo.UpdateLastLoginAsync(user.UserId);

        return Redirect(role == "admin" ? "/" : "/my-loans");
    }
}
