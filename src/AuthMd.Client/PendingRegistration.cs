namespace AuthMd.Client;

/// <summary>
/// A registration waiting for the User: the Claim Ceremony as the Agent sees it. Show the User
/// <see cref="VerificationUri"/> and <see cref="UserCode"/>, then await <see cref="WaitForUserAsync"/>.
/// </summary>
public sealed class PendingRegistration
{
    private readonly ServiceConnection _connection;
    private readonly string _claimToken;

    internal PendingRegistration(
        ServiceConnection connection,
        string claimToken,
        string registrationId,
        string userCode,
        Uri verificationUri,
        DateTimeOffset expiresAt,
        TimeSpan interval,
        IReadOnlyList<string> scopes)
    {
        _connection = connection;
        _claimToken = claimToken;
        RegistrationId = registrationId;
        UserCode = userCode;
        VerificationUri = verificationUri;
        ExpiresAt = expiresAt;
        Interval = interval;
        Scopes = scopes;
    }

    /// <summary>The Service's id for this registration.</summary>
    public string RegistrationId { get; }

    /// <summary>The code the User types into the Service's own page after signing in. Never send it anywhere else.</summary>
    public string UserCode { get; }

    /// <summary>The page on the Service where the User signs in and enters <see cref="UserCode"/>.</summary>
    public Uri VerificationUri { get; }

    /// <summary>When the User's chance to approve runs out.</summary>
    public DateTimeOffset ExpiresAt { get; }

    /// <summary>How long the Service asks the Agent to wait between polls.</summary>
    public TimeSpan Interval { get; }

    /// <summary>The Scopes the Agent will hold once the User approves.</summary>
    public IReadOnlyList<string> Scopes { get; }

    /// <summary>
    /// Polls the Service, at the pace it asks for, until the User has approved.
    /// </summary>
    /// <exception cref="RegistrationRequiredException">The User did not approve in time. Register again.</exception>
    public async Task<AgentSession> WaitForUserAsync(CancellationToken cancellationToken = default)
    {
        var interval = Interval;
        while (true)
        {
            await Task.Delay(interval, _connection.Clock, cancellationToken);

            var response = await _connection.RequestTokenAsync(
                new Dictionary<string, string>
                {
                    ["grant_type"] = "urn:workos:agent-auth:grant-type:claim",
                    ["claim_token"] = _claimToken,
                },
                cancellationToken);

            switch (response.Error)
            {
                case null:
                    return AgentSession.FromTokenResponse(_connection, RegistrationId, response);
                case "authorization_pending":
                    continue;
                case "slow_down":
                    interval += TimeSpan.FromSeconds(5);
                    continue;
                case "expired_token" or "invalid_grant":
                    throw new RegistrationRequiredException(response.Error, response.ErrorDescription);
                default:
                    throw response.ToException();
            }
        }
    }
}
