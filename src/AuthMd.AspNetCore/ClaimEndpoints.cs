using System.Text.Json;
using System.Text.Json.Nodes;
using System.Text.Json.Serialization;
using Microsoft.AspNetCore.Http;

namespace AuthMd.AspNetCore;

/// <summary>
/// The human half of the Claim Ceremony, called by the Service's own claim page on behalf of the
/// signed-in User: <c>GET /agent/identity/claim/view</c> and <c>POST /agent/identity/claim/confirm</c>.
/// </summary>
internal static class ClaimEndpoints
{
    private sealed record ConfirmRequest(
        [property: JsonPropertyName("claim_attempt_token")] string? ClaimAttemptToken,
        [property: JsonPropertyName("user_code")] string? UserCode);

    public static async Task<IResult> View(
        string? claim_attempt_token,
        HttpContext context,
        ISignedInUserAccessor users,
        IClaimCeremonyStore ceremonies,
        TimeProvider clock,
        CancellationToken cancellationToken)
    {
        var user = await users.FindAsync(context.User);
        if (user is null)
        {
            return NotSignedIn();
        }

        var ceremony = await Find(ceremonies, claim_attempt_token, cancellationToken);
        if (ceremony is null)
        {
            return UnknownClaim();
        }

        if (!users.IsSameEmail(user.Email, ceremony.LoginHint))
        {
            return NotTheNamedUser();
        }

        return Results.Json(Describe(ceremony, clock.GetUtcNow()));
    }

    public static async Task<IResult> Confirm(
        HttpContext context,
        ISignedInUserAccessor users,
        IClaimCeremonyStore ceremonies,
        IAgentRegistrationStore registrations,
        TimeProvider clock,
        CancellationToken cancellationToken)
    {
        var user = await users.FindAsync(context.User);
        if (user is null)
        {
            return NotSignedIn();
        }

        var request = await ReadRequest(context, cancellationToken);

        var ceremony = await Find(ceremonies, request?.ClaimAttemptToken, cancellationToken);
        if (ceremony is null)
        {
            return UnknownClaim();
        }

        if (!users.IsSameEmail(user.Email, ceremony.LoginHint))
        {
            return NotTheNamedUser();
        }

        if (ceremony.Status == ClaimCeremonyStatus.Claimed)
        {
            return ProtocolError.Result(
                "claimed_or_in_flight",
                "This claim has already been confirmed.",
                StatusCodes.Status409Conflict);
        }

        var now = clock.GetUtcNow();
        if (ceremony.ExpiresAt <= now)
        {
            return ProtocolError.Result(
                "claim_expired",
                "The claim ceremony expired before it was confirmed. The agent must register again.",
                StatusCodes.Status410Gone);
        }

        if (!Secrets.Matches(ceremony.UserCode, request!.UserCode))
        {
            return ProtocolError.Result("invalid_request", "The user_code is incorrect.");
        }

        await registrations.CreateAsync(
            new AgentRegistration
            {
                Id = ceremony.RegistrationId,
                UserId = user.Id,
                Scopes = [.. ceremony.Scopes],
                Status = AgentRegistrationStatus.Active,
                CreatedAt = now,
            },
            cancellationToken);

        ceremony.Status = ClaimCeremonyStatus.Claimed;
        ceremony.UserId = user.Id;
        await ceremonies.UpdateAsync(ceremony, cancellationToken);

        return Results.Json(Describe(ceremony, now));
    }

    private static async Task<ConfirmRequest?> ReadRequest(HttpContext context, CancellationToken cancellationToken)
    {
        if (!context.Request.HasJsonContentType())
        {
            return null;
        }

        try
        {
            return await context.Request.ReadFromJsonAsync<ConfirmRequest>(cancellationToken);
        }
        catch (JsonException)
        {
            return null;
        }
    }

    private static async Task<ClaimCeremony?> Find(
        IClaimCeremonyStore ceremonies,
        string? claimAttemptToken,
        CancellationToken cancellationToken) =>
        string.IsNullOrEmpty(claimAttemptToken)
            ? null
            : await ceremonies.FindByClaimAttemptTokenHashAsync(Secrets.Hash(claimAttemptToken), cancellationToken);

    private static IResult NotSignedIn() =>
        ProtocolError.Result(
            "login_required",
            "The user must sign in to the service before viewing or confirming a claim.",
            StatusCodes.Status401Unauthorized);

    private static IResult NotTheNamedUser() =>
        ProtocolError.Result(
            "access_denied",
            "This claim was issued for a different user.",
            StatusCodes.Status403Forbidden);

    private static IResult UnknownClaim() =>
        ProtocolError.Result("invalid_claim_token", "The claim_attempt_token is not recognized.");

    private static JsonObject Describe(ClaimCeremony ceremony, DateTimeOffset now) => new()
    {
        ["registration_id"] = ceremony.RegistrationId,
        ["status"] = ceremony.Status == ClaimCeremonyStatus.Claimed ? "claimed"
            : ceremony.ExpiresAt <= now ? "expired"
            : "pending",
        ["login_hint"] = ceremony.LoginHint,
        ["scopes"] = new JsonArray([.. ceremony.Scopes.Select(scope => JsonValue.Create(scope))]),
        ["expires_at"] = ceremony.ExpiresAt,
    };
}
