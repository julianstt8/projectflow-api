using ProjectFlow.Domain.Common;

namespace ProjectFlow.Domain.Activity;

/// <summary>
/// One immutable entry of a project's activity log (RF-10): who changed what, when, from which value to which.
/// It has no methods that change it, and the database rejects updates and deletes on its table.
/// </summary>
public sealed class ActivityLog : Entity, IOrganizationOwned
{
    public const int EntityTypeMaxLength = 30;
    public const int ActionMaxLength = 50;
    public const int ValueMaxLength = 500;

    private ActivityLog(
        Guid id,
        Guid organizationId,
        Guid projectId,
        Guid actorId,
        string entityType,
        Guid entityId,
        string action,
        string? oldValue,
        string? newValue,
        DateTimeOffset createdAt)
        : base(id)
    {
        OrganizationId = organizationId;
        ProjectId = projectId;
        ActorId = actorId;
        EntityType = entityType;
        EntityId = entityId;
        Action = action;
        OldValue = oldValue;
        NewValue = newValue;
        CreatedAt = createdAt;
    }

    public Guid OrganizationId { get; }

    public Guid ProjectId { get; }

    public Guid ActorId { get; }

    public string EntityType { get; }

    public Guid EntityId { get; }

    public string Action { get; }

    public string? OldValue { get; }

    public string? NewValue { get; }

    public DateTimeOffset CreatedAt { get; }

    public static ActivityLog Record(ProjectActivityEvent activity, Guid actorId, DateTimeOffset now) =>
        new(
            Guid.CreateVersion7(now),
            activity.OrganizationId,
            activity.ProjectId,
            actorId,
            activity.EntityType,
            activity.EntityId,
            activity.Action,
            Truncate(activity.OldValue),
            Truncate(activity.NewValue),
            now);

    private static string? Truncate(string? value) =>
        value is { Length: > ValueMaxLength } ? value[..ValueMaxLength] : value;
}
