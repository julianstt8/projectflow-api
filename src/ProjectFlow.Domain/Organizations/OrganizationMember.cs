namespace ProjectFlow.Domain.Organizations;

public sealed class OrganizationMember
{
    internal OrganizationMember(Guid organizationId, Guid userId, OrganizationRole role, DateTimeOffset joinedAt)
    {
        OrganizationId = organizationId;
        UserId = userId;
        Role = role;
        JoinedAt = joinedAt;
    }

    public Guid OrganizationId { get; }

    public Guid UserId { get; }

    public OrganizationRole Role { get; private set; }

    public DateTimeOffset JoinedAt { get; }

    internal void ChangeRole(OrganizationRole role) => Role = role;
}
