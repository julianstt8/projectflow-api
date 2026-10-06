using System.Globalization;
using System.Text.RegularExpressions;
using Microsoft.EntityFrameworkCore;
using ProjectFlow.Application.Abstractions.Persistence;
using ProjectFlow.Application.Common;
using ProjectFlow.Application.Tasks;
using ProjectFlow.Domain.Projects;
using ProjectFlow.Domain.Tasks;

namespace ProjectFlow.Infrastructure.Persistence.Queries;

/// <summary>Read-only projections; organization and soft-delete filters apply.</summary>
internal sealed partial class TaskQueries(ApplicationDbContext dbContext) : ITaskQueries
{
    public async Task<PagedResponse<TaskResponse>> SearchAsync(TaskSearchCriteria criteria, CancellationToken cancellationToken)
    {
        var tasks = Filter(dbContext.Tasks.AsNoTracking().Where(task => task.ProjectId == criteria.ProjectId), criteria);

        var totalCount = await tasks.CountAsync(cancellationToken);

        var rows = await Rows(Sort(tasks, criteria.Sort)
                .Skip((criteria.Page - 1) * criteria.PageSize)
                .Take(criteria.PageSize))
            .ToListAsync(cancellationToken);

        return new PagedResponse<TaskResponse>(rows.Select(ToResponse).ToList(), criteria.Page, criteria.PageSize, totalCount);
    }

    public async Task<TaskResponse?> GetAsync(Guid projectId, Guid taskId, CancellationToken cancellationToken)
    {
        var row = await Rows(dbContext.Tasks.AsNoTracking().Where(task => task.Id == taskId && task.ProjectId == projectId))
            .SingleOrDefaultAsync(cancellationToken);

        return row is null ? null : ToResponse(row);
    }

    /// <summary>
    /// Each filter maps to a column covered by the data model's indexes: (organization_id, project_id, status),
    /// (organization_id, assignee_id), (organization_id, sprint_id), and the label_id index of task_labels.
    /// </summary>
    private static IQueryable<TaskItem> Filter(IQueryable<TaskItem> tasks, TaskSearchCriteria criteria)
    {
        if (criteria.Statuses.Count > 0)
        {
            var statuses = criteria.Statuses.ToList();
            tasks = tasks.Where(task => statuses.Contains(task.Status));
        }

        if (criteria.Unassigned)
        {
            tasks = tasks.Where(task => task.AssigneeId == null);
        }
        else if (criteria.AssigneeId is { } assigneeId)
        {
            tasks = tasks.Where(task => task.AssigneeId == assigneeId);
        }

        if (criteria.Backlog)
        {
            tasks = tasks.Where(task => task.SprintId == null);
        }
        else if (criteria.SprintId is { } sprintId)
        {
            tasks = tasks.Where(task => task.SprintId == sprintId);
        }

        if (criteria.EpicId is { } epicId)
        {
            tasks = tasks.Where(task => task.EpicId == epicId);
        }

        if (criteria.LabelId is { } labelId)
        {
            tasks = tasks.Where(task => task.Labels.Any(label => label.LabelId == labelId));
        }

        if (!string.IsNullOrWhiteSpace(criteria.Text))
        {
            var text = criteria.Text.Trim();
            var pattern = $"%{EscapeLike(text)}%";
            var number = ParseTaskNumber(text);

            // unaccent + ILIKE: case- and accent-insensitive ("validacion" finds "Validación").
            tasks = tasks.Where(task =>
                EF.Functions.ILike(EF.Functions.Unaccent(task.Title), EF.Functions.Unaccent(pattern), "\\")
                || (number != null && task.Number == number));
        }

        return tasks;
    }

    private static IQueryable<TaskItem> Sort(IQueryable<TaskItem> tasks, string sort) => sort switch
    {
        TaskSort.NumberDescending => tasks.OrderByDescending(task => task.Number),
        TaskSort.UpdatedAt => tasks.OrderBy(task => task.UpdatedAt).ThenBy(task => task.Number),
        TaskSort.UpdatedAtDescending => tasks.OrderByDescending(task => task.UpdatedAt).ThenByDescending(task => task.Number),
        _ => tasks.OrderBy(task => task.Number),
    };

    /// <summary>"12", "#12" or "WEB-12" → 12, so tasks can be found by their key.</summary>
    private static int? ParseTaskNumber(string text)
    {
        var match = TaskNumberPattern().Match(text);
        return match.Success && int.TryParse(match.Groups["number"].Value, NumberStyles.None, CultureInfo.InvariantCulture, out var number)
            ? number
            : null;
    }

    private static string EscapeLike(string text) =>
        text.Replace("\\", "\\\\").Replace("%", "\\%").Replace("_", "\\_");

    private IQueryable<TaskRow> Rows(IQueryable<TaskItem> tasks) =>
        tasks
            .Join(
                dbContext.Projects,
                task => task.ProjectId,
                project => project.Id,
                (task, project) => new TaskRow
                {
                    Task = task,
                    ProjectKey = project.Key,
                    LabelIds = task.Labels.Select(label => label.LabelId).ToList(),
                });

    private static TaskResponse ToResponse(TaskRow row) =>
        new(
            row.Task.Id,
            $"{row.ProjectKey.Value}-{row.Task.Number}",
            row.Task.Number,
            row.Task.Title,
            row.Task.Description,
            row.Task.Type,
            row.Task.Priority,
            row.Task.Status,
            row.Task.StoryPoints,
            row.Task.ReporterId,
            row.Task.AssigneeId,
            row.Task.SprintId,
            row.Task.EpicId,
            row.LabelIds,
            row.Task.CreatedAt,
            row.Task.UpdatedAt);

    [GeneratedRegex(@"^(?:[A-Za-z][A-Za-z0-9]{1,9}-|#)?(?<number>\d{1,9})$", RegexOptions.CultureInvariant)]
    private static partial Regex TaskNumberPattern();

    private sealed class TaskRow
    {
        public required TaskItem Task { get; init; }

        public required ProjectKey ProjectKey { get; init; }

        public required List<Guid> LabelIds { get; init; }
    }
}
