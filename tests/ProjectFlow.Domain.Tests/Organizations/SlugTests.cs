using ProjectFlow.Domain.Organizations;

namespace ProjectFlow.Domain.Tests.Organizations;

public class SlugTests
{
    [Theory]
    [InlineData("acme", "acme")]
    [InlineData(" Acme-Software ", "acme-software")]
    [InlineData("team-42", "team-42")]
    public void Create_normalizes_valid_slugs(string value, string expected)
    {
        var result = Slug.Create(value);

        Assert.True(result.IsSuccess);
        Assert.Equal(expected, result.Value.Value);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("  ")]
    public void Create_requires_a_value(string? value)
    {
        Assert.Equal(OrganizationErrors.SlugRequired, Slug.Create(value).Error);
    }

    [Theory]
    [InlineData("ab")]
    [InlineData("-acme")]
    [InlineData("acme-")]
    [InlineData("acme--software")]
    [InlineData("acme_software")]
    [InlineData("acme software")]
    [InlineData("acmé")]
    public void Create_rejects_invalid_slugs(string value)
    {
        Assert.Equal(OrganizationErrors.SlugInvalid, Slug.Create(value).Error);
    }

    [Fact]
    public void Create_rejects_too_long_slugs()
    {
        Assert.Equal(OrganizationErrors.SlugInvalid, Slug.Create(new string('a', Slug.MaxLength + 1)).Error);
    }
}
