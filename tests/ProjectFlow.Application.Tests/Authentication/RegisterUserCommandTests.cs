using ProjectFlow.Application.Authentication;
using ProjectFlow.Application.Authentication.Register;
using ProjectFlow.Domain.Users;

namespace ProjectFlow.Application.Tests.Authentication;

public class RegisterUserCommandTests
{
    private static readonly DateTimeOffset Now = new(2026, 10, 5, 12, 0, 0, TimeSpan.Zero);

    private readonly InMemoryUserRepository _users = new();
    private readonly FakeUnitOfWork _unitOfWork = new();
    private readonly RegisterUserCommandHandler _handler;

    public RegisterUserCommandTests()
    {
        _handler = new RegisterUserCommandHandler(_users, _unitOfWork, new FakePasswordHasher(), new FixedTimeProvider(Now));
    }

    [Fact]
    public async Task Registers_a_user_with_a_normalized_email_and_a_hashed_password()
    {
        var result = await _handler.Handle(new RegisterUserCommand(" Ana@Example.com ", "Secret123", "Ana"), CancellationToken.None);

        Assert.True(result.IsSuccess);
        var user = Assert.Single(_users.Users);
        Assert.Equal(user.Id, result.Value.Id);
        Assert.Equal("ana@example.com", result.Value.Email);
        Assert.Equal("hashed:Secret123", user.PasswordHash);
        Assert.Equal(Now, user.CreatedAt);
        Assert.Equal(1, _unitOfWork.SaveCount);
    }

    [Fact]
    public async Task Rejects_an_email_that_is_already_registered()
    {
        await _handler.Handle(new RegisterUserCommand("ana@example.com", "Secret123", "Ana"), CancellationToken.None);

        var result = await _handler.Handle(new RegisterUserCommand("ANA@example.com", "Other4567", "Ana 2"), CancellationToken.None);

        Assert.Equal(AuthenticationErrors.EmailAlreadyRegistered, result.Error);
        Assert.Single(_users.Users);
    }

    [Fact]
    public async Task Rejects_an_invalid_email_without_saving()
    {
        var result = await _handler.Handle(new RegisterUserCommand("not-an-email", "Secret123", "Ana"), CancellationToken.None);

        Assert.Equal(UserErrors.EmailInvalid, result.Error);
        Assert.Equal(0, _unitOfWork.SaveCount);
    }

    [Theory]
    [InlineData("", "Secret123", "Ana", nameof(RegisterUserCommand.Email))]
    [InlineData("ana@example.com", "Secret123", "", nameof(RegisterUserCommand.FullName))]
    [InlineData("ana@example.com", "Short1", "Ana", nameof(RegisterUserCommand.Password))]
    [InlineData("ana@example.com", "OnlyLetters", "Ana", nameof(RegisterUserCommand.Password))]
    [InlineData("ana@example.com", "123456789", "Ana", nameof(RegisterUserCommand.Password))]
    public void Validator_rejects_invalid_input(string email, string password, string fullName, string property)
    {
        var result = new RegisterUserCommandValidator().Validate(new RegisterUserCommand(email, password, fullName));

        Assert.Contains(result.Errors, error => error.PropertyName == property);
    }

    [Fact]
    public void Validator_accepts_valid_input()
    {
        Assert.True(new RegisterUserCommandValidator().Validate(new RegisterUserCommand("ana@example.com", "Secret123", "Ana")).IsValid);
    }
}
