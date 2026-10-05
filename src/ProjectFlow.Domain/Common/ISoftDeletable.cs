namespace ProjectFlow.Domain.Common;

/// <summary>Entity that is hidden instead of removed when deleted (only projects and tasks, per the PRD).</summary>
public interface ISoftDeletable
{
    DateTimeOffset? DeletedAt { get; }
}
