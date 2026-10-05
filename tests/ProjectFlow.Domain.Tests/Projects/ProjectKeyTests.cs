using ProjectFlow.Domain.Projects;

namespace ProjectFlow.Domain.Tests.Projects;

public class ProjectKeyTests
{
    [Theory]
    [InlineData("PRJ", "PRJ")]
    [InlineData(" prj ", "PRJ")]
    [InlineData("WEB2", "WEB2")]
    [InlineData("AB", "AB")]
    [InlineData("ABCDEFGHIJ", "ABCDEFGHIJ")]
    public void Create_normalizes_valid_keys(string value, string expected)
    {
        var result = ProjectKey.Create(value);

        Assert.True(result.IsSuccess);
        Assert.Equal(expected, result.Value.Value);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData(" ")]
    public void Create_requires_a_value(string? value)
    {
        Assert.Equal(ProjectErrors.KeyRequired, ProjectKey.Create(value).Error);
    }

    [Theory]
    [InlineData("A")]
    [InlineData("ABCDEFGHIJK")]
    [InlineData("2WEB")]
    [InlineData("PR-J")]
    [InlineData("PR J")]
    public void Create_rejects_invalid_keys(string value)
    {
        Assert.Equal(ProjectErrors.KeyInvalid, ProjectKey.Create(value).Error);
    }
}
