using System.Security.Claims;
using System.Security.Cryptography;
using System.Text;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.Extensions.Options;
using ShopWeb.Services;

namespace ShopWeb.Pages.Account;

/// <summary>
/// Passphrase sign-in for the admin sub-site. The passphrase comes from configuration
/// (<c>Admin:Passphrase</c>, a Key Vault reference in production); when none is configured
/// nobody can sign in. Swap for real identity (Entra ID) in production.
/// </summary>
public sealed class AdminLoginModel(IOptions<AdminAuthOptions> options) : PageModel
{
    public string? Error { get; private set; }

    [BindProperty(SupportsGet = true)]
    public string? ReturnUrl { get; set; }

    public void OnGet()
    {
    }

    public async Task<IActionResult> OnPostAsync(string passphrase)
    {
        var configured = options.Value.Passphrase;
        if (string.IsNullOrEmpty(configured))
        {
            Error = "Admin sign-in is not configured (Admin:Passphrase).";
            return Page();
        }

        if (string.IsNullOrEmpty(passphrase)
            || !CryptographicOperations.FixedTimeEquals(Encoding.UTF8.GetBytes(passphrase), Encoding.UTF8.GetBytes(configured)))
        {
            Error = "Incorrect passphrase.";
            return Page();
        }

        var identity = new ClaimsIdentity(
            [new Claim(ClaimTypes.Name, "admin"), new Claim(ClaimTypes.Role, "Admin")],
            CookieAuthenticationDefaults.AuthenticationScheme);
        await HttpContext.SignInAsync(CookieAuthenticationDefaults.AuthenticationScheme, new ClaimsPrincipal(identity));

        return LocalRedirect(Url.IsLocalUrl(ReturnUrl) ? ReturnUrl! : "/Admin");
    }
}
