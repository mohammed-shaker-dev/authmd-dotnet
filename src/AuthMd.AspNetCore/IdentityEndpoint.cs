using System.Net.Mail;
using System.Text.Json;
using System.Text.Json.Nodes;
using System.Text.Json.Serialization;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Options;

namespace AuthMd.AspNetCore;

/// <summary>
/// <c>POST /agent/identity</c>: where an Agent registers with the Service.
/// </summary>
internal static class IdentityEndpoint
{
    private sealed record Request(
        [property: JsonPropertyName("type")] string? Type,
        [property: JsonPropertyName("login_hint")] string? LoginHint);

    public static async Task<IResult> Handle(
        HttpContext context,
        IOptions<AuthMdOptions> options,
        IClaimCeremonyStore store,
        TimeProvider clock,
        CancellationToken cancellationToken)
    {
        var request = await ReadRequest(context, cancellationToken);

        // v1 implements service_auth only (ADR 0002); the other registration methods dispatch from here.
        return request?.Type switch
        {
            "service_auth" => await RegisterServiceAuth(request, context, options.Value, store, clock, cancellationToken),
            "anonymous" => Error("anonymous_not_enabled", "This service does not accept anonymous registrations."),
            "identity_assertion" => Error("issuer_not_enabled", "This service does not trust any identity assertion issuer."),
            _ => Error("invalid_request", "The body must be a JSON object with a supported \"type\"."),
        };
    }

    private static async Task<Request?> ReadRequest(HttpContext context, CancellationToken cancellationToken)
    {
        if (!context.Request.HasJsonContentType())
        {
            return null;
        }

        try
        {
            return await context.Request.ReadFromJsonAsync<Request>(cancellationToken);
        }
        catch (JsonException)
        {
            return null;
        }
    }

    private static async Task<IResult> RegisterServiceAuth(
        Request request,
        HttpContext context,
        AuthMdOptions options,
        IClaimCeremonyStore store,
        TimeProvider clock,
        CancellationToken cancellationToken)
    {
        if (!MailAddress.TryCreate(request.LoginHint, out _))
        {
            return Error("invalid_request", "\"login_hint\" must be the user's email address.");
        }

        var urls = ServiceUrls.For(context, options);
        var now = clock.GetUtcNow();
        var claimToken = Secrets.NewToken("clm_");
        var claimAttemptToken = Secrets.NewToken("cat_");

        var ceremony = new ClaimCeremony
        {
            Id = Guid.CreateVersion7().ToString("N"),
            RegistrationId = "reg_" + Guid.CreateVersion7().ToString("N"),
            ClaimTokenHash = Secrets.Hash(claimToken),
            ClaimAttemptTokenHash = Secrets.Hash(claimAttemptToken),
            UserCode = Secrets.NewUserCode(),
            LoginHint = request.LoginHint!,
            Scopes = [.. options.Scopes],
            Status = ClaimCeremonyStatus.Pending,
            CreatedAt = now,
            ExpiresAt = now + options.ClaimLifetime,
        };
        await store.CreateAsync(ceremony, cancellationToken);

        return Results.Json(new JsonObject
        {
            ["registration_id"] = ceremony.RegistrationId,
            ["registration_type"] = "service_auth",
            ["claim_url"] = urls.ClaimEndpoint,
            ["claim_token"] = claimToken,
            ["claim_token_expires"] = ceremony.ExpiresAt,
            ["post_claim_scopes"] = new JsonArray([.. ceremony.Scopes.Select(scope => JsonValue.Create(scope))]),
            ["claim"] = new JsonObject
            {
                ["user_code"] = ceremony.UserCode,
                ["expires_in"] = (int)options.ClaimLifetime.TotalSeconds,
                ["verification_uri"] = urls.ClaimPage(options.ClaimPagePath, claimAttemptToken),
                ["interval"] = (int)options.PollingInterval.TotalSeconds,
            },
        });
    }

    private static IResult Error(string code, string description) => ProtocolError.Result(code, description);
}
