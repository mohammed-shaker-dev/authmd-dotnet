namespace AuthMd.Client;

/// <summary>
/// What an Agent learns about a Service from its auth.md discovery documents.
/// </summary>
public sealed record DiscoveredService
{
    /// <summary>The canonical URL of the API the Agent wants to call.</summary>
    public required Uri Resource { get; init; }

    /// <summary>The Service's display name, to show the User when asking for consent.</summary>
    public string? ResourceName { get; init; }

    /// <summary>The Service's logo, to show alongside <see cref="ResourceName"/>.</summary>
    public Uri? ResourceLogoUri { get; init; }

    /// <summary>The Scopes the Service understands.</summary>
    public IReadOnlyList<string> ScopesSupported { get; init; } = [];

    /// <summary>The issuer of the Service's tokens.</summary>
    public required Uri Issuer { get; init; }

    /// <summary>Where the Agent polls the Claim Ceremony and exchanges its Identity Assertion.</summary>
    public required Uri TokenEndpoint { get; init; }

    /// <summary>Where the Agent revokes an Access Token, if the Service offers it.</summary>
    public Uri? RevocationEndpoint { get; init; }

    /// <summary>Where the Agent registers.</summary>
    public required Uri IdentityEndpoint { get; init; }

    /// <summary>The registration methods the Service accepts, e.g. <c>service_auth</c>.</summary>
    public IReadOnlyList<string> IdentityTypesSupported { get; init; } = [];

    /// <summary>Whether the Agent can register with only the User's email.</summary>
    public bool SupportsServiceAuth => IdentityTypesSupported.Contains("service_auth");

    /// <summary>The Service's <c>auth.md</c> document: its instructions to Agents, as Markdown.</summary>
    public string? Instructions { get; init; }
}
