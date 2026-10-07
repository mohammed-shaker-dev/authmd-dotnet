using OpenIddict.Abstractions;
using OpenIddict.Validation;
using static OpenIddict.Validation.OpenIddictValidationEvents;

namespace AuthMd.AspNetCore;

/// <summary>
/// Makes the Agent Registration the unit of revocation at the API: an otherwise valid Access Token is
/// refused once the registration it derives from is no longer active.
/// </summary>
internal sealed class RejectTokensOfRevokedRegistrations(IAgentRegistrationStore registrations)
    : IOpenIddictValidationHandler<ProcessAuthenticationContext>
{
    public static OpenIddictValidationHandlerDescriptor Descriptor { get; } =
        OpenIddictValidationHandlerDescriptor.CreateBuilder<ProcessAuthenticationContext>()
            .UseScopedHandler<RejectTokensOfRevokedRegistrations>()
            // After OpenIddict has validated the token itself.
            .SetOrder(int.MaxValue - 100_000)
            .Build();

    public async ValueTask HandleAsync(ProcessAuthenticationContext context)
    {
        // Tokens that do not come from an Agent Registration are not this handler's business.
        var registrationId = context.AccessTokenPrincipal?.GetClaim(IdentityAssertions.RegistrationIdClaim);
        if (registrationId is null)
        {
            return;
        }

        var registration = await registrations.FindByIdAsync(registrationId, context.CancellationToken);
        if (registration is not { Status: AgentRegistrationStatus.Active })
        {
            context.Reject(
                error: OpenIddictConstants.Errors.InvalidToken,
                description: "The agent registration this token derives from has been revoked.");
        }
    }
}
