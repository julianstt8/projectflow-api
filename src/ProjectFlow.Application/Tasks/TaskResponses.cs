using ProjectFlow.Domain.Common;
using ProjectFlow.Domain.Tasks;

namespace ProjectFlow.Application.Tasks;

/// <param name="Key">Human-readable key: project key and number, e.g. <c>WEB-12</c> (RF-04).</param>
public sealed record TaskResponse(
    Guid Id,
    string Key,
    int Number,
    string Title,
    string? Description,
    TaskType Type,
    TaskPriority Priority,
    TaskItemStatus Status,
    int? StoryPoints,
    Guid ReporterId,
    Guid? AssigneeId,
    Guid? SprintId,
    Guid? EpicId,
    IReadOnlyList<Guid> LabelIds,
    DateTimeOffset CreatedAt,
    DateTimeOffset UpdatedAt);

public static class TaskUseCaseErrors
{
    public static readonly Error NotFound = Error.NotFound("Task.NotFound", "The task does not exist.");

    public static readonly Error NotYourTask =
        Error.Forbidden("Task.NotYourTask", "Developers can only change tasks they reported or are assigned to.");

    public static readonly Error AssigneeNotAllowed =
        Error.BusinessRule("Task.AssigneeNotAllowed", "Tasks can only be assigned to people who can work in the project.");

    public static readonly Error SprintNotInProject =
        Error.BusinessRule("Task.SprintNotInProject", "The sprint does not exist in this project.");

    public static readonly Error EpicNotInProject =
        Error.BusinessRule("Task.EpicNotInProject", "The epic does not exist in this project.");
}
