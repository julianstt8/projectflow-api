using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage;
using Npgsql;
using ProjectFlow.Application.Abstractions;
using ProjectFlow.Application.Abstractions.Persistence;
using ProjectFlow.Domain.Activity;
using ProjectFlow.Domain.Common;

namespace ProjectFlow.Infrastructure.Persistence;

/// <summary>Saves the request's changes and turns database errors into Application exceptions (no EF types leak out).</summary>
internal sealed class UnitOfWork(ApplicationDbContext dbContext, ICurrentUser currentUser, TimeProvider timeProvider) : IUnitOfWork
{
    public async Task SaveChangesAsync(CancellationToken cancellationToken)
    {
        RecordActivity();

        try
        {
            await dbContext.SaveChangesAsync(cancellationToken);
        }
        catch (DbUpdateConcurrencyException exception)
        {
            throw new ConcurrencyConflictException(exception);
        }
        catch (DbUpdateException exception) when (exception.InnerException is PostgresException { SqlState: PostgresErrorCodes.UniqueViolation } postgres)
        {
            throw new UniqueConstraintViolationException(postgres.ConstraintName, exception);
        }
    }

    /// <summary>
    /// Turns the activity events raised by the changed entities into activity log entries (RF-10), saved in
    /// the same transaction as the change itself: if the change fails, nothing is logged.
    /// </summary>
    private void RecordActivity()
    {
        var activity = dbContext.ChangeTracker.Entries<Entity>()
            .SelectMany(entry => entry.Entity.PullDomainEvents())
            .OfType<ProjectActivityEvent>()
            .ToList();

        if (activity.Count == 0)
        {
            return;
        }

        var now = timeProvider.GetUtcNow();
        var actorId = currentUser.UserId;
        dbContext.ActivityLogs.AddRange(activity.Select(change => ActivityLog.Record(change, actorId, now)));
    }

    public async Task<ITransaction> BeginTransactionAsync(CancellationToken cancellationToken) =>
        new EfTransaction(await dbContext.Database.BeginTransactionAsync(cancellationToken));

    private sealed class EfTransaction(IDbContextTransaction transaction) : ITransaction
    {
        public Task CommitAsync(CancellationToken cancellationToken) => transaction.CommitAsync(cancellationToken);

        public ValueTask DisposeAsync() => transaction.DisposeAsync();
    }
}
