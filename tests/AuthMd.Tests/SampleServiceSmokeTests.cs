extern alias SampleService;

using System.Net;
using System.Text.RegularExpressions;
using Microsoft.AspNetCore.Mvc.Testing;

namespace AuthMd.Tests;

public class SampleServiceSmokeTests(WebApplicationFactory<SampleService::Program> factory)
    : IClassFixture<WebApplicationFactory<SampleService::Program>>
{
    [Fact]
    public async Task Sample_service_boots_and_answers_over_http()
    {
        using var client = factory.CreateClient();

        using var response = await client.GetAsync("/");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }

    [Fact]
    public async Task Sample_service_is_an_auth_md_service_with_a_claim_page_behind_its_login()
    {
        using var client = factory.CreateClient(new WebApplicationFactoryClientOptions { AllowAutoRedirect = false });

        var authMd = await client.GetStringAsync("/auth.md");
        using var claimPage = await client.GetAsync("/claim?claim_attempt_token=cat_example");
        using var api = await client.GetAsync("/api/notes");

        Assert.StartsWith("# auth.md", authMd);
        Assert.Equal(HttpStatusCode.Redirect, claimPage.StatusCode);
        Assert.StartsWith("/Account/Login", claimPage.Headers.Location!.PathAndQuery);
        Assert.Equal(HttpStatusCode.Unauthorized, api.StatusCode);
    }

    [Fact]
    public async Task Someone_without_an_account_can_sign_up_from_the_agents_link_and_reach_the_claim_page()
    {
        using var browser = factory.CreateClient();

        // The agent's link sends a signed-out visitor to the login page, which offers sign-up.
        using var loginPage = await browser.GetAsync("/claim?claim_attempt_token=cat_example");
        var signUpLink = WebUtility.HtmlDecode(
            Regex.Match(await loginPage.Content.ReadAsStringAsync(), "href=\"(/Account/Register[^\"]*)\"").Groups[1].Value);

        using var signUpPage = await browser.GetAsync(signUpLink);
        var antiforgery = WebUtility.HtmlDecode(Regex.Match(
            await signUpPage.Content.ReadAsStringAsync(),
            "name=\"__RequestVerificationToken\"[^>]*value=\"([^\"]+)\"").Groups[1].Value);
        using var landed = await browser.PostAsync(signUpLink, new FormUrlEncodedContent(new Dictionary<string, string>
        {
            ["Email"] = "newcomer@example.com",
            ["Password"] = "Sup3r-secret",
            ["__RequestVerificationToken"] = antiforgery,
        }));

        Assert.Equal(HttpStatusCode.OK, signUpPage.StatusCode);
        Assert.Equal(HttpStatusCode.OK, landed.StatusCode);
        Assert.Equal("/claim", landed.RequestMessage!.RequestUri!.AbsolutePath);
        Assert.Equal("?claim_attempt_token=cat_example", landed.RequestMessage.RequestUri.Query);
        var claimPage = await landed.Content.ReadAsStringAsync();
        Assert.Contains("Approve an agent", claimPage);
        Assert.Contains("newcomer@example.com", claimPage);
    }
}
