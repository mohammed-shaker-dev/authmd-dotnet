using AuthMd.Client;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.DependencyInjection;

namespace AuthMd.Tests;

public class ClientDiscoveryTests(WebApplicationFactory<Program> factory)
    : IClassFixture<WebApplicationFactory<Program>>
{
    [Fact]
    public async Task Client_discovers_a_service_from_its_url()
    {
        var client = new AuthMdClient(factory.CreateClient());

        var service = await client.DiscoverAsync(new Uri("http://localhost/"));

        Assert.Equal(new Uri("http://localhost/"), service.Resource);
        Assert.Equal("Test Service", service.ResourceName);
        Assert.Equal(["api.read", "api.write"], service.ScopesSupported);
        Assert.Equal(new Uri("http://localhost/"), service.Issuer);
        Assert.Equal(new Uri("http://localhost/oauth2/token"), service.TokenEndpoint);
        Assert.Equal(new Uri("http://localhost/oauth2/revoke"), service.RevocationEndpoint);
        Assert.Equal(new Uri("http://localhost/agent/identity"), service.IdentityEndpoint);
        Assert.Equal(["service_auth"], service.IdentityTypesSupported);
        Assert.True(service.SupportsServiceAuth);
        Assert.StartsWith("# auth.md", service.Instructions);
    }

    [Fact]
    public async Task Client_discovers_a_service_from_a_protected_api_url_that_turned_it_away()
    {
        var client = new AuthMdClient(factory.CreateClient());

        var service = await client.DiscoverAsync(new Uri("http://localhost/api/notes"));

        Assert.Equal(new Uri("http://localhost/"), service.Resource);
        Assert.Equal(new Uri("http://localhost/agent/identity"), service.IdentityEndpoint);
        Assert.True(service.SupportsServiceAuth);
    }

    [Fact]
    public async Task Discovery_of_a_host_that_publishes_no_metadata_fails_with_a_discovery_error()
    {
        using var notAService = factory.WithWebHostBuilder(host =>
            host.ConfigureTestServices(services => services.AddSingleton<IStartupFilter>(new HideWellKnownDocuments())));
        var client = new AuthMdClient(notAService.CreateClient());

        var failure = await Assert.ThrowsAsync<AuthMdDiscoveryException>(
            () => client.DiscoverAsync(new Uri("http://localhost/")));

        Assert.Contains("http://localhost/.well-known/oauth-protected-resource", failure.Message);
    }

    /// <summary>Turns the test host into one that serves no discovery documents.</summary>
    private sealed class HideWellKnownDocuments : IStartupFilter
    {
        public Action<IApplicationBuilder> Configure(Action<IApplicationBuilder> next) => app =>
        {
            app.Use(async (context, proceed) =>
            {
                if (context.Request.Path.StartsWithSegments("/.well-known"))
                {
                    context.Response.StatusCode = StatusCodes.Status404NotFound;
                    return;
                }

                await proceed(context);
            });
            next(app);
        };
    }
}
