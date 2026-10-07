using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc.RazorPages;

namespace AuthMd.Sample.Service.Pages;

/// <summary>
/// The claim page: where an agent's <c>verification_uri</c> lands. AuthMd.AspNetCore is headless, so
/// this page is the application's. All the server has to do is require a signed-in user; the page's
/// script talks to the two claim operations the package maps.
/// </summary>
[Authorize]
public class ClaimModel : PageModel
{
    public void OnGet()
    {
    }
}
