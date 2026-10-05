using FluentValidation;
using Mediator;
using ProjectFlow.Application.Abstractions;
using ProjectFlow.Application.Abstractions.Persistence;
using ProjectFlow.Domain.Common;
using ProjectFlow.Domain.Users;

namespace ProjectFlow.Application.Authentication.Login;

public sealed record LoginCommand(string Email, string Password) : ICommand<Result<TokenResponse>>;

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
    IRefreshTokenRepository refreshTokens,
    IUnitOfWork unitOfWork,
    IPasswordHasher passwordHasher,
    IAccessTokenGenerator accessTokenGenerator,
    IRefreshTokenService refreshTokenService,
    TimeProvider timeProvider)
    : ICommandHandler<LoginCommand, Result<TokenResponse>>
{
    public async ValueTask<Result<TokenResponse>> Handle(LoginCommand command, CancellationToken cancellationToken)
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

        // Every login starts a new refresh token family (one per device or session).
        var generated = refreshTokenService.Generate();
        var refreshToken = RefreshToken.Issue(user.Id, generated.Hash, timeProvider.GetUtcNow(), refreshTokenService.Lifetime);
        refreshTokens.Add(refreshToken);
        await unitOfWork.SaveChangesAsync(cancellationToken);

        var accessToken = accessTokenGenerator.Generate(user);
        return new TokenResponse(accessToken.Value, "Bearer", accessToken.ExpiresAt, generated.Value, refreshToken.ExpiresAt);
    }
}
