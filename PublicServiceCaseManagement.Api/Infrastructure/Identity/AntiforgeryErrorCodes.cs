namespace PublicServiceCaseManagement.Api.Infrastructure.Identity;

/// <summary>
/// Stable, machine-readable error codes returned alongside anti-forgery
/// failures. The frontend retry logic keys off these values rather than the
/// human-readable message, so wording changes never break token recovery.
/// </summary>
public static class AntiforgeryErrorCodes
{
    /// <summary>
    /// Emitted when an unsafe API request arrives without a valid anti-forgery
    /// token/cookie pair — for example after a session switch leaves the client
    /// with a stale cached token.
    /// </summary>
    public const string InvalidToken = "antiforgery_invalid";
}
