namespace AuthMd.AspNetCore;

/// <summary>
/// Persists Claim Ceremonies. Implement this to use a persistence technology other than EF Core.
/// </summary>
public interface IClaimCeremonyStore
{
    /// <summary>
    /// Persists a newly issued Claim Ceremony.
    /// </summary>
    Task CreateAsync(ClaimCeremony ceremony, CancellationToken cancellationToken);

    /// <summary>
    /// Finds the Claim Ceremony whose <see cref="ClaimCeremony.ClaimTokenHash"/> matches, if any.
    /// </summary>
    Task<ClaimCeremony?> FindByClaimTokenHashAsync(string claimTokenHash, CancellationToken cancellationToken);

    /// <summary>
    /// Finds the Claim Ceremony whose <see cref="ClaimCeremony.ClaimAttemptTokenHash"/> matches, if any.
    /// </summary>
    Task<ClaimCeremony?> FindByClaimAttemptTokenHashAsync(string claimAttemptTokenHash, CancellationToken cancellationToken);

    /// <summary>
    /// Persists changes to an existing Claim Ceremony.
    /// </summary>
    Task UpdateAsync(ClaimCeremony ceremony, CancellationToken cancellationToken);
}
