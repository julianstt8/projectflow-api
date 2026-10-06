using ProjectFlow.Domain.Projects;
using ProjectFlow.Domain.Tasks;
using static ProjectFlow.Domain.Projects.ProjectPermission;
using static ProjectFlow.Domain.Tests.TestData;

namespace ProjectFlow.Domain.Tests.Projects;

/// <summary>
/// The PRD matrix written out by hand (not derived from the production code), so any change to
/// the permissions has to be made in both places on purpose.
/// </summary>
public class ProjectPermissionsTests
{
    private static readonly ProjectPermission[] ProjectManagerAllowed =
        [ViewProject, EditProject, ManageProjectMembers, ManageSprints, ManageEpics, CreateTasks, EditAnyTask, EditOwnTasks, ReopenTasks, Comment, ViewActivity];

    private static readonly ProjectPermission[] DeveloperAllowed = [ViewProject, CreateTasks, EditOwnTasks, Comment];

    private static readonly ProjectPermission[] ViewerAllowed = [ViewProject];

    public static TheoryData<ProjectRole, ProjectPermission, bool> Matrix()
    {
        var data = new TheoryData<ProjectRole, ProjectPermission, bool>();
        foreach (var permission in Enum.GetValues<ProjectPermission>())
        {
            data.Add(ProjectRole.ProjectManager, permission, ProjectManagerAllowed.Contains(permission));
            data.Add(ProjectRole.Developer, permission, DeveloperAllowed.Contains(permission));
            data.Add(ProjectRole.Viewer, permission, ViewerAllowed.Contains(permission));
        }

        return data;
    }

    [Theory]
    [MemberData(nameof(Matrix))]
    public void Project_roles_follow_the_PRD_matrix(ProjectRole role, ProjectPermission permission, bool allowed)
    {
        var access = new ProjectAccess(Guid.NewGuid(), IsOrganizationAdmin: false, role);

        Assert.Equal(allowed, access.Has(permission));
    }

    [Theory]
    [MemberData(nameof(AllPermissions))]
    public void Organization_admins_have_every_permission_without_a_project_role(ProjectPermission permission)
    {
        var access = new ProjectAccess(Guid.NewGuid(), IsOrganizationAdmin: true, ProjectRole: null);

        Assert.True(access.HasAccess);
        Assert.True(access.Has(permission));
    }

    [Theory]
    [MemberData(nameof(AllPermissions))]
    public void Users_without_a_project_role_have_no_permission(ProjectPermission permission)
    {
        var access = new ProjectAccess(Guid.NewGuid(), IsOrganizationAdmin: false, ProjectRole: null);

        Assert.False(access.HasAccess);
        Assert.False(access.Has(permission));
    }

    public static TheoryData<ProjectPermission> AllPermissions() => new(Enum.GetValues<ProjectPermission>());

    [Fact]
    public void Developers_edit_only_tasks_they_reported_or_are_assigned_to()
    {
        var project = CreateProject();
        var developer = Guid.NewGuid();
        var reported = TaskItem.Create(project, developer, TaskType.Task, "Reported", null, TaskPriority.Low, Now).Value;
        var assigned = CreateTask(project);
        assigned.Assign(developer, Now);
        var someoneElses = CreateTask(project);

        var access = new ProjectAccess(developer, IsOrganizationAdmin: false, ProjectRole.Developer);

        Assert.True(access.CanEditTask(reported));
        Assert.True(access.CanEditTask(assigned));
        Assert.False(access.CanEditTask(someoneElses));
    }

    [Theory]
    [InlineData(ProjectRole.ProjectManager, false, true)]
    [InlineData(ProjectRole.Viewer, false, false)]
    [InlineData(null, true, true)]
    public void Editing_any_task_depends_on_the_role(ProjectRole? role, bool isOrganizationAdmin, bool canEdit)
    {
        var task = CreateTask(CreateProject());

        var access = new ProjectAccess(Guid.NewGuid(), isOrganizationAdmin, role);

        Assert.Equal(canEdit, access.CanEditTask(task));
    }
}
