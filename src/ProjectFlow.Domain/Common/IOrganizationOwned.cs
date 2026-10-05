namespace ProjectFlow.Domain.Common;

/// <summary>Entity that belongs to exactly one organization; its data must never be visible to another one.</summary>
public interface IOrganizationOwned
{
    Guid OrganizationId { get; }
}
