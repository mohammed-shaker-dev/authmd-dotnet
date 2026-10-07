using Microsoft.Extensions.DependencyInjection;

namespace AuthMd.AspNetCore;

/// <summary>
/// Returned by <see cref="AuthMdServiceCollectionExtensions.AddAuthMd{TUser}"/> to configure the auth.md services further.
/// </summary>
public sealed class AuthMdBuilder
{
    internal AuthMdBuilder(IServiceCollection services) => Services = services;

    /// <summary>
    /// The services being configured.
    /// </summary>
    public IServiceCollection Services { get; }
}
