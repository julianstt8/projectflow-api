using Microsoft.AspNetCore.Identity;
using ProjectFlow.Application.Abstractions;

namespace ProjectFlow.Infrastructure.Authentication;

/// <summary>PBKDF2 hashing from ASP.NET Core Identity (salted, versioned format), without the rest of Identity.</summary>
internal sealed class PasswordHasher : IPasswordHasher
{
    private readonly PasswordHasher<PasswordOwner> _hasher = new();

    public string Hash(string password) => _hasher.HashPassword(PasswordOwner.Instance, password);

    public bool Verify(string password, string passwordHash) =>
        _hasher.VerifyHashedPassword(PasswordOwner.Instance, passwordHash, password) != PasswordVerificationResult.Failed;

    // Identity's hasher is generic over the user type but does not use it.
    private sealed class PasswordOwner
    {
        public static readonly PasswordOwner Instance = new();
    }
}
