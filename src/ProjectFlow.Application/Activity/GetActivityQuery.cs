using FluentValidation;
using Mediator;
using ProjectFlow.Application.Abstractions.Persistence;
using ProjectFlow.Application.Common;

namespace ProjectFlow.Application.Activity;

public sealed record ActivityResponse(
    Guid Id,
    Guid ActorId,
    string ActorName,
    string EntityType,
    Guid EntityId,
    string Action,
    string? OldValue,
    string? NewValue,
    DateTimeOffset CreatedAt);

/// <summary>The project's activity log (RF-10). Admins and project managers only, by the endpoint permission.</summary>
public sealed record GetActivityQuery(Guid ProjectId, Guid? EntityId, int Page, int PageSize) : IQuery<PagedResponse<ActivityResponse>>;

public sealed class GetActivityQueryValidator : AbstractValidator<GetActivityQuery>
{
    public GetActivityQueryValidator()
    {
        RuleFor(query => query.Page).GreaterThanOrEqualTo(1);
        RuleFor(query => query.PageSize).InclusiveBetween(1, PagedResponse<ActivityResponse>.MaxPageSize);
    }
}

public sealed class GetActivityQueryHandler(IActivityQueries queries) : IQueryHandler<GetActivityQuery, PagedResponse<ActivityResponse>>
{
    public async ValueTask<PagedResponse<ActivityResponse>> Handle(GetActivityQuery query, CancellationToken cancellationToken) =>
        await queries.ListAsync(query.ProjectId, query.EntityId, query.Page, query.PageSize, cancellationToken);
}
