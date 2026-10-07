using System.Security.Claims;
using AuthMd.AspNetCore;
using AuthMd.TestHost;
using Microsoft.AspNetCore.Identity;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;

var builder = WebApplication.CreateBuilder(args);

// One open in-memory SQLite connection per host: the database lives as long as the host does.
var connection = new SqliteConnection("DataSource=:memory:");
connection.Open();
builder.Services.AddSingleton(connection);
builder.Services.AddDbContext<TestHostDbContext>(options => options.UseSqlite(connection));

builder.Services
    .AddIdentity<IdentityUser, IdentityRole>()
    .AddEntityFrameworkStores<TestHostDbContext>();

builder.Services
    .AddAuthMd<IdentityUser>(options =>
    {
        options.ResourceName = "Test Service";
        options.Scopes.Add("api.read");
        options.Scopes.Add("api.write");
    })
    .AddEntityFrameworkStores<TestHostDbContext>();

// The application owns the OAuth keys. A test host gets throwaway ones and plain-HTTP requests.
builder.Services.AddOpenIddict().AddServer(server =>
{
    server.AddEphemeralEncryptionKey().AddEphemeralSigningKey();
    server.UseAspNetCore().DisableTransportSecurityRequirement();
});

var app = builder.Build();

using (var scope = app.Services.CreateScope())
{
    scope.ServiceProvider.GetRequiredService<TestHostDbContext>().Database.EnsureCreated();
}

app.MapAuthMd();

// The Service's own API, open to Agents holding the right Scope.
app.MapGet("/api/notes", (ClaimsPrincipal agent) => Results.Json(new { owner = agent.FindFirstValue("sub") }))
    .RequireAgentScope("api.read");
app.MapPost("/api/notes", () => Results.NoContent())
    .RequireAgentScope("api.write");

// Stands in for wherever the Service lets a User or an admin cut an Agent off.
app.MapPost("/test/registrations/{id}/revoke", async (string id, AgentRegistrationManager registrations, CancellationToken cancellationToken) =>
    await registrations.RevokeAsync(id, cancellationToken) ? Results.NoContent() : Results.NotFound());

// Stands in for the Service's own Identity login page: seeds the User if needed and signs them in.
app.MapPost("/test/sign-in", async (string email, UserManager<IdentityUser> users, SignInManager<IdentityUser> signIn) =>
{
    var user = await users.FindByEmailAsync(email);
    if (user is null)
    {
        user = new IdentityUser { UserName = email, Email = email, EmailConfirmed = true };
        await users.CreateAsync(user);
    }

    await signIn.SignInAsync(user, isPersistent: false);
    return Results.Json(new { id = user.Id });
});

app.Run();
