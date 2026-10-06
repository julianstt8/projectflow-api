using Mediator;
using ProjectFlow.Application.Abstractions;
using ProjectFlow.Application.Abstractions.Persistence;
using ProjectFlow.Domain.Common;
using ProjectFlow.Domain.Organizations;

namespace ProjectFlow.Application.Projects;

/// <summary>Projects the current user can see in the organization (PRD 4.1).</summary>
public sealed record GetProjectsQuery(Guid OrganizationId) : IQuery<IReadOnlyList<ProjectSummaryResponse>>;

public sealed class GetProjectsQueryHandler(
    IProjectQueries queries,
    IOrganizationMembership organizationMembership,
    ICurrentUser currentUser)
    : IQueryHandler<GetProjectsQuery, IReadOnlyList<ProjectSummaryResponse>>
{
    public async ValueTask<IReadOnlyList<ProjectSummaryResponse>> Handle(GetProjectsQuery query, CancellationToken cancellationToken)
    {
        var role = await organizationMembership.GetRoleAsync(query.OrganizationId, currentUser.UserId, cancellationToken);

        return await queries.ListVisibleAsync(
            query.OrganizationId,
            currentUser.UserId,
            isOrganizationAdmin: role == OrganizationRole.Admin,
            cancellationToken);
    }
}

public sealed record GetProjectQuery(Guid ProjectId) : IQuery<Result<ProjectDetailsResponse>>;

public sealed class GetProjectQueryHandler(IProjectQueries queries)
    : IQueryHandler<GetProjectQuery, Result<ProjectDetailsResponse>>
{
    public async ValueTask<Result<ProjectDetailsResponse>> Handle(GetProjectQuery query, CancellationToken cancellationToken)
    {
        var details = await queries.GetDetailsAsync(query.ProjectId, cancellationToken);

        return details is null ? ProjectUseCaseErrors.NotFound : details;
    }
}
