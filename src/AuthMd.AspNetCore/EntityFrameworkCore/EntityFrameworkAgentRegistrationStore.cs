using Microsoft.EntityFrameworkCore;

namespace AuthMd.AspNetCore.EntityFrameworkCore;

internal sealed class EntityFrameworkAgentRegistrationStore<TContext>(TContext context) : IAgentRegistrationStore
    where TContext : DbContext
{
    public async Task CreateAsync(AgentRegistration registration, CancellationToken cancellationToken)
    {
        context.Set<AgentRegistration>().Add(registration);
        await context.SaveChangesAsync(cancellationToken);
    }

    public async Task UpdateAsync(AgentRegistration registration, CancellationToken cancellationToken)
    {
        context.Set<AgentRegistration>().Update(registration);
        await context.SaveChangesAsync(cancellationToken);
    }

    public Task<AgentRegistration?> FindByIdAsync(string id, CancellationToken cancellationToken) =>
        context.Set<AgentRegistration>().SingleOrDefaultAsync(registration => registration.Id == id, cancellationToken);
}
