namespace AuthMd.AspNetCore;

/// <summary>
/// The state of a Claim Ceremony.
/// </summary>
public enum ClaimCeremonyStatus
{
    /// <summary>Issued to the Agent; the User has not confirmed yet.</summary>
    Pending,

    /// <summary>The User authenticated and confirmed the <c>user_code</c>.</summary>
    Claimed,
}

/// <summary>
/// A <c>service_auth</c> Claim Ceremony: the user-verification process that turns an Agent's
/// registration request into an Agent Registration once the User confirms.
/// </summary>
public class ClaimCeremony
{
    /// <summary>The store key.</summary>
    public string Id { get; set; } = default!;

    /// <summary>The <c>registration_id</c> returned to the Agent.</summary>
    public string RegistrationId { get; set; } = default!;

    /// <summary>SHA-256 hash of the <c>claim_token</c> the Agent polls with. The token itself is never stored.</summary>
    public string ClaimTokenHash { get; set; } = default!;

    /// <summary>SHA-256 hash of the <c>claim_attempt_token</c> embedded in the verification URL.</summary>
    public string ClaimAttemptTokenHash { get; set; } = default!;

    /// <summary>The short code the Agent hands to the User, who enters it on the Service's claim page.</summary>
    public string UserCode { get; set; } = default!;

    /// <summary>The User's email as presented by the Agent.</summary>
    public string LoginHint { get; set; } = default!;

    /// <summary>The key of the User who confirmed the ceremony; <see langword="null"/> while pending.</summary>
    public string? UserId { get; set; }

    /// <summary>The Scopes the Agent Registration will carry once claimed.</summary>
    public List<string> Scopes { get; set; } = [];

    /// <summary>Where the ceremony is in <c>pending → claimed</c>.</summary>
    public ClaimCeremonyStatus Status { get; set; }

    /// <summary>When the ceremony was issued.</summary>
    public DateTimeOffset CreatedAt { get; set; }

    /// <summary>When the Agent last polled the token endpoint for this ceremony.</summary>
    public DateTimeOffset? LastPolledAt { get; set; }

    /// <summary>When the Agent collected its tokens. A <c>claim_token</c> yields tokens only once.</summary>
    public DateTimeOffset? RedeemedAt { get; set; }

    /// <summary>When the ceremony stops being claimable.</summary>
    public DateTimeOffset ExpiresAt { get; set; }
}
