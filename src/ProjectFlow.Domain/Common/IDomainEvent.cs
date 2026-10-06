namespace ProjectFlow.Domain.Common;

/// <summary>Something that happened in the domain, raised by an entity and handled when the change is saved.</summary>
public interface IDomainEvent;

/// <summary>
/// A change that belongs in the project's activity log (RF-10): what changed, from which value to which.
/// Who did it and when are added when the change is saved (in the same transaction).
/// </summary>
public abstract record ProjectActivityEvent(Guid OrganizationId, Guid ProjectId, string EntityType, Guid EntityId) : IDomainEvent
{
    public abstract string Action { get; }

    public virtual string? OldValue => null;

    public virtual string? NewValue => null;
}
