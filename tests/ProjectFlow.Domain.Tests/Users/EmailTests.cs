using ProjectFlow.Domain.Users;

namespace ProjectFlow.Domain.Tests.Users;

public class EmailTests
{
    [Fact]
    public void Create_trims_and_lowercases()
    {
        var result = Email.Create("  Ana.Dev@Example.COM ");

        Assert.True(result.IsSuccess);
        Assert.Equal("ana.dev@example.com", result.Value.Value);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public void Create_requires_a_value(string? value)
    {
        var result = Email.Create(value);

        Assert.Equal(UserErrors.EmailRequired, result.Error);
    }

    [Theory]
    [InlineData("plainaddress")]
    [InlineData("missing-at.example.com")]
    [InlineData("missing-domain@")]
    [InlineData("no-tld@example")]
    [InlineData("with space@example.com")]
    public void Create_rejects_invalid_format(string value)
    {
        var result = Email.Create(value);

        Assert.Equal(UserErrors.EmailInvalid, result.Error);
    }

    [Fact]
    public void Create_rejects_too_long_addresses()
    {
        var value = new string('a', Email.MaxLength) + "@example.com";

        var result = Email.Create(value);

        Assert.Equal(UserErrors.EmailInvalid, result.Error);
    }

    [Fact]
    public void Emails_with_the_same_value_are_equal()
    {
        Assert.Equal(Email.Create("dev@example.com").Value, Email.Create("DEV@example.com").Value);
    }
}
