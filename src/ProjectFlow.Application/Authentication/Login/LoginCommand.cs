using FluentValidation;
using Mediator;
using ProjectFlow.Application.Abstractions;
using ProjectFlow.Application.Abstractions.Persistence;
using ProjectFlow.Domain.Common;
using ProjectFlow.Domain.Users;

namespace ProjectFlow.Application.Authentication.Login;

public sealed record LoginCommand(string Email, string Password) : ICommand<Result<AccessTokenResponse>>;

public sealed record AccessTokenResponse(string AccessToken, string TokenType, DateTimeOffset ExpiresAt);

public sealed class LoginCommandValidator : AbstractValidator<LoginCommand>
{
    public LoginCommandValidator()
    {
        RuleFor(command => command.Email).NotEmpty();
        RuleFor(command => command.Password).NotEmpty();
    }
}

public sealed class LoginCommandHandler(
    IUserRepository users,
    IPasswordHasher passwordHasher,
    IAccessTokenGenerator accessTokenGenerator)
    : ICommandHandler<LoginCommand, Result<AccessTokenResponse>>
{
    public async ValueTask<Result<AccessTokenResponse>> Handle(LoginCommand command, CancellationToken cancellationToken)
    {
        var email = Email.Create(command.Email);
        var user = email.IsSuccess ? await users.GetByEmailAsync(email.Value, cancellationToken) : null;

        if (user is null)
        {
            // Hash anyway so an unknown e-mail takes as long as a wrong password (no account enumeration by timing).
            passwordHasher.Hash(command.Password);
            return AuthenticationErrors.InvalidCredentials;
        }

        if (!passwordHasher.Verify(command.Password, user.PasswordHash) || !user.IsActive)
        {
            return AuthenticationErrors.InvalidCredentials;
        }

        var token = accessTokenGenerator.Generate(user);
        return new AccessTokenResponse(token.Value, "Bearer", token.ExpiresAt);
    }
}
