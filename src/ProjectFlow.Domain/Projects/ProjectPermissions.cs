using System.Collections.Frozen;
using ProjectFlow.Domain.Tasks;

namespace ProjectFlow.Domain.Projects;

/// <summary>Actions on a project and its content (PRD section 4).</summary>
public enum ProjectPermission
{
    ViewProject = 1,
    EditProject,
    ManageProjectMembers,
    ManageSprints,
    ManageEpics,
    CreateTasks,

    /// <summary>Edit and move any task of the project.</summary>
    EditAnyTask,

    /// <summary>Edit and move only tasks the user reported or is assigned to.</summary>
    EditOwnTasks,

    /// <summary>Reopen a done task (RF-08).</summary>
    ReopenTasks,
    Comment,
    ViewActivity,
}

/// <summary>Permissions as code: the role → permission matrix of the PRD.</summary>
public static class ProjectPermissions
{
    public static readonly IReadOnlySet<ProjectPermission> All = Enum.GetValues<ProjectPermission>().ToFrozenSet();

    private static readonly FrozenDictionary<ProjectRole, FrozenSet<ProjectPermission>> Matrix =
        new Dictionary<ProjectRole, FrozenSet<ProjectPermission>>
        {
            [ProjectRole.ProjectManager] = All.ToFrozenSet(),
            [ProjectRole.Developer] = new[]
            {
                ProjectPermission.ViewProject,
                ProjectPermission.CreateTasks,
                ProjectPermission.EditOwnTasks,
                ProjectPermission.Comment,
            }.ToFrozenSet(),
            [ProjectRole.Viewer] = new[] { ProjectPermission.ViewProject }.ToFrozenSet(),
        }.ToFrozenDictionary();

    public static IReadOnlySet<ProjectPermission> Of(ProjectRole role) => Matrix[role];
}

/// <summary>
/// What a user can do in one project. Organization admins have every permission in every project of
/// their organization (PRD 4.1); everyone else needs a project role.
/// </summary>
public sealed record ProjectAccess(Guid UserId, bool IsOrganizationAdmin, ProjectRole? ProjectRole)
{
    /// <summary>Whether the user can see the project at all.</summary>
    public bool HasAccess => IsOrganizationAdmin || ProjectRole is not null;

    public bool Has(ProjectPermission permission) =>
        IsOrganizationAdmin || (ProjectRole is { } role && ProjectPermissions.Of(role).Contains(permission));

    /// <summary>Resource-based rule: developers can edit and move only tasks they reported or are assigned to.</summary>
    public bool CanEditTask(TaskItem task) =>
        Has(ProjectPermission.EditAnyTask)
        || (Has(ProjectPermission.EditOwnTasks) && (task.ReporterId == UserId || task.AssigneeId == UserId));
}
