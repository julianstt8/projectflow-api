using ProjectFlow.Domain.Common;
using ProjectFlow.Domain.Epics;
using ProjectFlow.Domain.Labels;
using ProjectFlow.Domain.Projects;
using ProjectFlow.Domain.Sprints;

namespace ProjectFlow.Domain.Tasks;

/// <summary>A unit of work in a project. Named <c>TaskItem</c> to avoid clashing with <see cref="System.Threading.Tasks.Task"/>.</summary>
public sealed class TaskItem : Entity, IOrganizationOwned, ISoftDeletable
{
    public const int TitleMaxLength = 200;
    public const int DescriptionMaxLength = 10000;
    public const int MaxStoryPoints = 100;

    /// <summary>Allowed status changes (RF-07). Review → InProgress is the only step back.</summary>
    private static readonly Dictionary<TaskItemStatus, TaskItemStatus[]> AllowedTransitions = new()
    {
        [TaskItemStatus.ToDo] = [TaskItemStatus.InProgress],
        [TaskItemStatus.InProgress] = [TaskItemStatus.Review],
        [TaskItemStatus.Review] = [TaskItemStatus.Done, TaskItemStatus.InProgress],
        [TaskItemStatus.Done] = [],
    };

    private readonly List<TaskLabel> _labels = [];

    private TaskItem(
        Guid id,
        Guid projectId,
        Guid organizationId,
        int number,
        Guid reporterId,
        TaskType type,
        string title,
        string? description,
        TaskPriority priority,
        DateTimeOffset createdAt)
        : base(id)
    {
        ProjectId = projectId;
        OrganizationId = organizationId;
        Number = number;
        ReporterId = reporterId;
        Type = type;
        Title = title;
        Description = description;
        Priority = priority;
        Status = TaskItemStatus.ToDo;
        CreatedAt = createdAt;
        UpdatedAt = createdAt;
    }

    public Guid ProjectId { get; }

    public Guid OrganizationId { get; }

    /// <summary>Sequential number inside the project; with the project key it forms the task key (e.g. <c>PRJ-12</c>).</summary>
    public int Number { get; }

    public Guid? SprintId { get; private set; }

    public Guid? EpicId { get; private set; }

    public Guid? AssigneeId { get; private set; }

    public Guid ReporterId { get; }

    public string Title { get; private set; }

    public string? Description { get; private set; }

    public TaskType Type { get; private set; }

    public TaskPriority Priority { get; private set; }

    public TaskItemStatus Status { get; private set; }

    public int? StoryPoints { get; private set; }

    public DateTimeOffset? DeletedAt { get; private set; }

    public DateTimeOffset CreatedAt { get; }

    public DateTimeOffset UpdatedAt { get; private set; }

    public bool IsDeleted => DeletedAt is not null;

    public IReadOnlyCollection<TaskLabel> Labels => _labels.AsReadOnly();

    /// <summary>Creates a task in the project and takes the next task number from it (RF-04, RF-06).</summary>
    public static Result<TaskItem> Create(
        Project project,
        Guid reporterId,
        TaskType type,
        string title,
        string? description,
        TaskPriority priority,
        DateTimeOffset now)
    {
        var canChange = project.EnsureCanBeChanged();
        if (canChange.IsFailure)
        {
            return canChange.Error;
        }

        if (reporterId == Guid.Empty)
        {
            return TaskErrors.UserRequired;
        }

        var details = ValidateDetails(type, title, description, priority);
        if (details.IsFailure)
        {
            return details.Error;
        }

        var (validTitle, validDescription) = details.Value;
        var number = project.AllocateTaskNumber();

        return new TaskItem(
            Guid.CreateVersion7(now),
            project.Id,
            project.OrganizationId,
            number,
            reporterId,
            type,
            validTitle,
            validDescription,
            priority,
            now);
    }

    public Result UpdateDetails(TaskType type, string title, string? description, TaskPriority priority, DateTimeOffset now)
    {
        var editable = EnsureEditable();
        if (editable.IsFailure)
        {
            return editable;
        }

        var details = ValidateDetails(type, title, description, priority);
        if (details.IsFailure)
        {
            return details.Error;
        }

        (Title, Description) = details.Value;
        Type = type;
        Priority = priority;
        return Touch(now);
    }

    public Result Estimate(int? storyPoints, DateTimeOffset now)
    {
        var editable = EnsureEditable();
        if (editable.IsFailure)
        {
            return editable;
        }

        if (storyPoints is < 0 or > MaxStoryPoints)
        {
            return TaskErrors.StoryPointsOutOfRange;
        }

        StoryPoints = storyPoints;
        return Touch(now);
    }

    /// <summary>Assigns the task, or unassigns it with <see langword="null"/>. Project membership is checked by the use case.</summary>
    public Result Assign(Guid? assigneeId, DateTimeOffset now)
    {
        var editable = EnsureEditable();
        if (editable.IsFailure)
        {
            return editable;
        }

        if (assigneeId == Guid.Empty)
        {
            return TaskErrors.UserRequired;
        }

        AssigneeId = assigneeId;
        return Touch(now);
    }

    /// <summary>Moves the task into a sprint, or back to the backlog with <see langword="null"/>.</summary>
    public Result MoveToSprint(Sprint? sprint, DateTimeOffset now)
    {
        var editable = EnsureEditable();
        if (editable.IsFailure)
        {
            return editable;
        }

        if (sprint is not null)
        {
            if (sprint.ProjectId != ProjectId)
            {
                return TaskErrors.SprintFromAnotherProject;
            }

            if (sprint.Status == SprintStatus.Completed)
            {
                return TaskErrors.SprintCompleted;
            }
        }

        SprintId = sprint?.Id;
        return Touch(now);
    }

    /// <summary>Links the task to an epic, or unlinks it with <see langword="null"/>.</summary>
    public Result SetEpic(Epic? epic, DateTimeOffset now)
    {
        var editable = EnsureEditable();
        if (editable.IsFailure)
        {
            return editable;
        }

        if (epic is not null)
        {
            if (epic.ProjectId != ProjectId)
            {
                return TaskErrors.EpicFromAnotherProject;
            }

            if (epic.Status == EpicStatus.Closed)
            {
                return TaskErrors.EpicClosed;
            }
        }

        EpicId = epic?.Id;
        return Touch(now);
    }

    public Result AddLabel(Label label, DateTimeOffset now)
    {
        var editable = EnsureEditable();
        if (editable.IsFailure)
        {
            return editable;
        }

        if (label.ProjectId != ProjectId)
        {
            return TaskErrors.LabelFromAnotherProject;
        }

        if (_labels.Exists(taskLabel => taskLabel.LabelId == label.Id))
        {
            return Result.Success();
        }

        _labels.Add(new TaskLabel(Id, label.Id));
        return Touch(now);
    }

    public Result RemoveLabel(Guid labelId, DateTimeOffset now)
    {
        var editable = EnsureEditable();
        if (editable.IsFailure)
        {
            return editable;
        }

        if (_labels.RemoveAll(taskLabel => taskLabel.LabelId == labelId) == 0)
        {
            return Result.Success();
        }

        return Touch(now);
    }

    /// <summary>Moves the task along the workflow ToDo → InProgress → Review → Done (RF-07).</summary>
    public Result ChangeStatus(TaskItemStatus status, DateTimeOffset now)
    {
        if (IsDeleted)
        {
            return TaskErrors.Deleted;
        }

        if (!Enum.IsDefined(status))
        {
            return TaskErrors.StatusInvalid;
        }

        if (!AllowedTransitions[Status].Contains(status))
        {
            return TaskErrors.InvalidTransition(Status, status);
        }

        Status = status;
        return Touch(now);
    }

    /// <summary>
    /// Reopens a done task back to InProgress (RF-08). Only a project manager may do this;
    /// that permission is checked by the authorization layer.
    /// </summary>
    public Result Reopen(DateTimeOffset now)
    {
        if (IsDeleted)
        {
            return TaskErrors.Deleted;
        }

        if (Status != TaskItemStatus.Done)
        {
            return TaskErrors.NotDone;
        }

        Status = TaskItemStatus.InProgress;
        return Touch(now);
    }

    /// <summary>Soft delete: the task is hidden but kept in the database.</summary>
    public Result Delete(DateTimeOffset now)
    {
        if (IsDeleted)
        {
            return TaskErrors.Deleted;
        }

        DeletedAt = now;
        return Touch(now);
    }

    /// <summary>A deleted or done task cannot be edited (RF-08).</summary>
    private Result EnsureEditable()
    {
        if (IsDeleted)
        {
            return TaskErrors.Deleted;
        }

        if (Status == TaskItemStatus.Done)
        {
            return TaskErrors.Closed;
        }

        return Result.Success();
    }

    private Result Touch(DateTimeOffset now)
    {
        UpdatedAt = now;
        return Result.Success();
    }

    private static Result<(string Title, string? Description)> ValidateDetails(
        TaskType type,
        string? title,
        string? description,
        TaskPriority priority)
    {
        if (!Enum.IsDefined(type))
        {
            return TaskErrors.TypeInvalid;
        }

        if (!Enum.IsDefined(priority))
        {
            return TaskErrors.PriorityInvalid;
        }

        var validTitle = Text.Required(title, TitleMaxLength, TaskErrors.TitleRequired, TaskErrors.TitleTooLong);
        if (validTitle.IsFailure)
        {
            return validTitle.Error;
        }

        var validDescription = Text.Optional(description, DescriptionMaxLength, TaskErrors.DescriptionTooLong);
        if (validDescription.IsFailure)
        {
            return validDescription.Error;
        }

        return (validTitle.Value, validDescription.Value);
    }
}
