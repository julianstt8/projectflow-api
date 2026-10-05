using ProjectFlow.Domain.Common;
using ProjectFlow.Domain.Projects;

namespace ProjectFlow.Domain.Sprints;

public sealed class Sprint : Entity
{
    public const int NameMaxLength = 100;
    public const int GoalMaxLength = 500;

    private Sprint(Guid id, Guid projectId, Guid organizationId, string name, string? goal, DateOnly? startDate, DateOnly? endDate)
        : base(id)
    {
        ProjectId = projectId;
        OrganizationId = organizationId;
        Name = name;
        Goal = goal;
        StartDate = startDate;
        EndDate = endDate;
        Status = SprintStatus.Planned;
    }

    public Guid ProjectId { get; }

    public Guid OrganizationId { get; }

    public string Name { get; private set; }

    public string? Goal { get; private set; }

    public DateOnly? StartDate { get; private set; }

    public DateOnly? EndDate { get; private set; }

    public SprintStatus Status { get; private set; }

    public static Result<Sprint> Create(
        Project project,
        string name,
        string? goal,
        DateOnly? startDate,
        DateOnly? endDate,
        DateTimeOffset now)
    {
        var canChange = project.EnsureCanBeChanged();
        if (canChange.IsFailure)
        {
            return canChange.Error;
        }

        var details = ValidateDetails(name, goal, startDate, endDate);
        if (details.IsFailure)
        {
            return details.Error;
        }

        var (validName, validGoal) = details.Value;
        return new Sprint(Guid.CreateVersion7(now), project.Id, project.OrganizationId, validName, validGoal, startDate, endDate);
    }

    public Result UpdateDetails(string name, string? goal, DateOnly? startDate, DateOnly? endDate)
    {
        if (Status == SprintStatus.Completed)
        {
            return SprintErrors.Completed;
        }

        var details = ValidateDetails(name, goal, startDate, endDate);
        if (details.IsFailure)
        {
            return details.Error;
        }

        (Name, Goal) = details.Value;
        StartDate = startDate;
        EndDate = endDate;
        return Result.Success();
    }

    /// <summary>
    /// Starts the sprint (RF-05). <paramref name="projectSprints"/> must contain the other sprints of the project;
    /// the database also enforces a single active sprint with a partial unique index.
    /// </summary>
    public Result Start(IEnumerable<Sprint> projectSprints, DateOnly today)
    {
        if (Status != SprintStatus.Planned)
        {
            return SprintErrors.NotPlanned;
        }

        if (projectSprints.Any(sprint => sprint.ProjectId == ProjectId && sprint.Id != Id && sprint.Status == SprintStatus.Active))
        {
            return SprintErrors.AnotherSprintActive;
        }

        var startDate = StartDate ?? today;
        if (EndDate < startDate)
        {
            return SprintErrors.EndBeforeStart;
        }

        StartDate = startDate;
        Status = SprintStatus.Active;
        return Result.Success();
    }

    public Result Complete(DateOnly today)
    {
        if (Status != SprintStatus.Active)
        {
            return SprintErrors.NotActive;
        }

        EndDate ??= today;
        Status = SprintStatus.Completed;
        return Result.Success();
    }

    private static Result<(string Name, string? Goal)> ValidateDetails(
        string? name,
        string? goal,
        DateOnly? startDate,
        DateOnly? endDate)
    {
        var validName = Text.Required(name, NameMaxLength, SprintErrors.NameRequired, SprintErrors.NameTooLong);
        if (validName.IsFailure)
        {
            return validName.Error;
        }

        var validGoal = Text.Optional(goal, GoalMaxLength, SprintErrors.GoalTooLong);
        if (validGoal.IsFailure)
        {
            return validGoal.Error;
        }

        if (startDate is not null && endDate < startDate)
        {
            return SprintErrors.EndBeforeStart;
        }

        return (validName.Value, validGoal.Value);
    }
}
