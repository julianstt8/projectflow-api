namespace ProjectFlow.Domain.Projects;

public sealed class ProjectMember
{
    internal ProjectMember(Guid projectId, Guid userId, ProjectRole role)
    {
        ProjectId = projectId;
        UserId = userId;
        Role = role;
    }

    public Guid ProjectId { get; }

    public Guid UserId { get; }

    public ProjectRole Role { get; private set; }

    internal void ChangeRole(ProjectRole role) => Role = role;
}
