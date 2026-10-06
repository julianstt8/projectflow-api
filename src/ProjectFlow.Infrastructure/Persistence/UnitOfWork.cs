using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage;
using Npgsql;
using ProjectFlow.Application.Abstractions.Persistence;

namespace ProjectFlow.Infrastructure.Persistence;

/// <summary>Saves the request's changes and turns database errors into Application exceptions (no EF types leak out).</summary>
internal sealed class UnitOfWork(ApplicationDbContext dbContext) : IUnitOfWork
{
    public async Task SaveChangesAsync(CancellationToken cancellationToken)
    {
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

    public async Task<ITransaction> BeginTransactionAsync(CancellationToken cancellationToken) =>
        new EfTransaction(await dbContext.Database.BeginTransactionAsync(cancellationToken));

    private sealed class EfTransaction(IDbContextTransaction transaction) : ITransaction
    {
        public Task CommitAsync(CancellationToken cancellationToken) => transaction.CommitAsync(cancellationToken);

        public ValueTask DisposeAsync() => transaction.DisposeAsync();
    }
}
