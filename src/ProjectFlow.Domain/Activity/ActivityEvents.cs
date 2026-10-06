using ProjectFlow.Domain.Common;

namespace ProjectFlow.Domain.Activity;

public static class ActivityEntityTypes
{
    public const string Project = "Project";
    public const string Sprint = "Sprint";
    public const string Epic = "Epic";
    public const string Task = "Task";
    public const string Comment = "Comment";
}

// ---------- Tasks ----------

public sealed record TaskCreated(Guid OrganizationId, Guid ProjectId, Guid TaskId, string Title)
    : ProjectActivityEvent(OrganizationId, ProjectId, ActivityEntityTypes.Task, TaskId)
{
    public override string Action => "Created";

    public override string? NewValue => Title;
}

public sealed record TaskTitleChanged(Guid OrganizationId, Guid ProjectId, Guid TaskId, string From, string To)
    : ProjectActivityEvent(OrganizationId, ProjectId, ActivityEntityTypes.Task, TaskId)
{
    public override string Action => "TitleChanged";

    public override string? OldValue => From;

    public override string? NewValue => To;
}

public sealed record TaskPriorityChanged(Guid OrganizationId, Guid ProjectId, Guid TaskId, string From, string To)
    : ProjectActivityEvent(OrganizationId, ProjectId, ActivityEntityTypes.Task, TaskId)
{
    public override string Action => "PriorityChanged";

    public override string? OldValue => From;

    public override string? NewValue => To;
}

public sealed record TaskStoryPointsChanged(Guid OrganizationId, Guid ProjectId, Guid TaskId, int? From, int? To)
    : ProjectActivityEvent(OrganizationId, ProjectId, ActivityEntityTypes.Task, TaskId)
{
    public override string Action => "StoryPointsChanged";

    public override string? OldValue => From?.ToString(System.Globalization.CultureInfo.InvariantCulture);

    public override string? NewValue => To?.ToString(System.Globalization.CultureInfo.InvariantCulture);
}

public sealed record TaskStatusChanged(Guid OrganizationId, Guid ProjectId, Guid TaskId, string From, string To)
    : ProjectActivityEvent(OrganizationId, ProjectId, ActivityEntityTypes.Task, TaskId)
{
    public override string Action => "StatusChanged";

    public override string? OldValue => From;

    public override string? NewValue => To;
}

public sealed record TaskReopened(Guid OrganizationId, Guid ProjectId, Guid TaskId)
    : ProjectActivityEvent(OrganizationId, ProjectId, ActivityEntityTypes.Task, TaskId)
{
    public override string Action => "Reopened";
}

/// <summary>Assignee, sprint or epic of a task changed; values are ids (null = none / backlog).</summary>
public sealed record TaskReferenceChanged(
    Guid OrganizationId,
    Guid ProjectId,
    Guid TaskId,
    string Reference,
    Guid? From,
    Guid? To)
    : ProjectActivityEvent(OrganizationId, ProjectId, ActivityEntityTypes.Task, TaskId)
{
    public const string Assignee = "Assignee";
    public const string Sprint = "Sprint";
    public const string Epic = "Epic";

    public override string Action => $"{Reference}Changed";

    public override string? OldValue => From?.ToString();

    public override string? NewValue => To?.ToString();
}

public sealed record TaskLabelChanged(Guid OrganizationId, Guid ProjectId, Guid TaskId, Guid LabelId, bool Added)
    : ProjectActivityEvent(OrganizationId, ProjectId, ActivityEntityTypes.Task, TaskId)
{
    public override string Action => Added ? "LabelAdded" : "LabelRemoved";

    public override string? NewValue => LabelId.ToString();
}

public sealed record TaskDeleted(Guid OrganizationId, Guid ProjectId, Guid TaskId)
    : ProjectActivityEvent(OrganizationId, ProjectId, ActivityEntityTypes.Task, TaskId)
{
    public override string Action => "Deleted";
}

public sealed record CommentAdded(Guid OrganizationId, Guid ProjectId, Guid TaskId, Guid CommentId)
    : ProjectActivityEvent(OrganizationId, ProjectId, ActivityEntityTypes.Task, TaskId)
{
    public override string Action => "CommentAdded";

    public override string? NewValue => CommentId.ToString();
}

// ---------- Sprints, epics, projects ----------

/// <summary>A lifecycle step of a sprint, epic or project (Created, Started, Completed, Closed, Archived...).</summary>
public sealed record LifecycleChanged(
    Guid OrganizationId,
    Guid ProjectId,
    string Type,
    Guid Id,
    string Step,
    string? Name)
    : ProjectActivityEvent(OrganizationId, ProjectId, Type, Id)
{
    public override string Action => Step;

    public override string? NewValue => Name;
}

public sealed record ProjectMemberChanged(
    Guid OrganizationId,
    Guid ProjectId,
    Guid UserId,
    string? FromRole,
    string? ToRole)
    : ProjectActivityEvent(OrganizationId, ProjectId, ActivityEntityTypes.Project, ProjectId)
{
    public override string Action => (FromRole, ToRole) switch
    {
        (null, _) => "MemberAdded",
        (_, null) => "MemberRemoved",
        _ => "MemberRoleChanged",
    };

    public override string? OldValue => FromRole is null ? null : $"{UserId}:{FromRole}";

    public override string? NewValue => ToRole is null ? null : $"{UserId}:{ToRole}";
}
