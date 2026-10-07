using AuthMd.AspNetCore;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Identity.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore;

namespace AuthMd.Sample.Service;

public class SampleDbContext(DbContextOptions<SampleDbContext> options)
    : IdentityDbContext<IdentityUser>(options)
{
    protected override void OnModelCreating(ModelBuilder builder)
    {
        base.OnModelCreating(builder);

        // The auth.md tables, foreign-keyed to AspNetUsers.
        builder.AddAuthMd<IdentityUser>();
    }
}
