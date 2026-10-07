using Microsoft.Extensions.Options;
using OpenIddict.Abstractions;
using OpenIddict.Validation;
using static OpenIddict.Validation.OpenIddictValidationEvents;

namespace AuthMd.AspNetCore;

/// <summary>
/// Adds <c>resource_metadata</c> to the <c>WWW-Authenticate</c> challenge, so an Agent turned away by
/// the API can start the two-hop discovery walk (RFC 9728).
/// </summary>
internal sealed class PointChallengeAtResourceMetadata(IOptions<AuthMdOptions> options)
    : IOpenIddictValidationHandler<ProcessChallengeContext>
{
    public static OpenIddictValidationHandlerDescriptor Descriptor { get; } =
        OpenIddictValidationHandlerDescriptor.CreateBuilder<ProcessChallengeContext>()
            .UseSingletonHandler<PointChallengeAtResourceMetadata>()
            // After OpenIddict has decided the error, before the host writes the header.
            .SetOrder(int.MinValue + 1_000_000)
            .Build();

    public ValueTask HandleAsync(ProcessChallengeContext context)
    {
        var urls = ServiceUrls.For(context.BaseUri!, options.Value);
        context.Response["resource_metadata"] = urls.ProtectedResourceMetadata;

        // A valid token that authorization turned away lacks a Scope: say so in RFC 6750's vocabulary.
        if (context.Response.Error == OpenIddictConstants.Errors.InsufficientAccess)
        {
            context.Response.Error = OpenIddictConstants.Errors.InsufficientScope;
            context.Response.ErrorDescription = "The access token does not carry a scope this endpoint requires.";
            context.Response.ErrorUri = null;
        }

        return default;
    }
}
