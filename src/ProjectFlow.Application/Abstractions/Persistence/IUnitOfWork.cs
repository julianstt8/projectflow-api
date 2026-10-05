namespace ProjectFlow.Application.Abstractions.Persistence;

/// <summary>Saves every change made through the repositories of the current request in one transaction.</summary>
public interface IUnitOfWork
{
    Task SaveChangesAsync(CancellationToken cancellationToken);
}
