using ProjectFlow.Application.Authentication;
using ProjectFlow.Application.Authentication.Login;
using ProjectFlow.Domain.Users;

namespace ProjectFlow.Application.Tests.Authentication;

public class LoginCommandTests
{
    private static readonly DateTimeOffset Now = new(2026, 10, 5, 12, 0, 0, TimeSpan.Zero);

    private readonly InMemoryUserRepository _users = new();
    private readonly FakePasswordHasher _hasher = new();
    private readonly LoginCommandHandler _handler;
    private readonly User _ana;

    public LoginCommandTests()
    {
        _handler = new LoginCommandHandler(_users, _hasher, new FakeAccessTokenGenerator());
        _ana = User.Create(Email.Create("ana@example.com").Value, _hasher.Hash("Secret123"), "Ana", Now).Value;
        _users.Add(_ana);
    }

    [Fact]
    public async Task Returns_a_bearer_token_for_valid_credentials()
    {
        var result = await _handler.Handle(new LoginCommand("ANA@example.com", "Secret123"), CancellationToken.None);

        Assert.True(result.IsSuccess);
        Assert.Equal($"token-for-{_ana.Id}", result.Value.AccessToken);
        Assert.Equal("Bearer", result.Value.TokenType);
        Assert.Equal(FakeAccessTokenGenerator.ExpiresAt, result.Value.ExpiresAt);
    }

    [Theory]
    [InlineData("ana@example.com", "WrongPassword1")]
    [InlineData("unknown@example.com", "Secret123")]
    [InlineData("not-an-email", "Secret123")]
    public async Task Returns_the_same_error_for_any_invalid_credentials(string email, string password)
    {
        var result = await _handler.Handle(new LoginCommand(email, password), CancellationToken.None);

        Assert.Equal(AuthenticationErrors.InvalidCredentials, result.Error);
    }

    [Fact]
    public async Task Rejects_an_inactive_user()
    {
        _ana.Deactivate();

        var result = await _handler.Handle(new LoginCommand("ana@example.com", "Secret123"), CancellationToken.None);

        Assert.Equal(AuthenticationErrors.InvalidCredentials, result.Error);
    }

    [Fact]
    public async Task Hashes_the_password_even_when_the_user_does_not_exist()
    {
        var hashesBefore = _hasher.HashCount;

        await _handler.Handle(new LoginCommand("unknown@example.com", "Secret123"), CancellationToken.None);

        Assert.Equal(hashesBefore + 1, _hasher.HashCount);
    }
}
