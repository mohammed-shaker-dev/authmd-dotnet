using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Builder;
using OpenIddict.Abstractions;
using OpenIddict.Validation.AspNetCore;

namespace AuthMd.AspNetCore;

/// <summary>
/// Protects the Service's own API so that only Agents holding a valid Access Token get through.
/// </summary>
public static class AuthMdAuthorizationExtensions
{
    /// <summary>
    /// The authentication scheme that validates agent Access Tokens, for use with
    /// <c>[Authorize(AuthenticationSchemes = ...)]</c>.
    /// </summary>
    public const string AuthenticationScheme = OpenIddictValidationAspNetCoreDefaults.AuthenticationScheme;

    /// <summary>
    /// Requires a valid agent Access Token carrying every one of <paramref name="scopes"/>.
    /// </summary>
    public static AuthorizationPolicyBuilder RequireAgentScope(this AuthorizationPolicyBuilder policy, params string[] scopes)
    {
        ArgumentNullException.ThrowIfNull(policy);
        ArgumentNullException.ThrowIfNull(scopes);

        policy.AddAuthenticationSchemes(AuthenticationScheme);
        policy.RequireAuthenticatedUser();
        policy.RequireAssertion(context => scopes.All(scope => context.User.HasScope(scope)));
        return policy;
    }

    /// <summary>
    /// Requires a valid agent Access Token carrying every one of <paramref name="scopes"/> to reach the endpoint.
    /// </summary>
    public static TBuilder RequireAgentScope<TBuilder>(this TBuilder builder, params string[] scopes)
        where TBuilder : IEndpointConventionBuilder
    {
        ArgumentNullException.ThrowIfNull(builder);

        return builder.RequireAuthorization(policy => policy.RequireAgentScope(scopes));
    }
}
