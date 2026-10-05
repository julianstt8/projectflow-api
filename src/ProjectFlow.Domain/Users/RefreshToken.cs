using ProjectFlow.Domain.Common;

namespace ProjectFlow.Domain.Users;

/// <summary>
/// A long-lived credential that can be exchanged once for a new access token. Every login starts a
/// <see cref="FamilyId">family</see>; each refresh revokes the current token and issues the next one in the
/// same family. Presenting a revoked token means it was stolen, so the whole family must be revoked.
/// Only the hash of the token is stored.
/// </summary>
public sealed class RefreshToken : Entity
{
    private RefreshToken(
        Guid id,
        Guid userId,
        Guid familyId,
        string tokenHash,
        DateTimeOffset createdAt,
        DateTimeOffset expiresAt)
        : base(id)
    {
        UserId = userId;
        FamilyId = familyId;
        TokenHash = tokenHash;
        CreatedAt = createdAt;
        ExpiresAt = expiresAt;
    }

    public Guid UserId { get; }

    public Guid FamilyId { get; }

    public string TokenHash { get; }

    public DateTimeOffset CreatedAt { get; }

    public DateTimeOffset ExpiresAt { get; }

    public DateTimeOffset? RevokedAt { get; private set; }

    /// <summary>The token issued when this one was rotated.</summary>
    public Guid? ReplacedById { get; private set; }

    public bool IsRevoked => RevokedAt is not null;

    public bool IsExpired(DateTimeOffset now) => now >= ExpiresAt;

    /// <summary>Starts a new family, e.g. on login.</summary>
    public static RefreshToken Issue(Guid userId, string tokenHash, DateTimeOffset now, TimeSpan lifetime) =>
        Create(userId, Guid.CreateVersion7(now), tokenHash, now, lifetime);

    /// <summary>Revokes this token and issues the next one of the same family.</summary>
    public Result<RefreshToken> Rotate(string newTokenHash, DateTimeOffset now, TimeSpan lifetime)
    {
        if (IsRevoked)
        {
            return RefreshTokenErrors.Revoked;
        }

        if (IsExpired(now))
        {
            return RefreshTokenErrors.Expired;
        }

        var next = Create(UserId, FamilyId, newTokenHash, now, lifetime);
        RevokedAt = now;
        ReplacedById = next.Id;

        return next;
    }

    public void Revoke(DateTimeOffset now) => RevokedAt ??= now;

    private static RefreshToken Create(Guid userId, Guid familyId, string tokenHash, DateTimeOffset now, TimeSpan lifetime) =>
        new(Guid.CreateVersion7(now), userId, familyId, tokenHash, now, now + lifetime);
}

public static class RefreshTokenErrors
{
    public static readonly Error Revoked =
        Error.Unauthorized("RefreshToken.Revoked", "The refresh token has been revoked.");

    public static readonly Error Expired =
        Error.Unauthorized("RefreshToken.Expired", "The refresh token has expired.");
}
