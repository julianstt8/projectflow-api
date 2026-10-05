using ProjectFlow.Domain.Labels;
using static ProjectFlow.Domain.Tests.TestData;

namespace ProjectFlow.Domain.Tests.Labels;

public class LabelTests
{
    [Fact]
    public void Create_normalizes_the_color()
    {
        var project = CreateProject();

        var label = Label.Create(project, " backend ", "#1d76db", Now).Value;

        Assert.Equal(project.Id, label.ProjectId);
        Assert.Equal(project.OrganizationId, label.OrganizationId);
        Assert.Equal("backend", label.Name);
        Assert.Equal("#1D76DB", label.Color);
    }

    [Theory]
    [InlineData("")]
    [InlineData("1D76DB")]
    [InlineData("#1D76D")]
    [InlineData("#GGGGGG")]
    [InlineData("blue")]
    public void Create_rejects_invalid_colors(string color)
    {
        Assert.Equal(LabelErrors.ColorInvalid, Label.Create(CreateProject(), "backend", color, Now).Error);
    }

    [Fact]
    public void Update_changes_name_and_color()
    {
        var label = Label.Create(CreateProject(), "backend", "#1D76DB", Now).Value;

        Assert.Equal(LabelErrors.NameRequired, label.Update(" ", "#000000").Error);
        Assert.True(label.Update("api", "#0e8a16").IsSuccess);
        Assert.Equal("api", label.Name);
        Assert.Equal("#0E8A16", label.Color);
    }
}
