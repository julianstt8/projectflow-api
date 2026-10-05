using Mediator;
using ProjectFlow.Application.Abstractions;
using ProjectFlow.Application.Abstractions.Persistence;
using ProjectFlow.Domain.Common;

namespace ProjectFlow.Application.Organizations;

/// <summary>Organizations the current user belongs to, with their role in each one.</summary>
public sealed record GetMyOrganizationsQuery : IQuery<IReadOnlyList<OrganizationSummaryResponse>>;

public sealed class GetMyOrganizationsQueryHandler(IOrganizationQueries queries, ICurrentUser currentUser)
    : IQueryHandler<GetMyOrganizationsQuery, IReadOnlyList<OrganizationSummaryResponse>>
{
    public async ValueTask<IReadOnlyList<OrganizationSummaryResponse>> Handle(
        GetMyOrganizationsQuery query,
        CancellationToken cancellationToken) =>
        await queries.ListForUserAsync(currentUser.UserId, cancellationToken);
}

/// <summary>An organization with its members. Members only (enforced by the endpoint policy).</summary>
public sealed record GetOrganizationQuery(Guid OrganizationId) : IQuery<Result<OrganizationDetailsResponse>>;

public sealed class GetOrganizationQueryHandler(IOrganizationQueries queries)
    : IQueryHandler<GetOrganizationQuery, Result<OrganizationDetailsResponse>>
{
    public async ValueTask<Result<OrganizationDetailsResponse>> Handle(
        GetOrganizationQuery query,
        CancellationToken cancellationToken)
    {
        var details = await queries.GetDetailsAsync(query.OrganizationId, cancellationToken);

        return details is null ? OrganizationUseCaseErrors.NotFound : details;
    }
}
