using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.Options;

namespace AuthMd.AspNetCore;

/// <summary>
/// Maps the auth.md endpoints.
/// </summary>
public static class AuthMdEndpointRouteBuilderExtensions
{
    /// <summary>
    /// Maps the auth.md discovery documents and agent endpoints.
    /// Requires <see cref="AuthMdServiceCollectionExtensions.AddAuthMd{TUser}"/>.
    /// </summary>
    /// <remarks>
    /// The authorization-server metadata at <c>/.well-known/oauth-authorization-server</c> is served by
    /// OpenIddict, with the auth.md fields attached.
    /// </remarks>
    public static IEndpointRouteBuilder MapAuthMd(this IEndpointRouteBuilder endpoints)
    {
        ArgumentNullException.ThrowIfNull(endpoints);

        endpoints.MapGet("/auth.md",
            (HttpContext context, IOptions<AuthMdOptions> options) =>
                Results.Text(
                    AuthMdDocument.Render(ServiceUrls.For(context, options.Value), options.Value),
                    "text/markdown; charset=utf-8"));

        endpoints.MapGet("/.well-known/oauth-protected-resource",
            (HttpContext context, IOptions<AuthMdOptions> options) =>
                Results.Json(DiscoveryDocuments.ProtectedResourceMetadata(ServiceUrls.For(context, options.Value), options.Value)));

        endpoints.MapPost("/agent/identity", IdentityEndpoint.Handle);
        endpoints.MapGet("/agent/identity/claim/view", ClaimEndpoints.View);
        endpoints.MapPost("/agent/identity/claim/confirm", ClaimEndpoints.Confirm);
        endpoints.MapPost("/oauth2/token", TokenEndpoint.Handle);

        return endpoints;
    }
}
