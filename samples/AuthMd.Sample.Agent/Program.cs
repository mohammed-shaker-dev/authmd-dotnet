using System.Net.Http.Json;
using System.Text.Json;
using AuthMd.Client;

// An "agent" in miniature: it knows a service's URL and its user's email, and nothing else.
//   dotnet run --project samples/AuthMd.Sample.Agent [serviceUrl] [email]
var serviceUrl = new Uri(args.ElementAtOrDefault(0) ?? "http://localhost:5125/");
var email = args.ElementAtOrDefault(1) ?? "demo@example.com";

using var http = new HttpClient();
var agent = new AuthMdClient(http);

try
{
    // 1. Discover: read the service's auth.md documents.
    Console.WriteLine($"Discovering {serviceUrl} ...");
    var service = await agent.DiscoverAsync(serviceUrl);
    Console.WriteLine($"  Service:  {service.ResourceName ?? service.Resource.ToString()}");
    Console.WriteLine($"  Scopes:   {string.Join(", ", service.ScopesSupported)}");
    Console.WriteLine($"  Methods:  {string.Join(", ", service.IdentityTypesSupported)}");

    if (!service.SupportsServiceAuth)
    {
        Console.Error.WriteLine("This service does not accept service_auth registrations.");
        return 1;
    }

    // 2. Register on the user's behalf. Nothing is granted yet.
    Console.WriteLine();
    Console.WriteLine($"Registering on behalf of {email} ...");
    var pending = await agent.RegisterAsync(service, email);

    // 3. Hand off to the human. The code goes from the agent to the user, and from the user into
    //    the service's own page, never back to the agent.
    Console.WriteLine();
    Console.WriteLine("  ┌──────────────────────────────────────────────────────────────┐");
    Console.WriteLine("  │  To let this agent act for you, open the link below, sign    │");
    Console.WriteLine("  │  in, and enter this code:                                    │");
    Console.WriteLine("  │                                                              │");
    Console.WriteLine($"  │                         {pending.UserCode}                               │");
    Console.WriteLine("  └──────────────────────────────────────────────────────────────┘");
    Console.WriteLine($"  {pending.VerificationUri}");
    Console.WriteLine();
    Console.WriteLine($"Waiting for approval (expires {pending.ExpiresAt.ToLocalTime():t}) ...");

    // 4. Poll until the user has approved.
    var session = await pending.WaitForUserAsync();
    Console.WriteLine($"Approved. Registration {session.RegistrationId}.");

    // 5. Call the service's API. The handler attaches the access token and renews it as needed.
    var handler = session.CreateHandler();
    handler.InnerHandler = new HttpClientHandler();
    using var api = new HttpClient(handler) { BaseAddress = service.Resource };

    Console.WriteLine();
    Console.WriteLine("Reading the user's notes ...");
    await PrintNotesAsync(api);

    Console.WriteLine();
    Console.WriteLine("Adding a note ...");
    using var added = await api.PostAsJsonAsync("api/notes", new { text = $"Added by your agent at {DateTime.Now:t}" });
    added.EnsureSuccessStatusCode();
    await PrintNotesAsync(api);

    // 6. Clean up: the task is done, so give the access token back.
    await session.RevokeAccessTokenAsync();
    Console.WriteLine();
    Console.WriteLine("Done. Access token revoked; the registration stays until the user or the service revokes it.");
    return 0;
}
catch (RegistrationRequiredException exception)
{
    Console.Error.WriteLine($"The registration is no longer usable ({exception.Error}). Run the agent again to re-register.");
    return 1;
}
catch (AuthMdProtocolException exception)
{
    Console.Error.WriteLine($"The service refused: {exception.Message}");
    return 1;
}
catch (AuthMdDiscoveryException exception)
{
    Console.Error.WriteLine($"Could not discover the service. Is it running? {exception.Message}");
    return 1;
}

static async Task PrintNotesAsync(HttpClient api)
{
    var body = await api.GetFromJsonAsync<JsonElement>("api/notes");
    foreach (var note in body.GetProperty("notes").EnumerateArray())
    {
        Console.WriteLine($"  - {note.GetString()}");
    }
}
