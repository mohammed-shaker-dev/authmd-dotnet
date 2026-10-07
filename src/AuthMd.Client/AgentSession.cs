using System.Net;
using System.Net.Http.Headers;

namespace AuthMd.Client;

/// <summary>
/// The Agent's standing with a Service after the User approved: an Identity Assertion and the
/// Access Tokens obtained from it. There is no refresh token: when an Access Token runs out, the
/// session exchanges the Identity Assertion for a new one.
/// </summary>
public sealed class AgentSession
{
    /// <summary>An Access Token this close to expiry is replaced before use rather than risked.</summary>
    private static readonly TimeSpan ExpiryMargin = TimeSpan.FromSeconds(30);

    private readonly ServiceConnection _connection;
    private readonly SemaphoreSlim _exchanging = new(1, 1);
    private string _accessToken;
    private DateTimeOffset _accessTokenExpires;

    private AgentSession(ServiceConnection connection, string registrationId, string identityAssertion, TokenResponse tokens)
    {
        _connection = connection;
        RegistrationId = registrationId;
        IdentityAssertion = identityAssertion;
        (_accessToken, _accessTokenExpires) = ReadAccessToken(connection, tokens);
    }

    /// <summary>The Service's id for the Agent Registration behind this session.</summary>
    public string RegistrationId { get; }

    /// <summary>
    /// The Service-signed JWT representing the Agent Registration. Treat it as a credential.
    /// </summary>
    public string IdentityAssertion { get; }

    /// <summary>
    /// A current Access Token, obtained from the Identity Assertion if the last one has run out.
    /// </summary>
    /// <exception cref="RegistrationRequiredException">
    /// The Identity Assertion has expired or the registration was revoked. Register again.
    /// </exception>
    public async Task<string> GetAccessTokenAsync(CancellationToken cancellationToken = default)
    {
        await _exchanging.WaitAsync(cancellationToken);
        try
        {
            if (_connection.Clock.GetUtcNow() >= _accessTokenExpires - ExpiryMargin)
            {
                await ExchangeAsync(cancellationToken);
            }

            return _accessToken;
        }
        finally
        {
            _exchanging.Release();
        }
    }

    /// <summary>
    /// Revokes the current Access Token, e.g. when a task is finished. The registration is untouched:
    /// the next call obtains a new token from the Identity Assertion.
    /// </summary>
    /// <exception cref="NotSupportedException">The Service advertises no revocation endpoint.</exception>
    public async Task RevokeAccessTokenAsync(CancellationToken cancellationToken = default)
    {
        var endpoint = _connection.Service.RevocationEndpoint
            ?? throw new NotSupportedException("The service advertises no revocation endpoint.");

        await _exchanging.WaitAsync(cancellationToken);
        try
        {
            using var response = await _connection.Http.PostAsync(
                endpoint,
                new FormUrlEncodedContent(new Dictionary<string, string>
                {
                    ["token"] = _accessToken,
                    ["token_type_hint"] = "access_token",
                }),
                cancellationToken);
            response.EnsureSuccessStatusCode();
            _accessTokenExpires = _connection.Clock.GetUtcNow();
        }
        finally
        {
            _exchanging.Release();
        }
    }

    /// <summary>
    /// A handler that sends the Agent's Access Token as <c>Authorization: Bearer</c> on every request,
    /// renewing it when it expires or the Service stops accepting it. Put it in the pipeline of the
    /// <see cref="HttpClient"/> that calls the Service's API.
    /// </summary>
    public DelegatingHandler CreateHandler() => new BearerHandler(this);

    internal static AgentSession FromTokenResponse(ServiceConnection connection, string registrationId, TokenResponse response) =>
        new(
            connection,
            registrationId,
            response.String("identity_assertion") ?? throw new AuthMdProtocolException("invalid_response", "The service returned no identity_assertion."),
            response);

    /// <summary>Replaces <paramref name="rejected"/>, unless another caller already has.</summary>
    private async Task<string> ReplaceAccessTokenAsync(string rejected, CancellationToken cancellationToken)
    {
        await _exchanging.WaitAsync(cancellationToken);
        try
        {
            if (_accessToken == rejected)
            {
                await ExchangeAsync(cancellationToken);
            }

            return _accessToken;
        }
        finally
        {
            _exchanging.Release();
        }
    }

    /// <summary>The RFC 7523 exchange: Identity Assertion in, Access Token out.</summary>
    private async Task ExchangeAsync(CancellationToken cancellationToken)
    {
        var response = await _connection.RequestTokenAsync(
            new Dictionary<string, string>
            {
                ["grant_type"] = "urn:ietf:params:oauth:grant-type:jwt-bearer",
                ["assertion"] = IdentityAssertion,
                ["resource"] = _connection.Service.Resource.ToString(),
            },
            cancellationToken);

        (_accessToken, _accessTokenExpires) = response.Error switch
        {
            null => ReadAccessToken(_connection, response),
            "invalid_grant" => throw new RegistrationRequiredException(response.Error, response.ErrorDescription),
            _ => throw response.ToException(),
        };
    }

    private static (string Token, DateTimeOffset Expires) ReadAccessToken(ServiceConnection connection, TokenResponse response)
    {
        var token = response.String("access_token")
            ?? throw new AuthMdProtocolException("invalid_response", "The service returned no access_token.");
        return (token, connection.Clock.GetUtcNow() + TimeSpan.FromSeconds(response.Number("expires_in") ?? 0));
    }

    private sealed class BearerHandler(AgentSession session) : DelegatingHandler
    {
        protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            var accessToken = await session.GetAccessTokenAsync(cancellationToken);
            request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", accessToken);
            var response = await base.SendAsync(request, cancellationToken);

            // A token the Service refuses was revoked. Try once more with a new one. Only requests
            // without a body are repeated: a body may not be readable twice.
            if (response.StatusCode != HttpStatusCode.Unauthorized || request.Content is not null)
            {
                return response;
            }

            var replacement = await session.ReplaceAccessTokenAsync(accessToken, cancellationToken);
            response.Dispose();

            using var retry = new HttpRequestMessage(request.Method, request.RequestUri) { Version = request.Version };
            foreach (var header in request.Headers)
            {
                retry.Headers.TryAddWithoutValidation(header.Key, header.Value);
            }
            retry.Headers.Authorization = new AuthenticationHeaderValue("Bearer", replacement);
            return await base.SendAsync(retry, cancellationToken);
        }
    }
}
