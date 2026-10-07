using System.Security.Claims;
using AuthMd.AspNetCore;
using AuthMd.Sample.Service;
using Microsoft.AspNetCore.Identity;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;

var builder = WebApplication.CreateBuilder(args);

// An in-memory database that lives as long as the process: every run of the sample starts clean.
var connection = new SqliteConnection("DataSource=:memory:");
connection.Open();
builder.Services.AddSingleton(connection);
builder.Services.AddDbContext<SampleDbContext>(options => options.UseSqlite(connection));

// The app's ordinary ASP.NET Core Identity setup. Nothing here knows about agents.
builder.Services
    .AddIdentity<IdentityUser, IdentityRole>()
    .AddEntityFrameworkStores<SampleDbContext>();
builder.Services.AddRazorPages();
builder.Services.AddSingleton<NoteStore>();

// auth.md: agents can now register on behalf of this app's users.
builder.Services
    .AddAuthMd<IdentityUser>(options =>
    {
        options.ResourceName = "Sample Notes";
        options.Scopes.Add("notes.read");
        options.Scopes.Add("notes.write");
    })
    .AddEntityFrameworkStores<SampleDbContext>();

// The OAuth keys are the application's to provide. Throwaway keys are fine for a demo: every token
// dies when the process stops. A real deployment registers certificates here instead.
builder.Services.AddOpenIddict().AddServer(server =>
{
    server.AddEphemeralEncryptionKey().AddEphemeralSigningKey();
    if (builder.Environment.IsDevelopment())
    {
        server.UseAspNetCore().DisableTransportSecurityRequirement();
    }
});

var app = builder.Build();

using (var scope = app.Services.CreateScope())
{
    await scope.ServiceProvider.GetRequiredService<SampleDbContext>().Database.EnsureCreatedAsync();
    var users = scope.ServiceProvider.GetRequiredService<UserManager<IdentityUser>>();
    await users.CreateAsync(
        new IdentityUser { UserName = DemoUser.Email, Email = DemoUser.Email, EmailConfirmed = true },
        DemoUser.Password);
}

app.MapRazorPages();
app.MapAuthMd();

// The app's API. An agent's access token says which user it acts for and what it may do.
app.MapGet("/api/notes", (ClaimsPrincipal agent, NoteStore notes) =>
        Results.Json(new { notes = notes.For(agent.FindFirstValue("sub")!) }))
    .RequireAgentScope("notes.read");

app.MapPost("/api/notes", (NewNote note, ClaimsPrincipal agent, NoteStore notes) =>
    {
        notes.Add(agent.FindFirstValue("sub")!, note.Text);
        return Results.NoContent();
    })
    .RequireAgentScope("notes.write");

app.Run();

internal sealed record NewNote(string Text);
