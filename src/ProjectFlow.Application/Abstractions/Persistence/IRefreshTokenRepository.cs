using ProjectFlow.Domain.Users;

namespace ProjectFlow.Application.Abstractions.Persistence;

public interface IRefreshTokenRepository
{
    Task<RefreshToken?> GetByHashAsync(string tokenHash, CancellationToken cancellationToken);

    void Add(RefreshToken refreshToken);

    /// <summary>Revokes every active token of the family immediately, outside the unit of work.</summary>
    Task RevokeFamilyAsync(Guid familyId, DateTimeOffset now, CancellationToken cancellationToken);
}
