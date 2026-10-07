using System.Globalization;
using System.Security.Claims;
using Microsoft.AspNetCore;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Options;
using Microsoft.IdentityModel.Tokens;
using OpenIddict.Abstractions;
using OpenIddict.Server.AspNetCore;
using static OpenIddict.Abstractions.OpenIddictConstants;

namespace AuthMd.AspNetCore;

/// <summary>
/// <c>POST /oauth2/token</c>. OpenIddict validates the request and mints the tokens; this decides
/// whether the grant the Agent presented entitles it to any.
/// </summary>
internal static class TokenEndpoint
{
    public static Task<IResult> Handle(
        HttpContext context,
        IOptions<AuthMdOptions> options,
        IClaimCeremonyStore ceremonies,
        IAgentRegistrationStore registrations,
        IdentityAssertions assertions,
        TimeProvider clock,
        CancellationToken cancellationToken)
    {
        var request = context.GetOpenIddictServerRequest()
            ?? throw new InvalidOperationException("The OpenIddict server request cannot be retrieved.");
        var urls = ServiceUrls.For(context, options.Value);

        // Every Access Token is for this Service's API; an Agent may say so (RFC 8707) but cannot aim elsewhere.
        if (request.Resources is { } resources && resources.Any(resource => resource != urls.Resource))
        {
            return Task.FromResult(Reject(Errors.InvalidTarget, $"The only resource this service issues tokens for is {urls.Resource}"));
        }

        return request.GrantType switch
        {
            GrantTypes.Claim => RedeemClaim(request, urls, options.Value, ceremonies, assertions, clock, cancellationToken),
            GrantTypes.JwtBearer => ExchangeAssertion(request, urls, registrations, assertions, clock, cancellationToken),
            _ => Task.FromResult(Reject(Errors.UnsupportedGrantType, "The grant type is not supported.")),
        };
    }

    /// <summary>The claim grant: the Agent polls until the User has completed the Claim Ceremony.</summary>
    private static async Task<IResult> RedeemClaim(
        OpenIddictRequest request,
        ServiceUrls urls,
        AuthMdOptions options,
        IClaimCeremonyStore ceremonies,
        IdentityAssertions assertions,
        TimeProvider clock,
        CancellationToken cancellationToken)
    {
        var claimToken = (string?)request["claim_token"];
        if (string.IsNullOrEmpty(claimToken))
        {
            return Reject(Errors.InvalidRequest, "The claim grant requires a claim_token.");
        }

        var ceremony = await ceremonies.FindByClaimTokenHashAsync(Secrets.Hash(claimToken), cancellationToken);

        if (ceremony is null || ceremony.RedeemedAt is not null)
        {
            return Reject(Errors.InvalidGrant, "The claim_token is not recognized or has already been used.");
        }

        var now = clock.GetUtcNow();
        var polledTooSoon = now - ceremony.LastPolledAt < options.PollingInterval;
        ceremony.LastPolledAt = now;
        if (!polledTooSoon && ceremony.Status == ClaimCeremonyStatus.Claimed)
        {
            ceremony.RedeemedAt = now;
        }
        await ceremonies.UpdateAsync(ceremony, cancellationToken);

        if (polledTooSoon)
        {
            return Reject("slow_down", "Polling too fast. Add at least 5 seconds to the interval and retry.");
        }

        if (ceremony.Status != ClaimCeremonyStatus.Claimed)
        {
            if (ceremony.ExpiresAt <= now)
            {
                return Reject("expired_token", "The claim ceremony expired before the user completed it. Register again.");
            }

            return Reject("authorization_pending", "The user has not completed the claim ceremony yet.");
        }

        var assertion = assertions.Issue(urls, ceremony.RegistrationId, ceremony.UserId!, ceremony.Scopes, now);

        // The Identity Assertion rides along with OpenIddict's token response as extension parameters.
        var properties = new AuthenticationProperties();
        properties.SetParameter("identity_assertion", assertion.Jwt);
        properties.SetParameter(
            "assertion_expires",
            assertion.Expires.UtcDateTime.ToString("yyyy-MM-dd'T'HH:mm:ss'Z'", CultureInfo.InvariantCulture));

        return Grant(urls, ceremony.UserId!, ceremony.RegistrationId, ceremony.Scopes, properties);
    }

    /// <summary>The RFC 7523 grant: the Agent trades its Identity Assertion for an Access Token.</summary>
    private static async Task<IResult> ExchangeAssertion(
        OpenIddictRequest request,
        ServiceUrls urls,
        IAgentRegistrationStore registrations,
        IdentityAssertions assertions,
        TimeProvider clock,
        CancellationToken cancellationToken)
    {
        if (string.IsNullOrEmpty(request.Assertion))
        {
            return Reject(Errors.InvalidRequest, "The jwt-bearer grant requires an assertion.");
        }

        var registrationId = await assertions.ValidateAsync(urls, request.Assertion, clock.GetUtcNow());
        var registration = registrationId is null
            ? null
            : await registrations.FindByIdAsync(registrationId, cancellationToken);

        if (registration is not { Status: AgentRegistrationStatus.Active })
        {
            return Reject(Errors.InvalidGrant, "The identity assertion is invalid, expired or revoked. Register again.");
        }

        // Least privilege: the Agent may ask for a subset of the Scopes its registration carries.
        var requested = request.GetScopes();
        if (requested.Except(registration.Scopes).Any())
        {
            return Reject(Errors.InvalidScope, "The registration does not carry every requested scope.");
        }

        return Grant(
            urls,
            registration.UserId,
            registration.Id,
            requested.IsEmpty ? registration.Scopes : requested,
            new AuthenticationProperties());
    }

    /// <summary>Hands OpenIddict the principal to mint an Access Token for.</summary>
    private static IResult Grant(
        ServiceUrls urls,
        string userId,
        string registrationId,
        IEnumerable<string> scopes,
        AuthenticationProperties properties)
    {
        var identity = new ClaimsIdentity(
            TokenValidationParameters.DefaultAuthenticationType,
            Claims.Name,
            Claims.Role);
        identity.SetClaim(Claims.Subject, userId);
        identity.SetClaim(IdentityAssertions.RegistrationIdClaim, registrationId);
        identity.SetScopes(scopes);
        identity.SetResources(urls.Resource);
        identity.SetDestinations(static _ => [Destinations.AccessToken]);

        return Results.SignIn(
            new ClaimsPrincipal(identity),
            properties,
            OpenIddictServerAspNetCoreDefaults.AuthenticationScheme);
    }

    private static IResult Reject(string error, string description) =>
        Results.Forbid(
            new AuthenticationProperties(new Dictionary<string, string?>
            {
                [OpenIddictServerAspNetCoreConstants.Properties.Error] = error,
                [OpenIddictServerAspNetCoreConstants.Properties.ErrorDescription] = description,
            }),
            [OpenIddictServerAspNetCoreDefaults.AuthenticationScheme]);
}
