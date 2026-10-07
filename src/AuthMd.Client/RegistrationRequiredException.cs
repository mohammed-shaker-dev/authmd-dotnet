namespace AuthMd.Client;

/// <summary>
/// The Agent no longer has, or never obtained, a usable registration: the Claim Ceremony expired, or
/// the Identity Assertion expired or was revoked. Register again, which needs the User's approval.
/// </summary>
public sealed class RegistrationRequiredException(string error, string? errorDescription)
    : AuthMdProtocolException(error, errorDescription);
