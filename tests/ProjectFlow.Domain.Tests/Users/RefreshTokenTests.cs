using ProjectFlow.Domain.Users;

namespace ProjectFlow.Domain.Tests.Users;

public class RefreshTokenTests
{
    private static readonly DateTimeOffset Now = new(2026, 10, 5, 12, 0, 0, TimeSpan.Zero);
    private static readonly TimeSpan Lifetime = TimeSpan.FromDays(7);
    private static readonly Guid UserId = Guid.NewGuid();

    [Fact]
    public void Issue_starts_an_active_token_in_a_new_family()
    {
        var token = RefreshToken.Issue(UserId, "hash-1", Now, Lifetime);
        var other = RefreshToken.Issue(UserId, "hash-2", Now, Lifetime);

        Assert.Equal(UserId, token.UserId);
        Assert.Equal("hash-1", token.TokenHash);
        Assert.Equal(Now + Lifetime, token.ExpiresAt);
        Assert.False(token.IsRevoked);
        Assert.False(token.IsExpired(Now));
        Assert.NotEqual(token.FamilyId, other.FamilyId);
    }

    [Fact]
    public void Rotate_revokes_the_token_and_issues_the_next_one_in_the_same_family()
    {
        var token = RefreshToken.Issue(UserId, "hash-1", Now, Lifetime);
        var later = Now.AddHours(1);

        var next = token.Rotate("hash-2", later, Lifetime).Value;

        Assert.True(token.IsRevoked);
        Assert.Equal(later, token.RevokedAt);
        Assert.Equal(next.Id, token.ReplacedById);
        Assert.Equal(token.FamilyId, next.FamilyId);
        Assert.Equal(UserId, next.UserId);
        Assert.Equal(later + Lifetime, next.ExpiresAt);
        Assert.False(next.IsRevoked);
    }

    [Fact]
    public void Rotate_rejects_a_revoked_token()
    {
        var token = RefreshToken.Issue(UserId, "hash-1", Now, Lifetime);
        token.Rotate("hash-2", Now, Lifetime);

        Assert.Equal(RefreshTokenErrors.Revoked, token.Rotate("hash-3", Now, Lifetime).Error);
    }

    [Fact]
    public void Rotate_rejects_an_expired_token()
    {
        var token = RefreshToken.Issue(UserId, "hash-1", Now, Lifetime);

        Assert.True(token.IsExpired(Now + Lifetime));
        Assert.Equal(RefreshTokenErrors.Expired, token.Rotate("hash-2", Now + Lifetime, Lifetime).Error);
        Assert.False(token.IsRevoked);
    }

    [Fact]
    public void Revoke_keeps_the_first_revocation_time()
    {
        var token = RefreshToken.Issue(UserId, "hash-1", Now, Lifetime);

        token.Revoke(Now.AddMinutes(1));
        token.Revoke(Now.AddMinutes(2));

        Assert.Equal(Now.AddMinutes(1), token.RevokedAt);
    }
}
