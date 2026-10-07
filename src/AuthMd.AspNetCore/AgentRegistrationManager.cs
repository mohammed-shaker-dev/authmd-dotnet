namespace AuthMd.AspNetCore;

/// <summary>
/// Server-side operations on Agent Registrations, for the Service's own account or admin pages.
/// </summary>
public sealed class AgentRegistrationManager(IAgentRegistrationStore registrations)
{
    /// <summary>
    /// Revokes an Agent Registration. Its Identity Assertion can no longer be exchanged, and every
    /// Access Token derived from it stops being accepted.
    /// </summary>
    /// <returns><see langword="false"/> if no registration has this id.</returns>
    public async Task<bool> RevokeAsync(string registrationId, CancellationToken cancellationToken = default)
    {
        var registration = await registrations.FindByIdAsync(registrationId, cancellationToken);
        if (registration is null)
        {
            return false;
        }

        registration.Status = AgentRegistrationStatus.Revoked;
        await registrations.UpdateAsync(registration, cancellationToken);
        return true;
    }
}
