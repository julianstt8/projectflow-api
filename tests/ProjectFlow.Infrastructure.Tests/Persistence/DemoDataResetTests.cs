using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using ProjectFlow.Domain.Users;
using ProjectFlow.Infrastructure.Authentication;
using ProjectFlow.Infrastructure.Persistence;
using ProjectFlow.Infrastructure.Persistence.Seeding;

namespace ProjectFlow.Infrastructure.Tests.Persistence;

/// <summary>The public demo goes back to its initial data once the data is older than the reset interval (ADR 0010).</summary>
[Collection(PostgreSqlCollection.Name)]
public sealed class DemoDataResetTests(PostgreSqlFixture database)
{
    private static readonly TimeSpan Interval = TimeSpan.FromHours(24);
    private static readonly PasswordHasher Hasher = new();

    private readonly AdjustableTimeProvider _time = new(new DateTimeOffset(2026, 10, 8, 3, 0, 0, TimeSpan.Zero));

    [Fact]
    public async Task An_empty_database_gets_the_demo_data()
    {
        var connectionString = await database.CreateIsolatedDatabaseAsync();

        var reset = await ResetAsync(connectionString);

        Assert.True(reset);
        Assert.Equal(7, await CountUsersAsync(connectionString));
    }

    [Fact]
    public async Task Recent_demo_data_is_left_alone()
    {
        var connectionString = await database.CreateIsolatedDatabaseAsync();
        await ResetAsync(connectionString);
        await AddVisitorAsync(connectionString);
        _time.Advance(Interval - TimeSpan.FromMinutes(1));

        var reset = await ResetAsync(connectionString);

        Assert.False(reset);
        Assert.Equal(8, await CountUsersAsync(connectionString));
    }

    [Fact]
    public async Task Old_demo_data_is_replaced_by_the_initial_demo_data_only()
    {
        var connectionString = await database.CreateIsolatedDatabaseAsync();
        await ResetAsync(connectionString);
        await AddVisitorAsync(connectionString);
        _time.Advance(Interval);

        var reset = await ResetAsync(connectionString);

        Assert.True(reset);
        await using var dbContext = PostgreSqlFixture.CreateDbContext(connectionString, organizationId: null);
        var users = await dbContext.Users.ToListAsync();
        Assert.Equal(7, users.Count);
        Assert.DoesNotContain(users, user => user.Email.Value.StartsWith("visitor", StringComparison.Ordinal));
        Assert.All(users, user => Assert.Equal(_time.GetUtcNow(), user.CreatedAt));
        Assert.Equal(2, await dbContext.Organizations.CountAsync());
    }

    private async Task<bool> ResetAsync(string connectionString)
    {
        await using var dbContext = PostgreSqlFixture.CreateDbContext(connectionString, organizationId: null);
        var seeder = new DevelopmentDataSeeder(dbContext, Hasher, _time, NullLogger<DevelopmentDataSeeder>.Instance);
        var reset = new DemoDataReset(dbContext, seeder, _time, NullLogger<DemoDataReset>.Instance);

        return await reset.ResetIfStaleAsync(Interval);
    }

    private async Task AddVisitorAsync(string connectionString)
    {
        await using var dbContext = PostgreSqlFixture.CreateDbContext(connectionString, organizationId: null);
        dbContext.Users.Add(User.Create(Email.Create("visitor@example.com").Value, "hash", "Visitor", _time.GetUtcNow()).Value);
        await dbContext.SaveChangesAsync();
    }

    private static async Task<int> CountUsersAsync(string connectionString)
    {
        await using var dbContext = PostgreSqlFixture.CreateDbContext(connectionString, organizationId: null);
        return await dbContext.Users.CountAsync();
    }

    private sealed class AdjustableTimeProvider(DateTimeOffset now) : TimeProvider
    {
        private DateTimeOffset _now = now;

        public override DateTimeOffset GetUtcNow() => _now;

        public void Advance(TimeSpan time) => _now += time;
    }
}
