namespace AuthMd.Client;

/// <summary>
/// The Service answered with a protocol error.
/// </summary>
public class AuthMdProtocolException(string error, string? errorDescription)
    : Exception(errorDescription is null ? error : $"{error}: {errorDescription}")
{
    /// <summary>The error code, e.g. <c>invalid_request</c>.</summary>
    public string Error { get; } = error;

    /// <summary>The Service's human-readable explanation, if it gave one.</summary>
    public string? ErrorDescription { get; } = errorDescription;
}
