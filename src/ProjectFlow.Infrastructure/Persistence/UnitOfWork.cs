using Microsoft.EntityFrameworkCore;
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
}
