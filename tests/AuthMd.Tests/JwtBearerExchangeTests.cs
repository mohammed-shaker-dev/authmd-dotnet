using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Time.Testing;

namespace AuthMd.Tests;

public class JwtBearerExchangeTests(WebApplicationFactory<Program> factory)
    : IClassFixture<WebApplicationFactory<Program>>
{
    [Fact]
    public async Task Identity_assertion_is_exchanged_for_a_fresh_access_token_again_and_again()
    {
        using var agent = factory.CreateClient();
        using var browser = factory.CreateClient();
        var userId = await browser.SignInAsync("ada@example.com");
        var held = await agent.CompleteCeremonyAsync(browser, "ada@example.com");
        var assertion = held.String("identity_assertion")!;

        using var first = await agent.ExchangeAsync(assertion);
        using var second = await agent.ExchangeAsync(assertion);

        Assert.Equal(HttpStatusCode.OK, first.StatusCode);
        var tokens = await first.Content.ReadFromJsonAsync<JsonElement>();
        Assert.Equal("Bearer", tokens.String("token_type"));
        Assert.InRange(tokens.GetProperty("expires_in").GetInt32(), 3590, 3600);
        Assert.Equal("api.read api.write", tokens.String("scope"));
        Assert.False(tokens.TryGetProperty("refresh_token", out _));

        var accessToken = CeremonySteps.JwtPayload(tokens.String("access_token")!);
        Assert.Equal(userId, accessToken.String("sub"));
        Assert.Equal(CeremonySteps.JwtPayload(assertion).String("registration_id"), accessToken.String("registration_id"));

        Assert.Equal(HttpStatusCode.OK, second.StatusCode);
        var again = await second.Content.ReadFromJsonAsync<JsonElement>();
        Assert.NotEqual(tokens.String("access_token"), again.String("access_token"));
        Assert.NotEqual(held.String("access_token"), tokens.String("access_token"));
    }

    [Fact]
    public async Task Expired_identity_assertion_is_rejected_as_invalid_grant()
    {
        var clock = new FakeTimeProvider(DateTimeOffset.UtcNow);
        using var service = factory.WithWebHostBuilder(host =>
            host.ConfigureTestServices(services => services.AddSingleton<TimeProvider>(clock)));
        using var agent = service.CreateClient();
        using var browser = service.CreateClient();
        var held = await agent.CompleteCeremonyAsync(browser, "grace@example.com");

        clock.Advance(TimeSpan.FromDays(6));
        using var stillValid = await agent.ExchangeAsync(held.String("identity_assertion"));
        clock.Advance(TimeSpan.FromDays(2));
        using var stale = await agent.ExchangeAsync(held.String("identity_assertion"));

        Assert.Equal(HttpStatusCode.OK, stillValid.StatusCode);
        Assert.Equal(HttpStatusCode.BadRequest, stale.StatusCode);
        var error = await stale.Content.ReadFromJsonAsync<JsonElement>();
        Assert.Equal("invalid_grant", error.String("error"));
    }

    [Fact]
    public async Task An_access_token_cannot_be_passed_off_as_an_identity_assertion()
    {
        using var agent = factory.CreateClient();
        using var browser = factory.CreateClient();
        var held = await agent.CompleteCeremonyAsync(browser, "linus@example.com");

        using var response = await agent.ExchangeAsync(held.String("access_token"));

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        var error = await response.Content.ReadFromJsonAsync<JsonElement>();
        Assert.Equal("invalid_grant", error.String("error"));
    }

    [Fact]
    public async Task Access_token_is_pinned_to_the_service_resource()
    {
        using var agent = factory.CreateClient();
        using var browser = factory.CreateClient();
        var held = await agent.CompleteCeremonyAsync(browser, "alan@example.com");

        using var pinned = await agent.ExchangeAsync(held.String("identity_assertion"), resource: "http://localhost/");
        using var elsewhere = await agent.ExchangeAsync(held.String("identity_assertion"), resource: "https://api.other.example/");

        Assert.Equal(HttpStatusCode.OK, pinned.StatusCode);
        var tokens = await pinned.Content.ReadFromJsonAsync<JsonElement>();
        Assert.Equal("http://localhost/", CeremonySteps.JwtPayload(tokens.String("access_token")!).String("aud"));

        Assert.Equal(HttpStatusCode.BadRequest, elsewhere.StatusCode);
        var error = await elsewhere.Content.ReadFromJsonAsync<JsonElement>();
        Assert.Equal("invalid_target", error.String("error"));
    }

    [Fact]
    public async Task Exchange_without_an_assertion_is_rejected_as_invalid_request()
    {
        using var agent = factory.CreateClient();

        using var response = await agent.ExchangeAsync(identityAssertion: null);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        var error = await response.Content.ReadFromJsonAsync<JsonElement>();
        Assert.Equal("invalid_request", error.String("error"));
    }

    [Fact]
    public async Task Forged_or_tampered_assertion_is_rejected_as_invalid_grant()
    {
        using var agent = factory.CreateClient();
        using var browser = factory.CreateClient();
        var held = await agent.CompleteCeremonyAsync(browser, "barbara@example.com");
        var genuine = held.String("identity_assertion")!;
        var signatureStart = genuine.LastIndexOf('.') + 1;
        var tampered = genuine[..signatureStart] + (genuine[signatureStart] == 'A' ? 'B' : 'A') + genuine[(signatureStart + 1)..];

        using var forged = await agent.ExchangeAsync("eyJhbGciOiJub25lIn0.eyJzdWIiOiJtYWxsb3J5In0.");
        using var broken = await agent.ExchangeAsync(tampered);

        Assert.Equal(HttpStatusCode.BadRequest, forged.StatusCode);
        Assert.Equal("invalid_grant", (await forged.Content.ReadFromJsonAsync<JsonElement>()).String("error"));
        Assert.Equal(HttpStatusCode.BadRequest, broken.StatusCode);
        Assert.Equal("invalid_grant", (await broken.Content.ReadFromJsonAsync<JsonElement>()).String("error"));
    }

    [Fact]
    public async Task Agent_can_ask_for_fewer_scopes_than_its_registration_carries()
    {
        using var agent = factory.CreateClient();
        using var browser = factory.CreateClient();
        var held = await agent.CompleteCeremonyAsync(browser, "margaret@example.com");

        using var narrowed = await agent.ExchangeAsync(held.String("identity_assertion"), scope: "api.read");
        using var unknown = await agent.ExchangeAsync(held.String("identity_assertion"), scope: "api.read admin");

        Assert.Equal(HttpStatusCode.OK, narrowed.StatusCode);
        var tokens = await narrowed.Content.ReadFromJsonAsync<JsonElement>();
        Assert.Equal("api.read", CeremonySteps.JwtPayload(tokens.String("access_token")!).String("scope"));

        Assert.Equal(HttpStatusCode.BadRequest, unknown.StatusCode);
        Assert.Equal("invalid_scope", (await unknown.Content.ReadFromJsonAsync<JsonElement>()).String("error"));
    }
}
