using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Options;
using OpenIddict.Abstractions;
using OpenIddict.Server;

namespace AuthMd.AspNetCore;

/// <summary>
/// Registers the auth.md services.
/// </summary>
public static class AuthMdServiceCollectionExtensions
{
    /// <summary>
    /// Makes the application an auth.md-compliant Service on top of its ASP.NET Core Identity users.
    /// Pair with <see cref="AuthMdEndpointRouteBuilderExtensions.MapAuthMd"/>.
    /// </summary>
    /// <typeparam name="TUser">The ASP.NET Core Identity user type the application already uses.</typeparam>
    public static AuthMdBuilder AddAuthMd<TUser>(this IServiceCollection services, Action<AuthMdOptions> configure)
        where TUser : class
    {
        ArgumentNullException.ThrowIfNull(services);
        ArgumentNullException.ThrowIfNull(configure);

        services.Configure(configure);
        services.TryAddSingleton(TimeProvider.System);
        services.TryAddScoped<ISignedInUserAccessor, IdentitySignedInUserAccessor<TUser>>();

        // OpenIddict is the OAuth 2.0 engine (ADR 0001). Signing and encryption credentials are the
        // application's to register, through its own AddOpenIddict().AddServer(...) call.
        services.AddOpenIddict().AddServer(server =>
        {
            server.SetTokenEndpointUris("oauth2/token");
            server.SetRevocationEndpointUris("oauth2/revoke");
            server.AllowCustomFlow(GrantTypes.JwtBearer);
            server.AllowCustomFlow(GrantTypes.Claim);
            server.AcceptAnonymousClients();
            server.DisableAccessTokenEncryption();

            // The Service's resource URL can be request-derived, so the token endpoint checks it itself.
            server.DisableResourceValidation();
            server.UseAspNetCore().EnableTokenEndpointPassthrough();
            server.AddEventHandler(AttachAgentAuthMetadata.Descriptor);
        });
        // Access Tokens presented to the Service's own API are validated against the local server,
        // including the stored token entry so a revoked token stops working at once.
        services.AddOpenIddict().AddValidation(validation =>
        {
            validation.UseLocalServer();
            validation.UseAspNetCore();
            validation.EnableTokenEntryValidation();
            validation.AddEventHandler(PointChallengeAtResourceMetadata.Descriptor);
            validation.AddEventHandler(RejectTokensOfRevokedRegistrations.Descriptor);
        });
        services.AddAuthorization();

        services.AddOptions<OpenIddictServerOptions>()
            .Configure<IOptions<AuthMdOptions>>((server, authMd) =>
            {
                server.Issuer ??= authMd.Value.Issuer;

                // The Service is not an OpenID Provider: its Scopes replace OpenIddict's default "openid".
                server.Scopes.Remove(OpenIddictConstants.Scopes.OpenId);
                server.Scopes.UnionWith(authMd.Value.Scopes);
            });
        services.TryAddSingleton<IdentityAssertions>();
        services.TryAddScoped<AgentRegistrationManager>();

        return new AuthMdBuilder(services);
    }
}
