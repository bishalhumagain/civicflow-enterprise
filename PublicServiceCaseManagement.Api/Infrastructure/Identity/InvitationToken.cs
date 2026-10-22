using System.Security.Cryptography;
using System.Text;
using Microsoft.AspNetCore.WebUtilities;

namespace PublicServiceCaseManagement.Api.Infrastructure.Identity;

/// <summary>
/// Creates and hashes the bearer token carried by an account invitation.
/// Tokens are 256-bit random values encoded for use in a URL. Only the
/// SHA-256 hash is persisted, so the raw token exists only in the email that
/// was delivered to the invitee.
/// </summary>
public static class InvitationToken
{
    private const int TokenByteLength = 32;

    public static string Create()
    {
        var bytes = RandomNumberGenerator.GetBytes(TokenByteLength);
        return WebEncoders.Base64UrlEncode(bytes);
    }

    public static string Hash(string token)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(token);

        var digest = SHA256.HashData(Encoding.UTF8.GetBytes(token));

        return Convert.ToHexString(digest);
    }
}
