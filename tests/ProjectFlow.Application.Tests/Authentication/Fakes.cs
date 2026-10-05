using ProjectFlow.Application.Abstractions;
using ProjectFlow.Application.Abstractions.Persistence;
using ProjectFlow.Domain.Users;

namespace ProjectFlow.Application.Tests.Authentication;

internal sealed class InMemoryUserRepository : IUserRepository
{
    public List<User> Users { get; } = [];

    public Task<bool> ExistsWithEmailAsync(Email email, CancellationToken cancellationToken) =>
        Task.FromResult(Users.Exists(user => user.Email == email));

    public Task<User?> GetByEmailAsync(Email email, CancellationToken cancellationToken) =>
        Task.FromResult(Users.Find(user => user.Email == email));

    public Task<User?> GetByIdAsync(Guid id, CancellationToken cancellationToken) =>
        Task.FromResult(Users.Find(user => user.Id == id));

    public void Add(User user) => Users.Add(user);
}

internal sealed class InMemoryRefreshTokenRepository : IRefreshTokenRepository
{
    public List<RefreshToken> Tokens { get; } = [];

    public Task<RefreshToken?> GetByHashAsync(string tokenHash, CancellationToken cancellationToken) =>
        Task.FromResult(Tokens.Find(token => token.TokenHash == tokenHash));

    public void Add(RefreshToken refreshToken) => Tokens.Add(refreshToken);

    public Task RevokeFamilyAsync(Guid familyId, DateTimeOffset now, CancellationToken cancellationToken)
    {
        foreach (var token in Tokens.Where(token => token.FamilyId == familyId))
        {
            token.Revoke(now);
        }

        return Task.CompletedTask;
    }
}

internal sealed class FakeUnitOfWork : IUnitOfWork
{
    public int SaveCount { get; private set; }

    /// <summary>When set, the next save fails as if another request had saved first.</summary>
    public bool FailNextSaveWithConflict { get; set; }

    public Task SaveChangesAsync(CancellationToken cancellationToken)
    {
        if (FailNextSaveWithConflict)
        {
            FailNextSaveWithConflict = false;
            throw new ConcurrencyConflictException(new InvalidOperationException("Simulated conflict"));
        }

        SaveCount++;
        return Task.CompletedTask;
    }
}

/// <summary>Sequential, readable tokens: "refresh-1", "refresh-2"... with hash "sha:refresh-1".</summary>
internal sealed class FakeRefreshTokenService : IRefreshTokenService
{
    private int _counter;

    public TimeSpan Lifetime => TimeSpan.FromDays(7);

    public GeneratedRefreshToken Generate()
    {
        var value = $"refresh-{++_counter}";
        return new GeneratedRefreshToken(value, Hash(value));
    }

    public string Hash(string refreshToken) => $"sha:{refreshToken}";
}

/// <summary>Deterministic, readable "hash" so tests can assert what was stored.</summary>
internal sealed class FakePasswordHasher : IPasswordHasher
{
    public int HashCount { get; private set; }

    public string Hash(string password)
    {
        HashCount++;
        return $"hashed:{password}";
    }

    public bool Verify(string password, string passwordHash) => passwordHash == $"hashed:{password}";
}

internal sealed class FakeAccessTokenGenerator : IAccessTokenGenerator
{
    public static readonly DateTimeOffset ExpiresAt = new(2026, 10, 5, 12, 15, 0, TimeSpan.Zero);

    public AccessToken Generate(User user) => new($"token-for-{user.Id}", ExpiresAt);
}

internal sealed class FixedTimeProvider(DateTimeOffset now) : TimeProvider
{
    public override DateTimeOffset GetUtcNow() => now;
}
