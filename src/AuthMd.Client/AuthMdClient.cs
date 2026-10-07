using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using System.Text.RegularExpressions;

namespace AuthMd.Client;

/// <summary>
/// The Agent's side of the auth.md protocol.
/// </summary>
/// <param name="http">The client used to reach the Service.</param>
/// <param name="timeProvider">The clock polling delays and token expiry are measured on; the system clock by default.</param>
public sealed partial class AuthMdClient(HttpClient http, TimeProvider? timeProvider = null)
{
    private readonly TimeProvider _clock = timeProvider ?? TimeProvider.System;

    /// <summary>
    /// Registers with the Service on behalf of the User with this email, starting a Claim Ceremony.
    /// Nothing is granted until the User approves: see <see cref="PendingRegistration"/>.
    /// </summary>
    /// <exception cref="AuthMdProtocolException">The Service refused the registration.</exception>
    public async Task<PendingRegistration> RegisterAsync(
        DiscoveredService service,
        string email,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(service);
        ArgumentException.ThrowIfNullOrEmpty(email);

        using var response = await http.PostAsJsonAsync(
            service.IdentityEndpoint,
            new Dictionary<string, string> { ["type"] = "service_auth", ["login_hint"] = email },
            cancellationToken);
        var body = await response.Content.ReadFromJsonAsync<JsonElement>(cancellationToken);
        if (!response.IsSuccessStatusCode)
        {
            throw new AuthMdProtocolException(
                OptionalString(body, "error") ?? $"http_{(int)response.StatusCode}",
                OptionalString(body, "error_description"));
        }

        var claim = body.GetProperty("claim");
        var expiresIn = TimeSpan.FromSeconds(claim.GetProperty("expires_in").GetInt32());

        return new PendingRegistration(
            new ServiceConnection(http, _clock, service),
            claimToken: body.GetProperty("claim_token").GetString()!,
            registrationId: body.GetProperty("registration_id").GetString()!,
            userCode: claim.GetProperty("user_code").GetString()!,
            verificationUri: new Uri(claim.GetProperty("verification_uri").GetString()!),
            expiresAt: _clock.GetUtcNow() + expiresIn,
            interval: TimeSpan.FromSeconds(claim.GetProperty("interval").GetInt32()),
            scopes: Strings(body, "post_claim_scopes"));
    }

    /// <summary>
    /// Learns how to register with a Service by walking its discovery documents: Protected Resource
    /// Metadata, then authorization-server metadata, then <c>auth.md</c>.
    /// </summary>
    /// <param name="serviceUrl">
    /// The Service's base URL, or the URL of one of its protected APIs. If the URL answers 401 with a
    /// <c>WWW-Authenticate</c> header naming the resource metadata, that is followed; otherwise the
    /// well-known path on the same host is used.
    /// </param>
    /// <param name="cancellationToken">Cancels the discovery.</param>
    /// <exception cref="AuthMdDiscoveryException">The documents could not be fetched or understood.</exception>
    public async Task<DiscoveredService> DiscoverAsync(Uri serviceUrl, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(serviceUrl);

        var resourceMetadataUrl = await ResourceMetadataUrlFromChallengeAsync(serviceUrl, cancellationToken)
            ?? new Uri(serviceUrl, "/.well-known/oauth-protected-resource");
        var resource = await FetchDocumentAsync(resourceMetadataUrl, cancellationToken);

        var authorizationServer = Strings(resource, "authorization_servers").FirstOrDefault()
            ?? throw new AuthMdDiscoveryException($"{resourceMetadataUrl} names no authorization server.");
        var serverMetadataUrl = new Uri(authorizationServer.TrimEnd('/') + "/.well-known/oauth-authorization-server");
        var server = await FetchDocumentAsync(serverMetadataUrl, cancellationToken);

        if (!server.TryGetProperty("agent_auth", out var agentAuth) || agentAuth.ValueKind != JsonValueKind.Object)
        {
            throw new AuthMdDiscoveryException($"{serverMetadataUrl} has no agent_auth block: the service does not support auth.md.");
        }

        return new DiscoveredService
        {
            Resource = RequiredUri(resource, "resource", resourceMetadataUrl),
            ResourceName = OptionalString(resource, "resource_name"),
            ResourceLogoUri = OptionalUri(resource, "resource_logo_uri"),
            ScopesSupported = Strings(resource, "scopes_supported"),
            Issuer = RequiredUri(server, "issuer", serverMetadataUrl),
            TokenEndpoint = RequiredUri(server, "token_endpoint", serverMetadataUrl),
            RevocationEndpoint = OptionalUri(server, "revocation_endpoint"),
            IdentityEndpoint = RequiredUri(agentAuth, "identity_endpoint", serverMetadataUrl),
            IdentityTypesSupported = Strings(agentAuth, "identity_types_supported"),
            Instructions = OptionalUri(agentAuth, "skill") is { } skill
                ? await FetchInstructionsAsync(skill, cancellationToken)
                : null,
        };
    }

    /// <summary>
    /// The first hop when the Agent starts from an API that turned it away:
    /// <c>WWW-Authenticate: Bearer resource_metadata="…"</c>.
    /// </summary>
    private async Task<Uri?> ResourceMetadataUrlFromChallengeAsync(Uri url, CancellationToken cancellationToken)
    {
        try
        {
            using var response = await http.GetAsync(url, HttpCompletionOption.ResponseHeadersRead, cancellationToken);
            if (response.StatusCode != HttpStatusCode.Unauthorized)
            {
                return null;
            }

            return response.Headers.WwwAuthenticate
                .Select(challenge => ResourceMetadataParameter().Match(challenge.Parameter ?? string.Empty))
                .Where(match => match.Success && Uri.IsWellFormedUriString(match.Groups[1].Value, UriKind.Absolute))
                .Select(match => new Uri(match.Groups[1].Value))
                .FirstOrDefault();
        }
        catch (HttpRequestException)
        {
            // Unreachable as given; the well-known path gets its own, reported, attempt.
            return null;
        }
    }

    private async Task<JsonElement> FetchDocumentAsync(Uri url, CancellationToken cancellationToken)
    {
        try
        {
            var document = await http.GetFromJsonAsync<JsonElement>(url, cancellationToken);
            return document.ValueKind == JsonValueKind.Object
                ? document
                : throw new AuthMdDiscoveryException($"{url} is not a JSON object.");
        }
        catch (Exception exception) when (exception is HttpRequestException or JsonException)
        {
            throw new AuthMdDiscoveryException($"Could not read the discovery document at {url}: {exception.Message}", exception);
        }
    }

    private async Task<string?> FetchInstructionsAsync(Uri url, CancellationToken cancellationToken)
    {
        try
        {
            return await http.GetStringAsync(url, cancellationToken);
        }
        catch (HttpRequestException)
        {
            // The metadata is enough to register; the prose is for the Agent's benefit.
            return null;
        }
    }

    private static Uri RequiredUri(JsonElement element, string name, Uri source) =>
        OptionalUri(element, name)
            ?? throw new AuthMdDiscoveryException($"{source} is missing \"{name}\" or it is not an absolute URL.");

    private static string? OptionalString(JsonElement element, string name) =>
        element.TryGetProperty(name, out var value) && value.ValueKind == JsonValueKind.String ? value.GetString() : null;

    private static Uri? OptionalUri(JsonElement element, string name) =>
        Uri.TryCreate(OptionalString(element, name), UriKind.Absolute, out var uri) ? uri : null;

    private static IReadOnlyList<string> Strings(JsonElement element, string name) =>
        element.TryGetProperty(name, out var values) && values.ValueKind == JsonValueKind.Array
            ? [.. values.EnumerateArray().Where(value => value.ValueKind == JsonValueKind.String).Select(value => value.GetString()!)]
            : [];

    [GeneratedRegex("resource_metadata=\"([^\"]+)\"")]
    private static partial Regex ResourceMetadataParameter();
}
