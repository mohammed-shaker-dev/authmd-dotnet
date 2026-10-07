using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.AspNetCore.Mvc.Testing;

namespace AuthMd.Tests;

public class RevocationTests(WebApplicationFactory<Program> factory)
    : IClassFixture<WebApplicationFactory<Program>>
{
    private static Task<HttpResponseMessage> RevokeTokenAsync(HttpClient agent, string accessToken) =>
        agent.PostAsync("/oauth2/revoke", new FormUrlEncodedContent(new Dictionary<string, string>
        {
            ["token"] = accessToken,
            ["token_type_hint"] = "access_token",
        }));

    [Fact]
    public async Task Revoking_an_access_token_kills_that_token_but_not_the_registration()
    {
        using var agent = factory.CreateClient();
        using var browser = factory.CreateClient();
        var held = await agent.CompleteCeremonyAsync(browser, "ada@example.com");
        var accessToken = held.String("access_token")!;
        using var before = await agent.CallApiAsync(HttpMethod.Get, "/api/notes", accessToken);

        using var revocation = await RevokeTokenAsync(agent, accessToken);
        using var repeated = await RevokeTokenAsync(agent, accessToken);
        using var after = await agent.CallApiAsync(HttpMethod.Get, "/api/notes", accessToken);
        var fresh = await agent.ExchangeAsync(held.String("identity_assertion")).JsonAsync();
        using var withFresh = await agent.CallApiAsync(HttpMethod.Get, "/api/notes", fresh.String("access_token"));

        Assert.Equal(HttpStatusCode.OK, before.StatusCode);
        Assert.Equal(HttpStatusCode.OK, revocation.StatusCode);
        Assert.Equal(HttpStatusCode.OK, repeated.StatusCode);
        Assert.Equal(HttpStatusCode.Unauthorized, after.StatusCode);
        Assert.Equal(HttpStatusCode.OK, withFresh.StatusCode);
    }

    [Fact]
    public async Task Revoking_the_agent_registration_kills_its_tokens_and_its_identity_assertion()
    {
        using var agent = factory.CreateClient();
        using var browser = factory.CreateClient();
        var held = await agent.CompleteCeremonyAsync(browser, "grace@example.com");
        var registrationId = CeremonySteps.JwtPayload(held.String("identity_assertion")!).String("registration_id");
        var second = await agent.ExchangeAsync(held.String("identity_assertion")).JsonAsync();

        using var revocation = await browser.PostAsync($"/test/registrations/{registrationId}/revoke", content: null);
        using var withFirst = await agent.CallApiAsync(HttpMethod.Get, "/api/notes", held.String("access_token"));
        using var withSecond = await agent.CallApiAsync(HttpMethod.Get, "/api/notes", second.String("access_token"));
        using var exchange = await agent.ExchangeAsync(held.String("identity_assertion"));

        Assert.Equal(HttpStatusCode.NoContent, revocation.StatusCode);
        Assert.Equal(HttpStatusCode.Unauthorized, withFirst.StatusCode);
        Assert.Equal(HttpStatusCode.Unauthorized, withSecond.StatusCode);
        Assert.Equal(HttpStatusCode.BadRequest, exchange.StatusCode);
        var error = await exchange.Content.ReadFromJsonAsync<JsonElement>();
        Assert.Equal("invalid_grant", error.String("error"));
    }
}
