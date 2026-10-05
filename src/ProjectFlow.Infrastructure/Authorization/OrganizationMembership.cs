using Microsoft.EntityFrameworkCore;
using ProjectFlow.Application.Abstractions;
using ProjectFlow.Domain.Organizations;
using ProjectFlow.Infrastructure.Persistence;

namespace ProjectFlow.Infrastructure.Authorization;

/// <summary>Scoped: each role is read from the database at most once per request.</summary>
internal sealed class OrganizationMembership(ApplicationDbContext dbContext) : IOrganizationMembership
{
    private readonly Dictionary<(Guid OrganizationId, Guid UserId), OrganizationRole?> _cache = [];

    public async Task<OrganizationRole?> GetRoleAsync(Guid organizationId, Guid userId, CancellationToken cancellationToken)
    {
        if (_cache.TryGetValue((organizationId, userId), out var cached))
        {
            return cached;
        }

        var role = await dbContext.Set<OrganizationMember>()
            .AsNoTracking()
            .Where(member => member.OrganizationId == organizationId && member.UserId == userId)
            .Select(member => (OrganizationRole?)member.Role)
            .SingleOrDefaultAsync(cancellationToken);

        _cache[(organizationId, userId)] = role;
        return role;
    }
}
