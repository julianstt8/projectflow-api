using ProjectFlow.Domain.Users;

namespace ProjectFlow.Application.Abstractions.Persistence;

public interface IUserRepository
{
    Task<bool> ExistsWithEmailAsync(Email email, CancellationToken cancellationToken);

    Task<User?> GetByEmailAsync(Email email, CancellationToken cancellationToken);

    void Add(User user);
}
