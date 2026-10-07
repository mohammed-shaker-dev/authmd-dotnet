using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.AspNetCore.Mvc.Testing;

namespace AuthMd.Tests;

public class DiscoveryTests(WebApplicationFactory<Program> factory)
    : IClassFixture<WebApplicationFactory<Program>>
{
    [Fact]
    public async Task Protected_resource_metadata_describes_the_service_and_its_scopes()
    {
        using var client = factory.CreateClient();

        var prm = await client.GetFromJsonAsync<JsonElement>("/.well-known/oauth-protected-resource");

        Assert.Equal("http://localhost/", prm.String("resource"));
        Assert.Equal("Test Service", prm.String("resource_name"));
        Assert.Equal(["http://localhost/"], prm.Strings("authorization_servers"));
        Assert.Equal(["api.read", "api.write"], prm.Strings("scopes_supported"));
        Assert.Equal(["header"], prm.Strings("bearer_methods_supported"));
    }

    [Fact]
    public async Task Authorization_server_metadata_locates_the_oauth_and_agent_endpoints()
    {
        using var client = factory.CreateClient();

        var metadata = await client.GetFromJsonAsync<JsonElement>("/.well-known/oauth-authorization-server");

        Assert.Equal("http://localhost/", metadata.String("issuer"));
        Assert.Equal("http://localhost/oauth2/token", metadata.String("token_endpoint"));
        Assert.Equal("http://localhost/oauth2/revoke", metadata.String("revocation_endpoint"));
        Assert.Equal(
            ["urn:ietf:params:oauth:grant-type:jwt-bearer", "urn:workos:agent-auth:grant-type:claim"],
            metadata.Strings("grant_types_supported"));
        Assert.Equal("http://localhost/", metadata.String("resource"));
        Assert.Equal(["api.read", "api.write"], metadata.Strings("scopes_supported"));

        var agentAuth = metadata.GetProperty("agent_auth");
        Assert.Equal("http://localhost/auth.md", agentAuth.String("skill"));
        Assert.Equal("http://localhost/agent/identity", agentAuth.String("identity_endpoint"));
        Assert.Equal("http://localhost/agent/identity/claim", agentAuth.String("claim_endpoint"));
        Assert.Equal(["service_auth"], agentAuth.Strings("identity_types_supported"));
    }

    [Fact]
    public async Task Auth_md_tells_an_agent_how_to_register_with_this_service()
    {
        using var client = factory.CreateClient();

        using var response = await client.GetAsync("/auth.md");
        var document = await response.Content.ReadAsStringAsync();

        Assert.Equal("text/markdown", response.Content.Headers.ContentType?.MediaType);
        Assert.StartsWith("# auth.md", document);
        Assert.Contains("http://localhost/.well-known/oauth-protected-resource", document);
        Assert.Contains("POST http://localhost/agent/identity", document);
        Assert.Contains("\"type\": \"service_auth\"", document);
        Assert.Contains("POST http://localhost/oauth2/token", document);
        Assert.Contains("grant_type=urn:workos:agent-auth:grant-type:claim", document);
        Assert.Contains("grant_type=urn:ietf:params:oauth:grant-type:jwt-bearer", document);
    }
}
