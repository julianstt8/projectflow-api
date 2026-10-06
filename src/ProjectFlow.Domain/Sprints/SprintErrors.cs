using ProjectFlow.Domain.Common;

namespace ProjectFlow.Domain.Sprints;

public static class SprintErrors
{
    public static readonly Error NameRequired =
        Error.Validation("Sprint.NameRequired", "The sprint name is required.");

    public static readonly Error NameTooLong =
        Error.Validation("Sprint.NameTooLong", $"The sprint name cannot exceed {Sprint.NameMaxLength} characters.");

    public static readonly Error GoalTooLong =
        Error.Validation("Sprint.GoalTooLong", $"The sprint goal cannot exceed {Sprint.GoalMaxLength} characters.");

    public static readonly Error EndBeforeStart =
        Error.Validation("Sprint.EndBeforeStart", "The end date cannot be earlier than the start date.");

    public static readonly Error NotPlanned =
        Error.BusinessRule("Sprint.NotPlanned", "Only a planned sprint can be started.");

    public static readonly Error NotActive =
        Error.BusinessRule("Sprint.NotActive", "Only an active sprint can be completed.");

    public static readonly Error Completed =
        Error.BusinessRule("Sprint.Completed", "A completed sprint cannot be changed.");

    public static readonly Error AnotherSprintActive =
        Error.Conflict("Sprint.AnotherSprintActive", "The project already has an active sprint.");
}
