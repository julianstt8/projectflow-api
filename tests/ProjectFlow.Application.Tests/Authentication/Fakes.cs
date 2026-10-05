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

    public void Add(User user) => Users.Add(user);
}

internal sealed class FakeUnitOfWork : IUnitOfWork
{
    public int SaveCount { get; private set; }

    public Task SaveChangesAsync(CancellationToken cancellationToken)
    {
        SaveCount++;
        return Task.CompletedTask;
    }
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
