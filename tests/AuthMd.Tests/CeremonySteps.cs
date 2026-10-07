using System.Net.Http.Json;
using System.Text.Json;
using System.Web;

namespace AuthMd.Tests;

/// <summary>What the Agent holds after a <c>service_auth</c> registration.</summary>
internal sealed record IssuedClaim(string RegistrationId, string ClaimToken, string UserCode, string ClaimAttemptToken);

/// <summary>
/// The steps of the Claim Ceremony as the Agent and the User's browser perform them over HTTP.
/// </summary>
internal static class CeremonySteps
{
    public static async Task<IssuedClaim> RegisterAsync(this HttpClient agent, string email)
    {
        using var response = await agent.PostAsJsonAsync("/agent/identity", new { type = "service_auth", login_hint = email });
        response.EnsureSuccessStatusCode();
        var registration = await response.Content.ReadFromJsonAsync<JsonElement>();
        var claim = registration.GetProperty("claim");
        var verificationUri = new Uri(claim.String("verification_uri")!);

        return new IssuedClaim(
            registration.String("registration_id")!,
            registration.String("claim_token")!,
            claim.String("user_code")!,
            HttpUtility.ParseQueryString(verificationUri.Query)["claim_attempt_token"]!);
    }

    /// <summary>Signs the browser in as the seeded Identity User with this email; returns the User's id.</summary>
    public static async Task<string> SignInAsync(this HttpClient browser, string email)
    {
        using var response = await browser.PostAsync($"/test/sign-in?email={Uri.EscapeDataString(email)}", content: null);
        response.EnsureSuccessStatusCode();
        return (await response.Content.ReadFromJsonAsync<JsonElement>()).String("id")!;
    }

    /// <summary>The claims of a JWT as its holder can read them. The signature is not checked.</summary>
    public static JsonElement JwtPayload(string jwt)
    {
        var payload = jwt.Split('.')[1];
        return JsonSerializer.Deserialize<JsonElement>(System.Buffers.Text.Base64Url.DecodeFromChars(payload));
    }

    public static Task<HttpResponseMessage> ViewClaimAsync(this HttpClient browser, string claimAttemptToken) =>
        browser.GetAsync($"/agent/identity/claim/view?claim_attempt_token={Uri.EscapeDataString(claimAttemptToken)}");

    public static Task<HttpResponseMessage> ConfirmClaimAsync(this HttpClient browser, string claimAttemptToken, string userCode) =>
        browser.PostAsJsonAsync("/agent/identity/claim/confirm", new
        {
            claim_attempt_token = claimAttemptToken,
            user_code = userCode,
        });

    /// <summary>The Agent's poll of the token endpoint with the claim grant.</summary>
    public static Task<HttpResponseMessage> PollAsync(this HttpClient agent, string claimToken) =>
        agent.PostAsync("/oauth2/token", new FormUrlEncodedContent(new Dictionary<string, string>
        {
            ["grant_type"] = "urn:workos:agent-auth:grant-type:claim",
            ["claim_token"] = claimToken,
        }));

    /// <summary>
    /// Runs the whole ceremony for a User with this email: register, sign in, confirm, poll.
    /// Returns the token response the Agent ends up holding.
    /// </summary>
    public static async Task<JsonElement> CompleteCeremonyAsync(this HttpClient agent, HttpClient browser, string email)
    {
        var claim = await agent.RegisterAsync(email);
        await browser.SignInAsync(email);
        using var confirmation = await browser.ConfirmClaimAsync(claim.ClaimAttemptToken, claim.UserCode);
        confirmation.EnsureSuccessStatusCode();
        using var poll = await agent.PollAsync(claim.ClaimToken);
        poll.EnsureSuccessStatusCode();
        return await poll.Content.ReadFromJsonAsync<JsonElement>();
    }

    /// <summary>The Agent's RFC 7523 exchange of an Identity Assertion for an Access Token.</summary>
    public static Task<HttpResponseMessage> ExchangeAsync(
        this HttpClient agent,
        string? identityAssertion,
        string? resource = null,
        string? scope = null)
    {
        var form = new Dictionary<string, string> { ["grant_type"] = "urn:ietf:params:oauth:grant-type:jwt-bearer" };
        if (identityAssertion is not null) form["assertion"] = identityAssertion;
        if (resource is not null) form["resource"] = resource;
        if (scope is not null) form["scope"] = scope;
        return agent.PostAsync("/oauth2/token", new FormUrlEncodedContent(form));
    }

    /// <summary>The Agent's call to a protected API, with or without a bearer token.</summary>
    public static Task<HttpResponseMessage> CallApiAsync(this HttpClient agent, HttpMethod method, string path, string? accessToken)
    {
        var request = new HttpRequestMessage(method, path);
        if (accessToken is not null)
        {
            request.Headers.Authorization = new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", accessToken);
        }
        return agent.SendAsync(request);
    }

    public static async Task<JsonElement> JsonAsync(this Task<HttpResponseMessage> pending)
    {
        using var response = await pending;
        return await response.Content.ReadFromJsonAsync<JsonElement>();
    }
}
