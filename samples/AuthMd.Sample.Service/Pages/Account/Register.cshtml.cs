using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;

namespace AuthMd.Sample.Service.Pages.Account;

/// <summary>
/// The app's own sign-up. Someone an agent registered for, who has no account yet, creates one here
/// and is sent back to the claim page. A real app would also confirm the email address.
/// </summary>
public class RegisterModel(UserManager<IdentityUser> users, SignInManager<IdentityUser> signIn) : PageModel
{
    [BindProperty]
    public string Email { get; set; } = string.Empty;

    [BindProperty]
    public string Password { get; set; } = string.Empty;

    [BindProperty(SupportsGet = true)]
    public string? ReturnUrl { get; set; }

    public IReadOnlyList<string> Problems { get; private set; } = [];

    public async Task<IActionResult> OnPostAsync()
    {
        var user = new IdentityUser { UserName = Email, Email = Email };
        var result = await users.CreateAsync(user, Password);
        if (!result.Succeeded)
        {
            Problems = [.. result.Errors.Select(error => error.Description)];
            return Page();
        }

        await signIn.SignInAsync(user, isPersistent: false);
        return LocalRedirect(Url.IsLocalUrl(ReturnUrl) ? ReturnUrl : "/");
    }
}
