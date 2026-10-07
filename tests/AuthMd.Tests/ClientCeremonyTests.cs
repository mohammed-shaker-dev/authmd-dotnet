using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using System.Web;
using AuthMd.Client;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Time.Testing;

namespace AuthMd.Tests;

/// <summary>
/// <c>AuthMd.Client</c> driving <c>AuthMd.AspNetCore</c> over HTTP. The Service and the Agent share one
/// fake clock, which the test advances to let the Agent's polling delays elapse.
/// </summary>
public sealed class ClientCeremonyTests : IClassFixture<WebApplicationFactory<Program>>, IDisposable
{
    private readonly FakeTimeProvider _clock = new(DateTimeOffset.UtcNow);
    private readonly WebApplicationFactory<Program> _service;
    private readonly HttpRecorder _wire;
    private readonly AuthMdClient _agent;
    private readonly HttpClient _browser;

    public ClientCeremonyTests(WebApplicationFactory<Program> factory)
    {
        _service = factory.WithWebHostBuilder(host =>
            host.ConfigureTestServices(services => services.AddSingleton<TimeProvider>(_clock)));
        _wire = new HttpRecorder(_clock);
        _agent = new AuthMdClient(_service.CreateDefaultClient(_wire), _clock);
        _browser = _service.CreateClient();
    }

    public void Dispose()
    {
        _browser.Dispose();
        _service.Dispose();
    }

    [Fact]
    public async Task Agent_registers_on_behalf_of_a_user_and_calls_the_protected_api()
    {
        var service = await _agent.DiscoverAsync(new Uri("http://localhost/api/notes"));

        var pending = await _agent.RegisterAsync(service, "ada@example.com");
        var waiting = pending.WaitForUserAsync();
        var userId = await HumanApprovesAsync(pending, "ada@example.com");
        var session = await PumpAsync(waiting);

        using var api = _service.CreateDefaultClient(session.CreateHandler());
        var notes = await api.GetFromJsonAsync<JsonElement>("/api/notes");

        Assert.Matches(@"^\d{6}$", pending.UserCode);
        Assert.Equal(["api.read", "api.write"], pending.Scopes);
        Assert.Equal(userId, notes.String("owner"));
    }

    [Fact]
    public async Task Agent_polls_no_faster_than_the_service_asks()
    {
        var service = await _agent.DiscoverAsync(new Uri("http://localhost/"));
        var pending = await _agent.RegisterAsync(service, "grace@example.com");
        var waiting = pending.WaitForUserAsync();

        _clock.Advance(TimeSpan.FromSeconds(12));
        await Task.Delay(TimeSpan.FromMilliseconds(50));
        await HumanApprovesAsync(pending, "grace@example.com");
        await PumpAsync(waiting);

        var polls = _wire.Exchanges.Where(exchange => exchange.Path == "/oauth2/token").ToList();
        Assert.True(polls.Count >= 2);
        Assert.DoesNotContain(polls, poll => poll.Error == "slow_down");
        Assert.All(polls.Zip(polls.Skip(1)), pair => Assert.True(pair.Second.At - pair.First.At >= pending.Interval));
    }

    [Fact]
    public async Task Agent_told_to_slow_down_waits_longer_before_polling_again()
    {
        // A flaky network delivers the Agent's first poll twice, so the Service sees two polls at once.
        var wire = new HttpRecorder(_clock);
        var agent = new AuthMdClient(_service.CreateDefaultClient(wire, new DeliverFirstPollTwice()), _clock);
        var service = await agent.DiscoverAsync(new Uri("http://localhost/"));
        var pending = await agent.RegisterAsync(service, "linus@example.com");
        var waiting = pending.WaitForUserAsync();

        _clock.Advance(TimeSpan.FromSeconds(6));
        await Task.Delay(TimeSpan.FromMilliseconds(200));
        await HumanApprovesAsync(pending, "linus@example.com");
        await PumpAsync(waiting);

        var polls = wire.Exchanges.Where(exchange => exchange.Path == "/oauth2/token").ToList();
        Assert.Equal("slow_down", polls[0].Error);
        Assert.True(polls[1].At - polls[0].At >= pending.Interval + TimeSpan.FromSeconds(5));
        Assert.Equal(HttpStatusCode.OK, polls[^1].Status);
    }

    [Fact]
    public async Task Agent_is_told_to_register_again_when_the_user_never_approves()
    {
        var service = await _agent.DiscoverAsync(new Uri("http://localhost/"));
        var pending = await _agent.RegisterAsync(service, "alan@example.com");

        var failure = await Assert.ThrowsAsync<RegistrationRequiredException>(
            () => PumpAsync(pending.WaitForUserAsync(), step: TimeSpan.FromSeconds(5)));

        Assert.Equal("expired_token", failure.Error);
        Assert.True(_clock.GetUtcNow() >= pending.ExpiresAt);
    }

    [Fact]
    public async Task Agent_keeps_working_past_access_token_expiry_without_the_api_ever_refusing_it()
    {
        var session = await RegisteredSessionAsync("barbara@example.com");
        var wire = new HttpRecorder(_clock);
        using var api = _service.CreateDefaultClient(wire, session.CreateHandler());

        _clock.Advance(TimeSpan.FromHours(2));
        using var response = await api.GetAsync("/api/notes");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.DoesNotContain(wire.Exchanges, exchange => exchange.Status == HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task Agent_recovers_when_its_access_token_is_revoked_behind_its_back()
    {
        var session = await RegisteredSessionAsync("margaret@example.com");
        using var api = _service.CreateDefaultClient(session.CreateHandler());
        var accessToken = await session.GetAccessTokenAsync();
        using var revocation = await _browser.PostAsync("/oauth2/revoke", new FormUrlEncodedContent(
            new Dictionary<string, string> { ["token"] = accessToken }));

        using var response = await api.GetAsync("/api/notes");

        Assert.Equal(HttpStatusCode.OK, revocation.StatusCode);
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.NotEqual(accessToken, await session.GetAccessTokenAsync());
    }

    [Fact]
    public async Task Agent_is_told_to_register_again_once_its_identity_assertion_has_expired()
    {
        var session = await RegisteredSessionAsync("katherine@example.com");
        using var api = _service.CreateDefaultClient(session.CreateHandler());

        _clock.Advance(TimeSpan.FromDays(8));
        var failure = await Assert.ThrowsAsync<RegistrationRequiredException>(() => api.GetAsync("/api/notes"));

        Assert.Equal("invalid_grant", failure.Error);
    }

    [Fact]
    public async Task Agent_is_told_to_register_again_once_its_registration_is_revoked()
    {
        var session = await RegisteredSessionAsync("dorothy@example.com");
        using var api = _service.CreateDefaultClient(session.CreateHandler());
        using var revocation = await _browser.PostAsync($"/test/registrations/{session.RegistrationId}/revoke", content: null);

        var failure = await Assert.ThrowsAsync<RegistrationRequiredException>(() => api.GetAsync("/api/notes"));

        Assert.Equal(HttpStatusCode.NoContent, revocation.StatusCode);
        Assert.Equal("invalid_grant", failure.Error);
    }

    [Fact]
    public async Task Agent_can_revoke_its_own_access_token_and_carry_on_with_a_new_one()
    {
        var session = await RegisteredSessionAsync("jean@example.com");
        using var api = _service.CreateDefaultClient(session.CreateHandler());
        var revoked = await session.GetAccessTokenAsync();

        await session.RevokeAccessTokenAsync();
        using var withRevoked = await _browser.CallApiAsync(HttpMethod.Get, "/api/notes", revoked);
        using var throughSession = await api.GetAsync("/api/notes");

        Assert.Equal(HttpStatusCode.Unauthorized, withRevoked.StatusCode);
        Assert.Equal(HttpStatusCode.OK, throughSession.StatusCode);
    }

    [Fact]
    public async Task Refused_registration_surfaces_the_service_error()
    {
        var service = await _agent.DiscoverAsync(new Uri("http://localhost/"));

        var failure = await Assert.ThrowsAsync<AuthMdProtocolException>(
            () => _agent.RegisterAsync(service, "not-an-email"));

        Assert.Equal("invalid_request", failure.Error);
    }

    /// <summary>Runs the whole ceremony for a User with this email and returns the Agent's session.</summary>
    private async Task<AgentSession> RegisteredSessionAsync(string email)
    {
        var service = await _agent.DiscoverAsync(new Uri("http://localhost/"));
        var pending = await _agent.RegisterAsync(service, email);
        await HumanApprovesAsync(pending, email);
        return await PumpAsync(pending.WaitForUserAsync());
    }

    /// <summary>The human step: sign in to the Service and type the code the Agent showed them.</summary>
    private async Task<string> HumanApprovesAsync(PendingRegistration pending, string email)
    {
        var claimAttemptToken = HttpUtility.ParseQueryString(pending.VerificationUri.Query)["claim_attempt_token"]!;
        var userId = await _browser.SignInAsync(email);
        using var confirmation = await _browser.ConfirmClaimAsync(claimAttemptToken, pending.UserCode);
        confirmation.EnsureSuccessStatusCode();
        return userId;
    }

    /// <summary>Lets fake time pass, a step at a time, until the Agent's task finishes.</summary>
    private async Task<T> PumpAsync<T>(Task<T> task, TimeSpan? step = null)
    {
        while (!task.IsCompleted)
        {
            _clock.Advance(step ?? TimeSpan.FromSeconds(1));
            await Task.Delay(TimeSpan.FromMilliseconds(5));
        }

        return await task;
    }
}

/// <summary>A network fault: the first request to the token endpoint reaches the Service twice.</summary>
internal sealed class DeliverFirstPollTwice : DelegatingHandler
{
    private bool _done;

    protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
    {
        if (_done || request.RequestUri!.AbsolutePath != "/oauth2/token")
        {
            return await base.SendAsync(request, cancellationToken);
        }

        _done = true;
        var form = await request.Content!.ReadAsStringAsync(cancellationToken);
        using var duplicate = new HttpRequestMessage(HttpMethod.Post, request.RequestUri)
        {
            Content = new StringContent(form, System.Text.Encoding.UTF8, "application/x-www-form-urlencoded"),
        };
        using var lost = await base.SendAsync(duplicate, cancellationToken);
        return await base.SendAsync(request, cancellationToken);
    }
}

/// <summary>Records what crossed the wire between the Agent and the Service, and when.</summary>
internal sealed class HttpRecorder(TimeProvider clock) : DelegatingHandler
{
    public List<(DateTimeOffset At, string Path, HttpStatusCode Status, string? Error)> Exchanges { get; } = [];

    protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
    {
        var at = clock.GetUtcNow();
        var response = await base.SendAsync(request, cancellationToken);

        string? error = null;
        if (!response.IsSuccessStatusCode && response.Content.Headers.ContentType?.MediaType == "application/json")
        {
            // Buffered and read as a string, so the Agent can still read the body afterwards.
            await response.Content.LoadIntoBufferAsync(cancellationToken);
            var body = JsonSerializer.Deserialize<JsonElement>(await response.Content.ReadAsStringAsync(cancellationToken));
            error = body.TryGetProperty("error", out var code) ? code.GetString() : null;
        }

        lock (Exchanges)
        {
            Exchanges.Add((at, request.RequestUri!.AbsolutePath, response.StatusCode, error));
        }

        return response;
    }
}
