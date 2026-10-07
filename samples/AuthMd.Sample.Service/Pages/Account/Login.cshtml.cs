using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;

namespace AuthMd.Sample.Service.Pages.Account;

/// <summary>
/// The app's own Identity login. The claim page sends people here first, like any protected page would.
/// </summary>
public class LoginModel(SignInManager<IdentityUser> signIn) : PageModel
{
    [BindProperty]
    public string Email { get; set; } = string.Empty;

    [BindProperty]
    public string Password { get; set; } = string.Empty;

    [BindProperty(SupportsGet = true)]
    public string? ReturnUrl { get; set; }

    public bool Failed { get; private set; }

    public async Task<IActionResult> OnPostAsync()
    {
        var result = await signIn.PasswordSignInAsync(Email, Password, isPersistent: false, lockoutOnFailure: true);
        if (!result.Succeeded)
        {
            Failed = true;
            return Page();
        }

        return LocalRedirect(Url.IsLocalUrl(ReturnUrl) ? ReturnUrl : "/");
    }
}
