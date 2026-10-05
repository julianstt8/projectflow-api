using Microsoft.EntityFrameworkCore;
using ProjectFlow.Application.Abstractions.Persistence;
using ProjectFlow.Domain.Users;

namespace ProjectFlow.Infrastructure.Persistence.Repositories;

internal sealed class RefreshTokenRepository(ApplicationDbContext dbContext) : IRefreshTokenRepository
{
    public Task<RefreshToken?> GetByHashAsync(string tokenHash, CancellationToken cancellationToken) =>
        dbContext.RefreshTokens.SingleOrDefaultAsync(token => token.TokenHash == tokenHash, cancellationToken);

    public void Add(RefreshToken refreshToken) => dbContext.RefreshTokens.Add(refreshToken);

    public Task RevokeFamilyAsync(Guid familyId, DateTimeOffset now, CancellationToken cancellationToken) =>
        dbContext.RefreshTokens
            .Where(token => token.FamilyId == familyId && token.RevokedAt == null)
            .ExecuteUpdateAsync(setters => setters.SetProperty(token => token.RevokedAt, now), cancellationToken);
}
