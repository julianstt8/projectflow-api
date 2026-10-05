using Microsoft.EntityFrameworkCore;
using ProjectFlow.Application.Abstractions.Persistence;
using ProjectFlow.Domain.Users;

namespace ProjectFlow.Infrastructure.Persistence.Repositories;

internal sealed class UserRepository(ApplicationDbContext dbContext) : IUserRepository
{
    public Task<bool> ExistsWithEmailAsync(Email email, CancellationToken cancellationToken) =>
        dbContext.Users.AnyAsync(user => user.Email == email, cancellationToken);

    public Task<User?> GetByEmailAsync(Email email, CancellationToken cancellationToken) =>
        dbContext.Users.SingleOrDefaultAsync(user => user.Email == email, cancellationToken);

    public void Add(User user) => dbContext.Users.Add(user);
}
