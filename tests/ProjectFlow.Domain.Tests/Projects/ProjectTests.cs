using ProjectFlow.Domain.Projects;
using static ProjectFlow.Domain.Tests.TestData;

namespace ProjectFlow.Domain.Tests.Projects;

public class ProjectTests
{
    private static readonly ProjectKey Key = ProjectKey.Create("PRJ").Value;

    [Fact]
    public void Create_makes_the_creator_the_first_project_manager()
    {
        var creator = Guid.NewGuid();

        var result = Project.Create(OrganizationId, Key, " ProjectFlow ", "  ", creator, Now);

        Assert.True(result.IsSuccess);
        var project = result.Value;
        Assert.Equal(OrganizationId, project.OrganizationId);
        Assert.Equal("ProjectFlow", project.Name);
        Assert.Null(project.Description);
        Assert.Equal(1, project.NextTaskNumber);
        Assert.False(project.IsArchived);
        Assert.False(project.IsDeleted);
        Assert.Equal(ProjectRole.ProjectManager, project.GetMemberRole(creator));
    }

    [Fact]
    public void Create_validates_its_input()
    {
        Assert.Equal(ProjectErrors.OrganizationRequired, Project.Create(Guid.Empty, Key, "P", null, Guid.NewGuid(), Now).Error);
        Assert.Equal(ProjectErrors.UserRequired, Project.Create(OrganizationId, Key, "P", null, Guid.Empty, Now).Error);
        Assert.Equal(ProjectErrors.NameRequired, Project.Create(OrganizationId, Key, " ", null, Guid.NewGuid(), Now).Error);
        Assert.Equal(
            ProjectErrors.NameTooLong,
            Project.Create(OrganizationId, Key, new string('a', Project.NameMaxLength + 1), null, Guid.NewGuid(), Now).Error);
        Assert.Equal(
            ProjectErrors.DescriptionTooLong,
            Project.Create(OrganizationId, Key, "P", new string('a', Project.DescriptionMaxLength + 1), Guid.NewGuid(), Now).Error);
    }

    [Fact]
    public void FormatTaskKey_combines_project_key_and_number()
    {
        Assert.Equal("PRJ-12", CreateProject().FormatTaskKey(12));
    }

    [Fact]
    public void Archived_project_rejects_changes_until_unarchived()
    {
        var project = CreateProject();

        Assert.True(project.Archive().IsSuccess);
        Assert.Equal(ProjectErrors.Archived, project.UpdateDetails("New name", null).Error);
        Assert.Equal(ProjectErrors.Archived, project.AddMember(Guid.NewGuid(), ProjectRole.Developer).Error);

        Assert.True(project.Unarchive().IsSuccess);
        Assert.True(project.UpdateDetails("New name", "Description").IsSuccess);
        Assert.Equal("New name", project.Name);
        Assert.Equal("Description", project.Description);
    }

    [Fact]
    public void Unarchive_requires_an_archived_project()
    {
        Assert.Equal(ProjectErrors.NotArchived, CreateProject().Unarchive().Error);
    }

    [Fact]
    public void Deleted_project_cannot_be_changed_or_deleted_again()
    {
        var project = CreateProject();

        Assert.True(project.Delete(Now).IsSuccess);
        Assert.True(project.IsDeleted);
        Assert.Equal(ProjectErrors.Deleted, project.Delete(Now).Error);
        Assert.Equal(ProjectErrors.Deleted, project.Archive().Error);
        Assert.Equal(ProjectErrors.Deleted, project.Unarchive().Error);
    }

    [Fact]
    public void AddMember_rejects_duplicates_and_invalid_roles()
    {
        var creator = Guid.NewGuid();
        var project = CreateProject(creatorUserId: creator);

        Assert.Equal(ProjectErrors.MemberAlreadyExists, project.AddMember(creator, ProjectRole.Developer).Error);
        Assert.Equal(ProjectErrors.RoleInvalid, project.AddMember(Guid.NewGuid(), (ProjectRole)99).Error);
        Assert.Equal(ProjectErrors.UserRequired, project.AddMember(Guid.Empty, ProjectRole.Developer).Error);
    }

    [Fact]
    public void Last_project_manager_cannot_be_demoted_or_removed()
    {
        var creator = Guid.NewGuid();
        var project = CreateProject(creatorUserId: creator);
        project.AddMember(Guid.NewGuid(), ProjectRole.Developer);

        Assert.Equal(ProjectErrors.LastProjectManager, project.ChangeMemberRole(creator, ProjectRole.Viewer).Error);
        Assert.Equal(ProjectErrors.LastProjectManager, project.RemoveMember(creator).Error);
        Assert.Equal(ProjectRole.ProjectManager, project.GetMemberRole(creator));
    }

    [Fact]
    public void Project_manager_can_step_down_when_another_one_exists()
    {
        var creator = Guid.NewGuid();
        var other = Guid.NewGuid();
        var project = CreateProject(creatorUserId: creator);
        project.AddMember(other, ProjectRole.ProjectManager);

        Assert.True(project.ChangeMemberRole(creator, ProjectRole.Developer).IsSuccess);
        Assert.Equal(ProjectRole.Developer, project.GetMemberRole(creator));
        Assert.True(project.RemoveMember(creator).IsSuccess);
        Assert.Null(project.GetMemberRole(creator));
    }

    [Fact]
    public void Member_operations_require_an_existing_member()
    {
        var project = CreateProject();

        Assert.Equal(ProjectErrors.MemberNotFound, project.ChangeMemberRole(Guid.NewGuid(), ProjectRole.Viewer).Error);
        Assert.Equal(ProjectErrors.MemberNotFound, project.RemoveMember(Guid.NewGuid()).Error);
    }
}
