namespace AuthMd.Client;

/// <summary>
/// The Service's discovery documents could not be fetched or understood: the host is not an
/// auth.md Service, or it is unreachable.
/// </summary>
public sealed class AuthMdDiscoveryException(string message, Exception? innerException = null)
    : Exception(message, innerException);
