using Microsoft.EntityFrameworkCore;
using ProjectFlow.Application.Abstractions.Persistence;
using ProjectFlow.Application.Projects;
using ProjectFlow.Domain.Projects;

namespace ProjectFlow.Infrastructure.Persistence.Queries;

/// <summary>Read-only projections; organization and soft-delete filters apply.</summary>
internal sealed class ProjectQueries(ApplicationDbContext dbContext) : IProjectQueries
{
    public async Task<IReadOnlyList<ProjectSummaryResponse>> ListVisibleAsync(
        Guid organizationId,
        Guid userId,
        bool isOrganizationAdmin,
        CancellationToken cancellationToken)
    {
        var rows = await dbContext.Projects
            .AsNoTracking()
            .Where(project => project.OrganizationId == organizationId
                && (isOrganizationAdmin || project.Members.Any(member => member.UserId == userId)))
            .Select(project => new
            {
                project.Id,
                project.Key,
                project.Name,
                project.IsArchived,
                MyRole = project.Members
                    .Where(member => member.UserId == userId)
                    .Select(member => (ProjectRole?)member.Role)
                    .FirstOrDefault(),
            })
            .OrderBy(row => row.Name)
            .ToListAsync(cancellationToken);

        return rows.Select(row => new ProjectSummaryResponse(row.Id, row.Key.Value, row.Name, row.IsArchived, row.MyRole)).ToList();
    }

    public async Task<ProjectDetailsResponse?> GetDetailsAsync(Guid projectId, CancellationToken cancellationToken)
    {
        var project = await dbContext.Projects
            .AsNoTracking()
            .Where(p => p.Id == projectId)
            .Select(p => new { p.Id, p.Key, p.Name, p.Description, p.IsArchived, p.CreatedAt })
            .SingleOrDefaultAsync(cancellationToken);

        if (project is null)
        {
            return null;
        }

        var members = await dbContext.Set<ProjectMember>()
            .AsNoTracking()
            .Where(member => member.ProjectId == projectId)
            .Join(
                dbContext.Users,
                member => member.UserId,
                user => user.Id,
                (member, user) => new { member.UserId, user.Email, user.FullName, member.Role })
            .ToListAsync(cancellationToken);

        return new ProjectDetailsResponse(
            project.Id,
            project.Key.Value,
            project.Name,
            project.Description,
            project.IsArchived,
            project.CreatedAt,
            members
                // Roles are stored as text, so they are ordered here by their enum value (managers first).
                .OrderBy(row => row.Role)
                .ThenBy(row => row.FullName, StringComparer.OrdinalIgnoreCase)
                .Select(row => new ProjectMemberResponse(row.UserId, row.Email.Value, row.FullName, row.Role))
                .ToList());
    }
}
