using Microsoft.EntityFrameworkCore;

namespace AuthMd.AspNetCore.EntityFrameworkCore;

internal sealed class EntityFrameworkClaimCeremonyStore<TContext>(TContext context) : IClaimCeremonyStore
    where TContext : DbContext
{
    public async Task CreateAsync(ClaimCeremony ceremony, CancellationToken cancellationToken)
    {
        context.Set<ClaimCeremony>().Add(ceremony);
        await context.SaveChangesAsync(cancellationToken);
    }

    public Task<ClaimCeremony?> FindByClaimTokenHashAsync(string claimTokenHash, CancellationToken cancellationToken) =>
        context.Set<ClaimCeremony>()
            .SingleOrDefaultAsync(ceremony => ceremony.ClaimTokenHash == claimTokenHash, cancellationToken);

    public Task<ClaimCeremony?> FindByClaimAttemptTokenHashAsync(string claimAttemptTokenHash, CancellationToken cancellationToken) =>
        context.Set<ClaimCeremony>()
            .SingleOrDefaultAsync(ceremony => ceremony.ClaimAttemptTokenHash == claimAttemptTokenHash, cancellationToken);

    public async Task UpdateAsync(ClaimCeremony ceremony, CancellationToken cancellationToken)
    {
        context.Set<ClaimCeremony>().Update(ceremony);
        await context.SaveChangesAsync(cancellationToken);
    }
}
