using Microsoft.EntityFrameworkCore;
using ProjectFlow.Application.Abstractions.Persistence;
using ProjectFlow.Application.Organizations;
using ProjectFlow.Domain.Organizations;

namespace ProjectFlow.Infrastructure.Persistence.Queries;

/// <summary>Read-only projections; nothing is tracked.</summary>
internal sealed class OrganizationQueries(ApplicationDbContext dbContext) : IOrganizationQueries
{
    public async Task<IReadOnlyList<OrganizationSummaryResponse>> ListForUserAsync(Guid userId, CancellationToken cancellationToken)
    {
        var rows = await dbContext.Organizations
            .AsNoTracking()
            .SelectMany(
                organization => organization.Members.Where(member => member.UserId == userId),
                (organization, member) => new { organization.Id, organization.Name, organization.Slug, member.Role })
            .OrderBy(row => row.Name)
            .ToListAsync(cancellationToken);

        return rows.Select(row => new OrganizationSummaryResponse(row.Id, row.Name, row.Slug.Value, row.Role)).ToList();
    }

    public async Task<OrganizationDetailsResponse?> GetDetailsAsync(Guid organizationId, CancellationToken cancellationToken)
    {
        var organization = await dbContext.Organizations
            .AsNoTracking()
            .Where(o => o.Id == organizationId)
            .Select(o => new { o.Id, o.Name, o.Slug, o.CreatedAt })
            .SingleOrDefaultAsync(cancellationToken);

        if (organization is null)
        {
            return null;
        }

        var members = await dbContext.Set<OrganizationMember>()
            .AsNoTracking()
            .Where(member => member.OrganizationId == organizationId)
            .Join(
                dbContext.Users,
                member => member.UserId,
                user => user.Id,
                (member, user) => new { member.UserId, user.Email, user.FullName, member.Role, member.JoinedAt })
            .OrderBy(row => row.JoinedAt)
            .ThenBy(row => row.FullName)
            .ToListAsync(cancellationToken);

        return new OrganizationDetailsResponse(
            organization.Id,
            organization.Name,
            organization.Slug.Value,
            organization.CreatedAt,
            members.Select(row => new OrganizationMemberResponse(row.UserId, row.Email.Value, row.FullName, row.Role, row.JoinedAt)).ToList());
    }
}
