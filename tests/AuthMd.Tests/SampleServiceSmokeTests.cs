extern alias SampleService;

using System.Net;
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
}
