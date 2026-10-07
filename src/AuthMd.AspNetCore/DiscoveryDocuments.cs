using System.Text.Json.Nodes;

namespace AuthMd.AspNetCore;

internal static class DiscoveryDocuments
{
    /// <summary>The RFC 9728 Protected Resource Metadata.</summary>
    public static JsonObject ProtectedResourceMetadata(ServiceUrls urls, AuthMdOptions options)
    {
        var metadata = new JsonObject { ["resource"] = urls.Resource };
        if (options.ResourceName is not null)
        {
            metadata["resource_name"] = options.ResourceName;
        }
        if (options.ResourceLogoUri is not null)
        {
            metadata["resource_logo_uri"] = options.ResourceLogoUri.ToString();
        }
        metadata["authorization_servers"] = new JsonArray(urls.Resource);
        metadata["scopes_supported"] = new JsonArray([.. options.Scopes.Select(scope => JsonValue.Create(scope))]);
        metadata["bearer_methods_supported"] = new JsonArray("header");
        return metadata;
    }
}
