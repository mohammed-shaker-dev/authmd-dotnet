namespace AuthMd.AspNetCore;

/// <summary>
/// Persists Agent Registrations. Implement this to use a persistence technology other than EF Core.
/// </summary>
public interface IAgentRegistrationStore
{
    /// <summary>
    /// Persists a new Agent Registration.
    /// </summary>
    Task CreateAsync(AgentRegistration registration, CancellationToken cancellationToken);

    /// <summary>
    /// Finds the Agent Registration with this id, if any.
    /// </summary>
    Task<AgentRegistration?> FindByIdAsync(string id, CancellationToken cancellationToken);

    /// <summary>
    /// Persists changes to an existing Agent Registration.
    /// </summary>
    Task UpdateAsync(AgentRegistration registration, CancellationToken cancellationToken);
}
