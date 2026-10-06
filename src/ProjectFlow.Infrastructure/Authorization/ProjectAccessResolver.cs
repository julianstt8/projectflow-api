using Microsoft.EntityFrameworkCore;
using ProjectFlow.Application.Abstractions;
using ProjectFlow.Domain.Organizations;
using ProjectFlow.Domain.Projects;
using ProjectFlow.Infrastructure.Persistence;

namespace ProjectFlow.Infrastructure.Authorization;

/// <summary>Scoped: each (project, user) pair is resolved at most once per request.</summary>
internal sealed class ProjectAccessResolver(ApplicationDbContext dbContext, IOrganizationMembership organizationMembership)
    : IProjectAccessResolver
{
    private readonly Dictionary<(Guid OrganizationId, Guid ProjectId, Guid UserId), ProjectAccess?> _cache = [];

    public async Task<ProjectAccess?> GetAsync(Guid organizationId, Guid projectId, Guid userId, CancellationToken cancellationToken)
    {
        var key = (organizationId, projectId, userId);
        if (_cache.TryGetValue(key, out var cached))
        {
            return cached;
        }

        var access = await ResolveAsync(organizationId, projectId, userId, cancellationToken);
        _cache[key] = access;
        return access;
    }

    private async Task<ProjectAccess?> ResolveAsync(Guid organizationId, Guid projectId, Guid userId, CancellationToken cancellationToken)
    {
        // Losing organization membership removes access to all its projects, even with a project role left behind.
        var organizationRole = await organizationMembership.GetRoleAsync(organizationId, userId, cancellationToken);
        if (organizationRole is null)
        {
            return null;
        }

        // The organization is matched explicitly instead of relying on the request's organization filter,
        // so the resolver is correct wherever it is called from. Deleted projects stay hidden.
        var project = await dbContext.Projects
            .IgnoreQueryFilters([ApplicationDbContext.OrganizationFilter])
            .AsNoTracking()
            .Where(p => p.Id == projectId && p.OrganizationId == organizationId)
            .Select(p => new
            {
                Role = p.Members
                    .Where(member => member.UserId == userId)
                    .Select(member => (ProjectRole?)member.Role)
                    .FirstOrDefault(),
            })
            .SingleOrDefaultAsync(cancellationToken);

        return project is null
            ? null
            : new ProjectAccess(userId, organizationRole == OrganizationRole.Admin, project.Role);
    }
}
