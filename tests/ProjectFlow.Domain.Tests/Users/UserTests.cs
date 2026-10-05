using ProjectFlow.Domain.Users;

namespace ProjectFlow.Domain.Tests.Users;

public class UserTests
{
    private static readonly DateTimeOffset Now = new(2026, 10, 5, 12, 0, 0, TimeSpan.Zero);
    private static readonly Email ValidEmail = Email.Create("dev@example.com").Value;
    private const string ValidHash = "hashed-password";

    [Fact]
    public void Create_returns_an_active_user()
    {
        var result = User.Create(ValidEmail, ValidHash, "  Ana Developer ", Now);

        Assert.True(result.IsSuccess);
        var user = result.Value;
        Assert.NotEqual(Guid.Empty, user.Id);
        Assert.Equal(ValidEmail, user.Email);
        Assert.Equal("Ana Developer", user.FullName);
        Assert.Equal(ValidHash, user.PasswordHash);
        Assert.True(user.IsActive);
        Assert.Equal(Now, user.CreatedAt);
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    public void Create_requires_a_full_name(string fullName)
    {
        var result = User.Create(ValidEmail, ValidHash, fullName, Now);

        Assert.Equal(UserErrors.FullNameRequired, result.Error);
    }

    [Fact]
    public void Create_rejects_a_too_long_full_name()
    {
        var result = User.Create(ValidEmail, ValidHash, new string('a', User.FullNameMaxLength + 1), Now);

        Assert.Equal(UserErrors.FullNameTooLong, result.Error);
    }

    [Fact]
    public void Create_requires_a_password_hash()
    {
        var result = User.Create(ValidEmail, " ", "Ana Developer", Now);

        Assert.Equal(UserErrors.PasswordHashRequired, result.Error);
    }

    [Fact]
    public void Rename_validates_and_trims()
    {
        var user = User.Create(ValidEmail, ValidHash, "Ana", Now).Value;

        Assert.Equal(UserErrors.FullNameRequired, user.Rename("").Error);
        Assert.True(user.Rename(" Ana Developer ").IsSuccess);
        Assert.Equal("Ana Developer", user.FullName);
    }

    [Fact]
    public void ChangePasswordHash_requires_a_value()
    {
        var user = User.Create(ValidEmail, ValidHash, "Ana", Now).Value;

        Assert.Equal(UserErrors.PasswordHashRequired, user.ChangePasswordHash("").Error);
        Assert.True(user.ChangePasswordHash("new-hash").IsSuccess);
        Assert.Equal("new-hash", user.PasswordHash);
    }

    [Fact]
    public void Deactivate_and_activate_toggle_the_state()
    {
        var user = User.Create(ValidEmail, ValidHash, "Ana", Now).Value;

        user.Deactivate();
        Assert.False(user.IsActive);

        user.Activate();
        Assert.True(user.IsActive);
    }
}
