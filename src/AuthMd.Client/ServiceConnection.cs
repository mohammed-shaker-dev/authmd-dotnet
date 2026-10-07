using System.Net.Http.Json;
using System.Text.Json;

namespace AuthMd.Client;

/// <summary>What the token endpoint answered: tokens, or a protocol error.</summary>
internal readonly record struct TokenResponse(JsonElement Body)
{
    public string? Error => String("error");

    public string? ErrorDescription => String("error_description");

    public string? String(string name) =>
        Body.ValueKind == JsonValueKind.Object && Body.TryGetProperty(name, out var value) && value.ValueKind == JsonValueKind.String
            ? value.GetString()
            : null;

    public double? Number(string name) =>
        Body.ValueKind == JsonValueKind.Object && Body.TryGetProperty(name, out var value) && value.ValueKind == JsonValueKind.Number
            ? value.GetDouble()
            : null;

    public AuthMdProtocolException ToException() => new(Error ?? "invalid_response", ErrorDescription);
}

/// <summary>
/// The Agent's line to one discovered Service: its HTTP client, its clock, and the Service's endpoints.
/// </summary>
internal sealed class ServiceConnection(HttpClient http, TimeProvider clock, DiscoveredService service)
{
    public HttpClient Http { get; } = http;

    public TimeProvider Clock { get; } = clock;

    public DiscoveredService Service { get; } = service;

    public async Task<TokenResponse> RequestTokenAsync(Dictionary<string, string> form, CancellationToken cancellationToken)
    {
        using var response = await Http.PostAsync(Service.TokenEndpoint, new FormUrlEncodedContent(form), cancellationToken);
        return new TokenResponse(await response.Content.ReadFromJsonAsync<JsonElement>(cancellationToken));
    }
}
