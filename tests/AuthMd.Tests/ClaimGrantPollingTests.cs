using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Time.Testing;

namespace AuthMd.Tests;

public class ClaimGrantPollingTests(WebApplicationFactory<Program> factory)
    : IClassFixture<WebApplicationFactory<Program>>
{
    [Fact]
    public async Task Polling_before_the_user_confirms_reports_authorization_pending()
    {
        using var agent = factory.CreateClient();
        var claim = await agent.RegisterAsync("ada@example.com");

        using var poll = await agent.PollAsync(claim.ClaimToken);

        Assert.Equal(HttpStatusCode.BadRequest, poll.StatusCode);
        var error = await poll.Content.ReadFromJsonAsync<JsonElement>();
        Assert.Equal("authorization_pending", error.String("error"));
    }

    [Fact]
    public async Task Polling_after_the_user_confirms_yields_an_access_token_and_identity_assertion()
    {
        using var agent = factory.CreateClient();
        using var browser = factory.CreateClient();
        var claim = await agent.RegisterAsync("grace@example.com");
        var userId = await browser.SignInAsync("grace@example.com");
        using var confirmation = await browser.ConfirmClaimAsync(claim.ClaimAttemptToken, claim.UserCode);
        var polledAt = DateTimeOffset.UtcNow;

        using var poll = await agent.PollAsync(claim.ClaimToken);

        Assert.Equal(HttpStatusCode.OK, poll.StatusCode);
        var tokens = await poll.Content.ReadFromJsonAsync<JsonElement>();
        Assert.Equal("Bearer", tokens.String("token_type"));
        Assert.InRange(tokens.GetProperty("expires_in").GetInt32(), 3590, 3600);
        Assert.Equal("api.read api.write", tokens.String("scope"));
        Assert.False(tokens.TryGetProperty("refresh_token", out _));

        var accessToken = CeremonySteps.JwtPayload(tokens.String("access_token")!);
        Assert.Equal(userId, accessToken.String("sub"));
        Assert.Equal("http://localhost/", accessToken.String("iss"));

        var assertion = CeremonySteps.JwtPayload(tokens.String("identity_assertion")!);
        Assert.Equal(userId, assertion.String("sub"));
        Assert.Equal("http://localhost/", assertion.String("iss"));
        Assert.Equal(claim.RegistrationId, assertion.String("registration_id"));
        Assert.Equal("api.read api.write", assertion.String("scope"));

        var assertionExpires = tokens.GetProperty("assertion_expires").GetDateTimeOffset();
        Assert.InRange(assertionExpires, polledAt.AddDays(7).AddMinutes(-1), polledAt.AddDays(7).AddMinutes(1));
        Assert.Equal(assertionExpires.ToUnixTimeSeconds(), assertion.GetProperty("exp").GetInt64());
    }

    [Fact]
    public async Task Polling_faster_than_the_interval_is_told_to_slow_down()
    {
        var clock = new FakeTimeProvider(DateTimeOffset.UtcNow);
        using var service = factory.WithWebHostBuilder(host =>
            host.ConfigureTestServices(services => services.AddSingleton<TimeProvider>(clock)));
        using var agent = service.CreateClient();
        var claim = await agent.RegisterAsync("linus@example.com");

        var first = await agent.PollAsync(claim.ClaimToken).JsonAsync();
        clock.Advance(TimeSpan.FromSeconds(2));
        var tooSoon = await agent.PollAsync(claim.ClaimToken).JsonAsync();
        clock.Advance(TimeSpan.FromSeconds(5));
        var patient = await agent.PollAsync(claim.ClaimToken).JsonAsync();

        Assert.Equal("authorization_pending", first.String("error"));
        Assert.Equal("slow_down", tooSoon.String("error"));
        Assert.Equal("authorization_pending", patient.String("error"));
    }

    [Fact]
    public async Task Polling_after_the_ceremony_expired_unclaimed_reports_expired_token()
    {
        var clock = new FakeTimeProvider(DateTimeOffset.UtcNow);
        using var service = factory.WithWebHostBuilder(host =>
            host.ConfigureTestServices(services => services.AddSingleton<TimeProvider>(clock)));
        using var agent = service.CreateClient();
        var claim = await agent.RegisterAsync("alan@example.com");

        clock.Advance(TimeSpan.FromMinutes(11));
        using var poll = await agent.PollAsync(claim.ClaimToken);

        Assert.Equal(HttpStatusCode.BadRequest, poll.StatusCode);
        var error = await poll.Content.ReadFromJsonAsync<JsonElement>();
        Assert.Equal("expired_token", error.String("error"));
    }

    [Fact]
    public async Task Polling_with_an_unknown_claim_token_is_rejected_as_invalid_grant()
    {
        using var agent = factory.CreateClient();

        using var poll = await agent.PollAsync("clm_made-up");

        Assert.Equal(HttpStatusCode.BadRequest, poll.StatusCode);
        var error = await poll.Content.ReadFromJsonAsync<JsonElement>();
        Assert.Equal("invalid_grant", error.String("error"));
    }

    [Fact]
    public async Task A_claim_token_yields_tokens_only_once()
    {
        var clock = new FakeTimeProvider(DateTimeOffset.UtcNow);
        using var service = factory.WithWebHostBuilder(host =>
            host.ConfigureTestServices(services => services.AddSingleton<TimeProvider>(clock)));
        using var agent = service.CreateClient();
        using var browser = service.CreateClient();
        var claim = await agent.RegisterAsync("barbara@example.com");
        await browser.SignInAsync("barbara@example.com");
        using var confirmation = await browser.ConfirmClaimAsync(claim.ClaimAttemptToken, claim.UserCode);
        using var first = await agent.PollAsync(claim.ClaimToken);

        clock.Advance(TimeSpan.FromSeconds(6));
        using var replay = await agent.PollAsync(claim.ClaimToken);

        Assert.Equal(HttpStatusCode.OK, first.StatusCode);
        Assert.Equal(HttpStatusCode.BadRequest, replay.StatusCode);
        var error = await replay.Content.ReadFromJsonAsync<JsonElement>();
        Assert.Equal("invalid_grant", error.String("error"));
    }

    [Fact]
    public async Task Polling_without_a_claim_token_is_rejected_as_invalid_request()
    {
        using var agent = factory.CreateClient();

        using var poll = await agent.PostAsync("/oauth2/token", new FormUrlEncodedContent(new Dictionary<string, string>
        {
            ["grant_type"] = "urn:workos:agent-auth:grant-type:claim",
        }));

        Assert.Equal(HttpStatusCode.BadRequest, poll.StatusCode);
        var error = await poll.Content.ReadFromJsonAsync<JsonElement>();
        Assert.Equal("invalid_request", error.String("error"));
    }

    [Fact]
    public async Task An_unsupported_grant_type_is_rejected()
    {
        using var agent = factory.CreateClient();

        using var response = await agent.PostAsync("/oauth2/token", new FormUrlEncodedContent(new Dictionary<string, string>
        {
            ["grant_type"] = "password",
            ["username"] = "ada@example.com",
            ["password"] = "hunter2",
        }));

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        var error = await response.Content.ReadFromJsonAsync<JsonElement>();
        Assert.Equal("unsupported_grant_type", error.String("error"));
    }
}
