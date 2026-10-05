using System.ComponentModel.DataAnnotations;
using System.Text;
using Microsoft.IdentityModel.Tokens;

namespace ProjectFlow.Infrastructure.Authentication;

/// <summary>Settings of the <c>Jwt</c> configuration section.</summary>
public sealed class JwtOptions
{
    public const string SectionName = "Jwt";

    /// <summary>HS256 needs at least 256 bits of key material.</summary>
    public const int MinimumSigningKeyLength = 32;

    [Required]
    public string Issuer { get; init; } = string.Empty;

    [Required]
    public string Audience { get; init; } = string.Empty;

    /// <summary>Secret used to sign tokens. Comes from user-secrets or environment variables, never from the repository.</summary>
    [Required]
    [MinLength(MinimumSigningKeyLength)]
    public string SigningKey { get; init; } = string.Empty;

    [Range(1, 60)]
    public int AccessTokenLifetimeMinutes { get; init; } = 15;

    /// <summary>Key used both to sign tokens and to validate them.</summary>
    public SymmetricSecurityKey GetSigningKey() => new(Encoding.UTF8.GetBytes(SigningKey));
}
