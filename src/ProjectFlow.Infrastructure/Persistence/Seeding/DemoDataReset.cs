using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace ProjectFlow.Infrastructure.Persistence.Seeding;

/// <summary>Settings of the public demo (ADR 0010). Leave <see cref="ResetIntervalHours"/> at 0 everywhere else.</summary>
public sealed class DemoOptions
{
    public const string SectionName = "Demo";

    /// <summary>Restore the demo data when it is older than this many hours; 0 turns the reset off.</summary>
    public int ResetIntervalHours { get; init; }
}

/// <summary>
/// Puts the public demo back to its initial state: empties every table of the model and loads the demo data again,
/// in one transaction. It also empties the activity log, on purpose and only here: the insert-only trigger guards
/// against UPDATE and DELETE, and a demo reset is neither.
/// </summary>
internal sealed class DemoDataReset(
    ApplicationDbContext dbContext,
    DevelopmentDataSeeder seeder,
    TimeProvider timeProvider,
    ILogger<DemoDataReset> logger)
{
    /// <returns><see langword="true"/> if the data was restored.</returns>
    public async Task<bool> ResetIfStaleAsync(TimeSpan maxAge, CancellationToken cancellationToken = default)
    {
        // The demo users are the oldest rows: their creation time is the time of the last reset.
        var seededAt = await dbContext.Users.MinAsync(user => (DateTimeOffset?)user.CreatedAt, cancellationToken);
        if (seededAt is { } at && timeProvider.GetUtcNow() - at < maxAge)
        {
            return false;
        }

        await using var transaction = await dbContext.Database.BeginTransactionAsync(cancellationToken);
        await dbContext.Database.ExecuteSqlRawAsync(TruncateEveryTable(), cancellationToken);
        await seeder.SeedAsync(cancellationToken);
        await transaction.CommitAsync(cancellationToken);

        logger.LogInformation("Demo data restored (previous data from {SeededAt})", seededAt);
        return true;
    }

    // Table names come from the EF Core model, never from input.
    private string TruncateEveryTable() =>
        "TRUNCATE TABLE " + string.Join(", ", dbContext.Model.GetEntityTypes()
            .Where(entity => entity.GetTableName() is not null && entity.GetViewName() is null)
            .Select(entity => $"\"{entity.GetSchema() ?? "public"}\".\"{entity.GetTableName()}\"")
            .Distinct()
            .Order()) + " CASCADE";
}
