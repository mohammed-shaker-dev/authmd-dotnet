using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Time.Testing;

namespace AuthMd.Tests;

public class ClaimConfirmationTests(WebApplicationFactory<Program> factory)
    : IClassFixture<WebApplicationFactory<Program>>
{
    [Fact]
    public async Task Signed_in_user_confirming_the_user_code_claims_the_ceremony()
    {
        using var agent = factory.CreateClient();
        using var browser = factory.CreateClient();
        var claim = await agent.RegisterAsync("ada@example.com");
        await browser.SignInAsync("ada@example.com");

        var before = await browser.ViewClaimAsync(claim.ClaimAttemptToken).JsonAsync();
        using var confirmation = await browser.ConfirmClaimAsync(claim.ClaimAttemptToken, claim.UserCode);
        var after = await browser.ViewClaimAsync(claim.ClaimAttemptToken).JsonAsync();

        Assert.Equal("pending", before.String("status"));
        Assert.Equal(["api.read", "api.write"], before.Strings("scopes"));

        Assert.Equal(HttpStatusCode.OK, confirmation.StatusCode);
        var confirmed = await confirmation.Content.ReadFromJsonAsync<JsonElement>();
        Assert.Equal(claim.RegistrationId, confirmed.String("registration_id"));
        Assert.Equal("claimed", confirmed.String("status"));
        Assert.Equal(["api.read", "api.write"], confirmed.Strings("scopes"));

        Assert.Equal("claimed", after.String("status"));
    }

    [Fact]
    public async Task Wrong_user_code_is_rejected_and_the_ceremony_stays_pending()
    {
        using var agent = factory.CreateClient();
        using var browser = factory.CreateClient();
        var claim = await agent.RegisterAsync("grace@example.com");
        await browser.SignInAsync("grace@example.com");
        var wrongCode = claim.UserCode == "000000" ? "000001" : "000000";

        using var confirmation = await browser.ConfirmClaimAsync(claim.ClaimAttemptToken, wrongCode);
        var after = await browser.ViewClaimAsync(claim.ClaimAttemptToken).JsonAsync();

        Assert.Equal(HttpStatusCode.BadRequest, confirmation.StatusCode);
        var error = await confirmation.Content.ReadFromJsonAsync<JsonElement>();
        Assert.Equal("invalid_request", error.String("error"));
        Assert.Equal("pending", after.String("status"));
    }

    [Fact]
    public async Task Confirming_after_the_ceremony_expired_is_rejected_as_claim_expired()
    {
        var clock = new FakeTimeProvider(DateTimeOffset.UtcNow);
        using var service = factory.WithWebHostBuilder(host =>
            host.ConfigureTestServices(services => services.AddSingleton<TimeProvider>(clock)));
        using var agent = service.CreateClient();
        using var browser = service.CreateClient();
        var claim = await agent.RegisterAsync("linus@example.com");
        await browser.SignInAsync("linus@example.com");

        clock.Advance(TimeSpan.FromMinutes(11));
        using var confirmation = await browser.ConfirmClaimAsync(claim.ClaimAttemptToken, claim.UserCode);
        var after = await browser.ViewClaimAsync(claim.ClaimAttemptToken).JsonAsync();

        Assert.Equal(HttpStatusCode.Gone, confirmation.StatusCode);
        var error = await confirmation.Content.ReadFromJsonAsync<JsonElement>();
        Assert.Equal("claim_expired", error.String("error"));
        Assert.Equal("expired", after.String("status"));
    }

    [Fact]
    public async Task Unknown_claim_attempt_token_is_rejected_as_invalid_claim_token()
    {
        using var browser = factory.CreateClient();
        await browser.SignInAsync("alan@example.com");

        using var view = await browser.ViewClaimAsync("cat_made-up");
        using var confirmation = await browser.ConfirmClaimAsync("cat_made-up", "123456");

        Assert.Equal(HttpStatusCode.BadRequest, view.StatusCode);
        Assert.Equal("invalid_claim_token", (await view.Content.ReadFromJsonAsync<JsonElement>()).String("error"));
        Assert.Equal(HttpStatusCode.BadRequest, confirmation.StatusCode);
        Assert.Equal("invalid_claim_token", (await confirmation.Content.ReadFromJsonAsync<JsonElement>()).String("error"));
    }

    [Fact]
    public async Task Nobody_signed_in_is_rejected_as_login_required()
    {
        using var agent = factory.CreateClient();
        using var browser = factory.CreateClient();
        var claim = await agent.RegisterAsync("barbara@example.com");

        using var view = await browser.ViewClaimAsync(claim.ClaimAttemptToken);
        using var confirmation = await browser.ConfirmClaimAsync(claim.ClaimAttemptToken, claim.UserCode);

        Assert.Equal(HttpStatusCode.Unauthorized, view.StatusCode);
        Assert.Equal("login_required", (await view.Content.ReadFromJsonAsync<JsonElement>()).String("error"));
        Assert.Equal(HttpStatusCode.Unauthorized, confirmation.StatusCode);
        Assert.Equal("login_required", (await confirmation.Content.ReadFromJsonAsync<JsonElement>()).String("error"));
    }

    [Fact]
    public async Task Only_the_user_the_agent_named_can_claim_the_ceremony()
    {
        using var agent = factory.CreateClient();
        using var intruder = factory.CreateClient();
        using var owner = factory.CreateClient();
        var claim = await agent.RegisterAsync("margaret@example.com");
        await intruder.SignInAsync("mallory@example.com");
        await owner.SignInAsync("margaret@example.com");

        using var view = await intruder.ViewClaimAsync(claim.ClaimAttemptToken);
        using var confirmation = await intruder.ConfirmClaimAsync(claim.ClaimAttemptToken, claim.UserCode);
        var after = await owner.ViewClaimAsync(claim.ClaimAttemptToken).JsonAsync();

        Assert.Equal(HttpStatusCode.Forbidden, view.StatusCode);
        Assert.Equal("access_denied", (await view.Content.ReadFromJsonAsync<JsonElement>()).String("error"));
        Assert.Equal(HttpStatusCode.Forbidden, confirmation.StatusCode);
        Assert.Equal("access_denied", (await confirmation.Content.ReadFromJsonAsync<JsonElement>()).String("error"));
        Assert.Equal("pending", after.String("status"));
    }

    [Fact]
    public async Task Confirming_an_already_claimed_ceremony_is_rejected_as_claimed_or_in_flight()
    {
        using var agent = factory.CreateClient();
        using var browser = factory.CreateClient();
        var claim = await agent.RegisterAsync("katherine@example.com");
        await browser.SignInAsync("katherine@example.com");
        using var first = await browser.ConfirmClaimAsync(claim.ClaimAttemptToken, claim.UserCode);

        using var second = await browser.ConfirmClaimAsync(claim.ClaimAttemptToken, claim.UserCode);

        Assert.Equal(HttpStatusCode.OK, first.StatusCode);
        Assert.Equal(HttpStatusCode.Conflict, second.StatusCode);
        Assert.Equal("claimed_or_in_flight", (await second.Content.ReadFromJsonAsync<JsonElement>()).String("error"));
    }
}
