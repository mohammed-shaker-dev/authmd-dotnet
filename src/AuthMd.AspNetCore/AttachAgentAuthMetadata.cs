using System.Text.Json.Nodes;
using Microsoft.Extensions.Options;
using OpenIddict.Server;
using static OpenIddict.Server.OpenIddictServerEvents;

namespace AuthMd.AspNetCore;

/// <summary>
/// Adds the auth.md profile to the authorization-server metadata OpenIddict serves: the restated
/// Protected Resource Metadata and the <c>agent_auth</c> block.
/// </summary>
internal sealed class AttachAgentAuthMetadata(IOptions<AuthMdOptions> options)
    : IOpenIddictServerHandler<HandleConfigurationRequestContext>
{
    public static OpenIddictServerHandlerDescriptor Descriptor { get; } =
        OpenIddictServerHandlerDescriptor.CreateBuilder<HandleConfigurationRequestContext>()
            .UseSingletonHandler<AttachAgentAuthMetadata>()
            .SetOrder(int.MaxValue - 100_000)
            .Build();

    public ValueTask HandleAsync(HandleConfigurationRequestContext context)
    {
        var urls = ServiceUrls.For(context.BaseUri!, options.Value);

        foreach (var (name, value) in DiscoveryDocuments.ProtectedResourceMetadata(urls, options.Value))
        {
            // scopes_supported is OpenIddict's to write, from the Scopes registered with the server.
            if (name != "scopes_supported")
            {
                context.Metadata[name] = value!.DeepClone();
            }
        }

        context.Metadata["agent_auth"] = new JsonObject
        {
            ["skill"] = urls.Skill,
            ["identity_endpoint"] = urls.IdentityEndpoint,
            ["claim_endpoint"] = urls.ClaimEndpoint,
            ["identity_types_supported"] = new JsonArray("service_auth"),
        };

        return default;
    }
}
