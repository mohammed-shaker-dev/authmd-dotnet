using Microsoft.AspNetCore.Http;

namespace AuthMd.AspNetCore;

/// <summary>
/// The absolute URLs the Service advertises, rooted at its configured or request-derived base URL.
/// </summary>
internal readonly struct ServiceUrls
{
    private readonly string _base;

    private ServiceUrls(string baseUrl) => _base = baseUrl.TrimEnd('/');

    public static ServiceUrls For(HttpContext context, AuthMdOptions options)
    {
        var request = context.Request;
        return new ServiceUrls(options.Issuer?.ToString() ?? $"{request.Scheme}://{request.Host}{request.PathBase}");
    }

    public static ServiceUrls For(Uri requestBaseUri, AuthMdOptions options) =>
        new((options.Issuer ?? requestBaseUri).ToString());

    /// <summary>The issuer identifier, in the form OpenIddict puts in the tokens it signs.</summary>
    public string Issuer => _base + "/";

    public string Resource => _base + "/";

    public string Skill => _base + "/auth.md";

    public string ProtectedResourceMetadata => _base + "/.well-known/oauth-protected-resource";

    public string AuthorizationServerMetadata => _base + "/.well-known/oauth-authorization-server";

    public string TokenEndpoint => _base + "/oauth2/token";

    public string RevocationEndpoint => _base + "/oauth2/revoke";

    public string IdentityEndpoint => _base + "/agent/identity";

    public string ClaimEndpoint => _base + "/agent/identity/claim";

    /// <summary>
    /// The Service-owned claim page, identified by the claim attempt so the URL never carries the user_code.
    /// </summary>
    public string ClaimPage(PathString path, string claimAttemptToken) =>
        $"{_base}{path}?claim_attempt_token={Uri.EscapeDataString(claimAttemptToken)}";
}
