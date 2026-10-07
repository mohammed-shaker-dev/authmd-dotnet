using System.Security.Claims;
using Microsoft.AspNetCore.Identity;

namespace AuthMd.AspNetCore;

/// <summary>The User behind the current request, as ASP.NET Core Identity knows them.</summary>
internal sealed record SignedInUser(string Id, string? Email);

internal interface ISignedInUserAccessor
{
    /// <summary>Resolves the User the principal is signed in as, or <see langword="null"/> if there is none.</summary>
    Task<SignedInUser?> FindAsync(ClaimsPrincipal principal);

    bool IsSameEmail(string? left, string? right);
}

internal sealed class IdentitySignedInUserAccessor<TUser>(UserManager<TUser> users) : ISignedInUserAccessor
    where TUser : class
{
    public async Task<SignedInUser?> FindAsync(ClaimsPrincipal principal)
    {
        var user = await users.GetUserAsync(principal);
        return user is null
            ? null
            : new SignedInUser(await users.GetUserIdAsync(user), await users.GetEmailAsync(user));
    }

    public bool IsSameEmail(string? left, string? right) =>
        left is not null && right is not null && users.NormalizeEmail(left) == users.NormalizeEmail(right);
}
