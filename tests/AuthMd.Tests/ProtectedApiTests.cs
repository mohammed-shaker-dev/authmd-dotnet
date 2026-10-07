using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.AspNetCore.Mvc.Testing;

namespace AuthMd.Tests;

public class ProtectedApiTests(WebApplicationFactory<Program> factory)
    : IClassFixture<WebApplicationFactory<Program>>
{
    [Fact]
    public async Task Access_token_with_the_required_scope_reaches_the_api_as_the_user()
    {
        using var agent = factory.CreateClient();
        using var browser = factory.CreateClient();
        var userId = await browser.SignInAsync("ada@example.com");
        var held = await agent.CompleteCeremonyAsync(browser, "ada@example.com");

        using var response = await agent.CallApiAsync(HttpMethod.Get, "/api/notes", held.String("access_token"));

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var notes = await response.Content.ReadFromJsonAsync<JsonElement>();
        Assert.Equal(userId, notes.String("owner"));
    }

    [Fact]
    public async Task Call_without_a_token_is_challenged_and_pointed_at_the_resource_metadata()
    {
        using var agent = factory.CreateClient();

        using var response = await agent.CallApiAsync(HttpMethod.Get, "/api/notes", accessToken: null);

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
        var challenge = Assert.Single(response.Headers.WwwAuthenticate);
        Assert.Equal("Bearer", challenge.Scheme);
        Assert.Contains(
            "resource_metadata=\"http://localhost/.well-known/oauth-protected-resource\"",
            challenge.Parameter);
    }

    [Fact]
    public async Task Token_without_the_required_scope_is_forbidden_as_insufficient_scope()
    {
        using var agent = factory.CreateClient();
        using var browser = factory.CreateClient();
        var held = await agent.CompleteCeremonyAsync(browser, "grace@example.com");
        var readOnly = await agent.ExchangeAsync(held.String("identity_assertion"), scope: "api.read").JsonAsync();

        using var read = await agent.CallApiAsync(HttpMethod.Get, "/api/notes", readOnly.String("access_token"));
        using var write = await agent.CallApiAsync(HttpMethod.Post, "/api/notes", readOnly.String("access_token"));

        Assert.Equal(HttpStatusCode.OK, read.StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, write.StatusCode);
        var challenge = Assert.Single(write.Headers.WwwAuthenticate);
        Assert.Contains("error=\"insufficient_scope\"", challenge.Parameter);
    }
}
