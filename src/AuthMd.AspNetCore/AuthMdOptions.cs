using Microsoft.AspNetCore.Http;

namespace AuthMd.AspNetCore;

/// <summary>
/// Configures how the Service presents itself to Agents.
/// </summary>
public sealed class AuthMdOptions
{
    /// <summary>
    /// The public base URL of the Service, e.g. <c>https://service.example.com</c>.
    /// When <see langword="null"/>, it is derived from each incoming request.
    /// </summary>
    public Uri? Issuer { get; set; }

    /// <summary>
    /// The display name of the Service, surfaced to the User when an Agent asks for consent.
    /// </summary>
    public string? ResourceName { get; set; }

    /// <summary>
    /// The URL of the Service's logo, surfaced alongside <see cref="ResourceName"/>.
    /// </summary>
    public Uri? ResourceLogoUri { get; set; }

    /// <summary>
    /// The Scopes the Service supports. Advertised in the metadata documents.
    /// </summary>
    public IList<string> Scopes { get; } = [];

    /// <summary>
    /// The path of the Service's own claim page, where the signed-in User enters the <c>user_code</c>.
    /// The package is headless: the application provides this page.
    /// </summary>
    public PathString ClaimPagePath { get; set; } = "/claim";

    /// <summary>
    /// How long a Claim Ceremony stays claimable after it is issued.
    /// </summary>
    public TimeSpan ClaimLifetime { get; set; } = TimeSpan.FromMinutes(10);

    /// <summary>
    /// How long an Identity Assertion can be exchanged for Access Tokens before the Agent must register again.
    /// </summary>
    public TimeSpan IdentityAssertionLifetime { get; set; } = TimeSpan.FromDays(7);

    /// <summary>
    /// The minimum time an Agent should wait between polls of the token endpoint.
    /// </summary>
    public TimeSpan PollingInterval { get; set; } = TimeSpan.FromSeconds(5);
}
