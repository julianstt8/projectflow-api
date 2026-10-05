using Microsoft.EntityFrameworkCore;
using ProjectFlow.Application.Abstractions.Persistence;
using ProjectFlow.Domain.Organizations;

namespace ProjectFlow.Infrastructure.Persistence.Repositories;

internal sealed class OrganizationRepository(ApplicationDbContext dbContext) : IOrganizationRepository
{
    public Task<bool> ExistsWithSlugAsync(Slug slug, CancellationToken cancellationToken) =>
        dbContext.Organizations.AnyAsync(organization => organization.Slug == slug, cancellationToken);

    public Task<Organization?> GetByIdAsync(Guid id, CancellationToken cancellationToken) =>
        dbContext.Organizations
            .Include(organization => organization.Members)
            .SingleOrDefaultAsync(organization => organization.Id == id, cancellationToken);

    public void Add(Organization organization) => dbContext.Organizations.Add(organization);
}
