namespace ProjectFlow.Application.Abstractions.Persistence;

/// <summary>Saves every change made through the repositories of the current request in one transaction.</summary>
public interface IUnitOfWork
{
    Task SaveChangesAsync(CancellationToken cancellationToken);

    /// <summary>
    /// Starts an explicit transaction, for use cases that must lock rows before reading them
    /// (e.g. task numbering). Disposing it without <see cref="ITransaction.CommitAsync"/> rolls it back.
    /// </summary>
    Task<ITransaction> BeginTransactionAsync(CancellationToken cancellationToken);
}

public interface ITransaction : IAsyncDisposable
{
    Task CommitAsync(CancellationToken cancellationToken);
}
