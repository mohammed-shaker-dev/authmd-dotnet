using Microsoft.Extensions.Options;
using Microsoft.IdentityModel.JsonWebTokens;
using Microsoft.IdentityModel.Tokens;
using OpenIddict.Server;

namespace AuthMd.AspNetCore;

/// <summary>An Identity Assertion as handed to the Agent.</summary>
internal readonly record struct IssuedIdentityAssertion(string Jwt, DateTimeOffset Expires);

/// <summary>
/// Mints Identity Assertions: Service-signed JWTs representing an Agent Registration, signed with the
/// same credentials OpenIddict signs Access Tokens with.
/// </summary>
internal sealed class IdentityAssertions(
    IOptionsMonitor<OpenIddictServerOptions> server,
    IOptions<AuthMdOptions> options)
{
    public const string TokenType = "agent-identity-assertion+jwt";

    public const string RegistrationIdClaim = "registration_id";

    private readonly JsonWebTokenHandler _handler = new();

    public IssuedIdentityAssertion Issue(
        ServiceUrls urls,
        string registrationId,
        string userId,
        IEnumerable<string> scopes,
        DateTimeOffset now)
    {
        // JWT timestamps are whole seconds; keep the advertised expiry identical to the signed one.
        var issuedAt = DateTimeOffset.FromUnixTimeSeconds(now.ToUnixTimeSeconds());
        var expires = issuedAt + options.Value.IdentityAssertionLifetime;

        var jwt = _handler.CreateToken(new SecurityTokenDescriptor
        {
            TokenType = TokenType,
            Issuer = urls.Issuer,
            Audience = urls.Issuer,
            IssuedAt = issuedAt.UtcDateTime,
            NotBefore = issuedAt.UtcDateTime,
            Expires = expires.UtcDateTime,
            SigningCredentials = server.CurrentValue.SigningCredentials[0],
            Claims = new Dictionary<string, object>
            {
                ["sub"] = userId,
                ["jti"] = Guid.NewGuid().ToString("N"),
                [RegistrationIdClaim] = registrationId,
                ["scope"] = string.Join(' ', scopes),
            },
        });

        return new IssuedIdentityAssertion(jwt, expires);
    }

    /// <summary>
    /// Verifies an Identity Assertion this Service signed and returns the id of the Agent Registration
    /// it represents, or <see langword="null"/> if the assertion is not acceptable.
    /// </summary>
    public async Task<string?> ValidateAsync(ServiceUrls urls, string jwt, DateTimeOffset now)
    {
        var result = await _handler.ValidateTokenAsync(jwt, new TokenValidationParameters
        {
            // Access Tokens are signed with the same key: only the token type tells them apart.
            ValidTypes = [TokenType],
            ValidIssuer = urls.Issuer,
            ValidAudience = urls.Issuer,
            IssuerSigningKeys = server.CurrentValue.SigningCredentials.Select(credentials => credentials.Key),

            // Judged against the Service's clock, with no grace period.
            RequireExpirationTime = true,
            LifetimeValidator = (notBefore, expires, _, _) =>
                expires > now.UtcDateTime && (notBefore is null || notBefore <= now.UtcDateTime),
        });

        return result.IsValid ? result.Claims[RegistrationIdClaim] as string : null;
    }
}
