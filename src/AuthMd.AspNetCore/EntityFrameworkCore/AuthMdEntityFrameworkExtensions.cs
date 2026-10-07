using AuthMd.AspNetCore.EntityFrameworkCore;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace AuthMd.AspNetCore;

/// <summary>
/// Wires the EF Core default stores.
/// </summary>
public static class AuthMdEntityFrameworkExtensions
{
    /// <summary>
    /// Stores auth.md state in <typeparamref name="TContext"/>. The context's model must include the
    /// auth.md entities: call <see cref="AddAuthMd{TUser}(ModelBuilder)"/> from <c>OnModelCreating</c>.
    /// </summary>
    public static AuthMdBuilder AddEntityFrameworkStores<TContext>(this AuthMdBuilder builder)
        where TContext : DbContext
    {
        ArgumentNullException.ThrowIfNull(builder);

        builder.Services.TryAddScoped<IClaimCeremonyStore, EntityFrameworkClaimCeremonyStore<TContext>>();
        builder.Services.TryAddScoped<IAgentRegistrationStore, EntityFrameworkAgentRegistrationStore<TContext>>();
        builder.Services.AddOpenIddict().AddCore(core => core.UseEntityFrameworkCore().UseDbContext<TContext>());
        return builder;
    }

    /// <summary>
    /// Adds the auth.md entities to the model, foreign-keyed to the <typeparamref name="TUser"/> table.
    /// </summary>
    /// <typeparam name="TUser">The ASP.NET Core Identity user entity. Its key must be a string.</typeparam>
    public static ModelBuilder AddAuthMd<TUser>(this ModelBuilder modelBuilder)
        where TUser : IdentityUser<string>
    {
        ArgumentNullException.ThrowIfNull(modelBuilder);

        // OpenIddict's own entities: the tokens it issues are stored so they can be revoked.
        modelBuilder.UseOpenIddict();

        modelBuilder.Entity<ClaimCeremony>(ceremony =>
        {
            ceremony.ToTable("AuthMdClaimCeremonies");
            ceremony.HasKey(c => c.Id);
            ceremony.HasIndex(c => c.ClaimTokenHash).IsUnique();
            ceremony.HasIndex(c => c.ClaimAttemptTokenHash).IsUnique();
            ceremony.Property(c => c.Status).HasConversion<string>();
            ceremony.HasOne<TUser>().WithMany().HasForeignKey(c => c.UserId);
        });

        modelBuilder.Entity<AgentRegistration>(registration =>
        {
            registration.ToTable("AuthMdAgentRegistrations");
            registration.HasKey(r => r.Id);
            registration.Property(r => r.Status).HasConversion<string>();
            registration.HasOne<TUser>().WithMany().HasForeignKey(r => r.UserId).IsRequired();
        });

        return modelBuilder;
    }
}
