using ProjectFlow.Domain.Projects;
using ProjectFlow.Domain.Sprints;
using static ProjectFlow.Domain.Tests.TestData;

namespace ProjectFlow.Domain.Tests.Sprints;

public class SprintTests
{
    private static Sprint CreateSprint(Project project, DateOnly? start = null, DateOnly? end = null) =>
        Sprint.Create(project, "Sprint 1", "Ship login", start, end, Now).Value;

    [Fact]
    public void Create_copies_project_and_organization_and_starts_planned()
    {
        var project = CreateProject();

        var sprint = CreateSprint(project);

        Assert.Equal(project.Id, sprint.ProjectId);
        Assert.Equal(project.OrganizationId, sprint.OrganizationId);
        Assert.Equal(SprintStatus.Planned, sprint.Status);
    }

    [Fact]
    public void Create_validates_name_and_dates()
    {
        var project = CreateProject();

        Assert.Equal(SprintErrors.NameRequired, Sprint.Create(project, " ", null, null, null, Now).Error);
        Assert.Equal(
            SprintErrors.EndBeforeStart,
            Sprint.Create(project, "S1", null, Today, Today.AddDays(-1), Now).Error);
    }

    [Fact]
    public void Create_is_rejected_in_an_archived_project()
    {
        var project = CreateProject();
        project.Archive();

        Assert.Equal(ProjectErrors.Archived, Sprint.Create(project, "S1", null, null, null, Now).Error);
    }

    [Fact]
    public void Start_activates_a_planned_sprint_and_sets_the_start_date()
    {
        var sprint = CreateSprint(CreateProject());

        var result = sprint.Start([], Today);

        Assert.True(result.IsSuccess);
        Assert.Equal(SprintStatus.Active, sprint.Status);
        Assert.Equal(Today, sprint.StartDate);
    }

    [Fact]
    public void Start_is_rejected_when_another_sprint_is_active()
    {
        var project = CreateProject();
        var active = CreateSprint(project);
        active.Start([], Today);
        var next = CreateSprint(project);

        var result = next.Start([active, next], Today);

        Assert.Equal(SprintErrors.AnotherSprintActive, result.Error);
        Assert.Equal(SprintStatus.Planned, next.Status);
    }

    [Fact]
    public void Start_ignores_active_sprints_of_other_projects()
    {
        var otherProjectSprint = CreateSprint(CreateProject("OTH"));
        otherProjectSprint.Start([], Today);
        var sprint = CreateSprint(CreateProject());

        Assert.True(sprint.Start([otherProjectSprint], Today).IsSuccess);
    }

    [Fact]
    public void Start_is_rejected_when_the_end_date_is_before_today()
    {
        var sprint = CreateSprint(CreateProject(), end: Today.AddDays(-1));

        Assert.Equal(SprintErrors.EndBeforeStart, sprint.Start([], Today).Error);
    }

    [Fact]
    public void Only_planned_sprints_can_start_and_only_active_ones_can_complete()
    {
        var sprint = CreateSprint(CreateProject());

        Assert.Equal(SprintErrors.NotActive, sprint.Complete(Today).Error);
        sprint.Start([], Today);
        Assert.Equal(SprintErrors.NotPlanned, sprint.Start([], Today).Error);
    }

    [Fact]
    public void Complete_closes_the_sprint_and_freezes_it()
    {
        var sprint = CreateSprint(CreateProject());
        sprint.Start([], Today);

        Assert.True(sprint.Complete(Today.AddDays(14)).IsSuccess);
        Assert.Equal(SprintStatus.Completed, sprint.Status);
        Assert.Equal(Today.AddDays(14), sprint.EndDate);
        Assert.Equal(SprintErrors.Completed, sprint.UpdateDetails("Renamed", null, null, null).Error);
    }
}
