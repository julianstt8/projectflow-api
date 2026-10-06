using FluentValidation;
using Mediator;
using ProjectFlow.Application.Abstractions.Persistence;
using ProjectFlow.Application.Common;
using ProjectFlow.Domain.Tasks;

namespace ProjectFlow.Application.Tasks;

public static class TaskSort
{
    public const string Number = "number";
    public const string NumberDescending = "-number";
    public const string UpdatedAt = "updatedAt";
    public const string UpdatedAtDescending = "-updatedAt";

    public static readonly string[] All = [Number, NumberDescending, UpdatedAt, UpdatedAtDescending];
}

/// <summary>Filters of a task search (RF-11). Every filter is optional; they are combined with AND.</summary>
/// <param name="Statuses">Any of these statuses.</param>
/// <param name="Unassigned">Only tasks without assignee (instead of <paramref name="AssigneeId"/>).</param>
/// <param name="Backlog">Only tasks outside any sprint (instead of <paramref name="SprintId"/>).</param>
/// <param name="Text">
/// Words of the title, ignoring case and accents ("validacion" finds "Validación"), or a task key or number ("WEB-12", "12").
/// </param>
public sealed record TaskSearchCriteria(
    Guid ProjectId,
    IReadOnlyList<TaskItemStatus> Statuses,
    Guid? AssigneeId,
    bool Unassigned,
    Guid? SprintId,
    bool Backlog,
    Guid? EpicId,
    Guid? LabelId,
    string? Text,
    string Sort,
    int Page,
    int PageSize);

public sealed record SearchTasksQuery(TaskSearchCriteria Criteria) : IQuery<PagedResponse<TaskResponse>>;

public sealed class SearchTasksQueryValidator : AbstractValidator<SearchTasksQuery>
{
    public const int TextMaxLength = 200;

    public SearchTasksQueryValidator()
    {
        RuleFor(query => query.Criteria.Page).GreaterThanOrEqualTo(1).OverridePropertyName("page");
        RuleFor(query => query.Criteria.PageSize)
            .InclusiveBetween(1, PagedResponse<TaskResponse>.MaxPageSize)
            .OverridePropertyName("pageSize");
        RuleFor(query => query.Criteria.Text).MaximumLength(TextMaxLength).OverridePropertyName("q");
        RuleForEach(query => query.Criteria.Statuses).IsInEnum().OverridePropertyName("status");
        RuleFor(query => query.Criteria.Sort)
            .Must(sort => TaskSort.All.Contains(sort))
            .WithMessage($"Sort must be one of: {string.Join(", ", TaskSort.All)}.")
            .OverridePropertyName("sort");
        RuleFor(query => query.Criteria)
            .Must(criteria => !(criteria.Unassigned && criteria.AssigneeId is not null))
            .WithMessage("Use either assigneeId or unassigned, not both.")
            .OverridePropertyName("unassigned");
        RuleFor(query => query.Criteria)
            .Must(criteria => !(criteria.Backlog && criteria.SprintId is not null))
            .WithMessage("Use either sprintId or backlog, not both.")
            .OverridePropertyName("backlog");
    }
}

public sealed class SearchTasksQueryHandler(ITaskQueries queries) : IQueryHandler<SearchTasksQuery, PagedResponse<TaskResponse>>
{
    public async ValueTask<PagedResponse<TaskResponse>> Handle(SearchTasksQuery query, CancellationToken cancellationToken) =>
        await queries.SearchAsync(query.Criteria, cancellationToken);
}
