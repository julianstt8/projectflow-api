using FluentValidation;
using Mediator;
using ProjectFlow.Application.Abstractions;
using ProjectFlow.Application.Abstractions.Persistence;

namespace ProjectFlow.Application.Authentication.Logout;

/// <summary>Ends the session of the given refresh token. Always succeeds, so it reveals nothing about the token.</summary>
public sealed record LogoutCommand(string RefreshToken) : ICommand;

public sealed class LogoutCommandValidator : AbstractValidator<LogoutCommand>
{
    public LogoutCommandValidator()
    {
        RuleFor(command => command.RefreshToken).NotEmpty();
    }
}

public sealed class LogoutCommandHandler(
    IRefreshTokenRepository refreshTokens,
    IRefreshTokenService refreshTokenService,
    TimeProvider timeProvider)
    : ICommandHandler<LogoutCommand>
{
    public async ValueTask<Unit> Handle(LogoutCommand command, CancellationToken cancellationToken)
    {
        var token = await refreshTokens.GetByHashAsync(refreshTokenService.Hash(command.RefreshToken), cancellationToken);

        if (token is not null)
        {
            await refreshTokens.RevokeFamilyAsync(token.FamilyId, timeProvider.GetUtcNow(), cancellationToken);
        }

        return Unit.Value;
    }
}
