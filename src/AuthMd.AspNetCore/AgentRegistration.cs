namespace AuthMd.AspNetCore;

/// <summary>
/// The state of an Agent Registration.
/// </summary>
public enum AgentRegistrationStatus
{
    /// <summary>The Agent may obtain credentials on behalf of the User.</summary>
    Active,

    /// <summary>Revoked: every credential derived from the registration is invalid.</summary>
    Revoked,
}

/// <summary>
/// The persistent relationship between an Agent and the Service, on behalf of a User.
/// The unit of revocation.
/// </summary>
public class AgentRegistration
{
    /// <summary>The <c>registration_id</c> the Agent was given when it registered.</summary>
    public string Id { get; set; } = default!;

    /// <summary>The key of the User the Agent acts on behalf of.</summary>
    public string UserId { get; set; } = default!;

    /// <summary>The Scopes granted to the Agent.</summary>
    public List<string> Scopes { get; set; } = [];

    /// <summary>Whether the registration is still in force.</summary>
    public AgentRegistrationStatus Status { get; set; }

    /// <summary>When the User confirmed the Claim Ceremony.</summary>
    public DateTimeOffset CreatedAt { get; set; }
}
