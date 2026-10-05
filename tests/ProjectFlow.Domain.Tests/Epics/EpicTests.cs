using ProjectFlow.Domain.Epics;
using ProjectFlow.Domain.Projects;
using static ProjectFlow.Domain.Tests.TestData;

namespace ProjectFlow.Domain.Tests.Epics;

public class EpicTests
{
    [Fact]
    public void Create_copies_project_and_organization_and_starts_open()
    {
        var project = CreateProject();

        var epic = Epic.Create(project, " Authentication ", null, Now).Value;

        Assert.Equal(project.Id, epic.ProjectId);
        Assert.Equal(project.OrganizationId, epic.OrganizationId);
        Assert.Equal("Authentication", epic.Name);
        Assert.Equal(EpicStatus.Open, epic.Status);
    }

    [Fact]
    public void Create_validates_input_and_project_state()
    {
        var project = CreateProject();

        Assert.Equal(EpicErrors.NameRequired, Epic.Create(project, "", null, Now).Error);
        project.Delete(Now);
        Assert.Equal(ProjectErrors.Deleted, Epic.Create(project, "Auth", null, Now).Error);
    }

    [Fact]
    public void Close_and_reopen_toggle_the_status()
    {
        var epic = Epic.Create(CreateProject(), "Auth", null, Now).Value;

        Assert.Equal(EpicErrors.AlreadyOpen, epic.Reopen().Error);
        Assert.True(epic.Close().IsSuccess);
        Assert.Equal(EpicErrors.AlreadyClosed, epic.Close().Error);
        Assert.True(epic.Reopen().IsSuccess);
        Assert.Equal(EpicStatus.Open, epic.Status);
    }
}
