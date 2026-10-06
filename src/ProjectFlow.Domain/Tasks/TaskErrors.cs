using ProjectFlow.Domain.Common;

namespace ProjectFlow.Domain.Tasks;

public static class TaskErrors
{
    public static readonly Error TitleRequired =
        Error.Validation("Task.TitleRequired", "The task title is required.");

    public static readonly Error TitleTooLong =
        Error.Validation("Task.TitleTooLong", $"The task title cannot exceed {TaskItem.TitleMaxLength} characters.");

    public static readonly Error DescriptionTooLong =
        Error.Validation("Task.DescriptionTooLong", $"The task description cannot exceed {TaskItem.DescriptionMaxLength} characters.");

    public static readonly Error TypeInvalid =
        Error.Validation("Task.TypeInvalid", "The task type is not valid.");

    public static readonly Error PriorityInvalid =
        Error.Validation("Task.PriorityInvalid", "The task priority is not valid.");

    public static readonly Error StatusInvalid =
        Error.Validation("Task.StatusInvalid", "The task status is not valid.");

    public static readonly Error StoryPointsOutOfRange =
        Error.Validation("Task.StoryPointsOutOfRange", $"Story points must be between 0 and {TaskItem.MaxStoryPoints}.");

    public static readonly Error UserRequired =
        Error.Validation("Task.UserRequired", "A valid user id is required.");

    public static readonly Error Closed =
        Error.BusinessRule("Task.Closed", "A done task cannot be changed until it is reopened.");

    public static readonly Error NotDone =
        Error.BusinessRule("Task.NotDone", "Only a done task can be reopened.");

    public static readonly Error Deleted =
        Error.BusinessRule("Task.Deleted", "The task has been deleted.");

    public static readonly Error SprintFromAnotherProject =
        Error.Validation("Task.SprintFromAnotherProject", "The sprint belongs to another project.");

    public static readonly Error SprintCompleted =
        Error.BusinessRule("Task.SprintCompleted", "Tasks cannot be moved into a completed sprint.");

    public static readonly Error EpicFromAnotherProject =
        Error.Validation("Task.EpicFromAnotherProject", "The epic belongs to another project.");

    public static readonly Error EpicClosed =
        Error.BusinessRule("Task.EpicClosed", "Tasks cannot be added to a closed epic.");

    public static readonly Error LabelFromAnotherProject =
        Error.Validation("Task.LabelFromAnotherProject", "The label belongs to another project.");

    public static Error InvalidTransition(TaskItemStatus from, TaskItemStatus to) =>
        Error.BusinessRule("Task.InvalidTransition", $"A task cannot move from {from} to {to}.");
}
