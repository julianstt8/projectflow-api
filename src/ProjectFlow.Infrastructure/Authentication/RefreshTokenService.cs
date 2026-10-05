using System.Security.Cryptography;
using System.Text;
using Microsoft.Extensions.Options;
using ProjectFlow.Application.Abstractions;

namespace ProjectFlow.Infrastructure.Authentication;

/// <summary>
/// Refresh tokens are 512 random bits, so a fast SHA-256 is enough to store them safely
/// (unlike passwords, they cannot be guessed with a dictionary).
/// </summary>
internal sealed class RefreshTokenService(IOptions<JwtOptions> options) : IRefreshTokenService
{
    private const int TokenSizeInBytes = 64;

    public TimeSpan Lifetime => TimeSpan.FromDays(options.Value.RefreshTokenLifetimeDays);

    public GeneratedRefreshToken Generate()
    {
        var value = Base64UrlEncode(RandomNumberGenerator.GetBytes(TokenSizeInBytes));
        return new GeneratedRefreshToken(value, Hash(value));
    }

    public string Hash(string refreshToken) =>
        Convert.ToHexStringLower(SHA256.HashData(Encoding.UTF8.GetBytes(refreshToken)));

    private static string Base64UrlEncode(byte[] bytes) =>
        Convert.ToBase64String(bytes).TrimEnd('=').Replace('+', '-').Replace('/', '_');
}
