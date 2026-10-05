using FluentValidation;
using Mediator;
using ProjectFlow.Application.Abstractions;
using ProjectFlow.Application.Abstractions.Persistence;
using ProjectFlow.Domain.Common;

namespace ProjectFlow.Application.Authentication.Refresh;

public sealed record RefreshTokenCommand(string RefreshToken) : ICommand<Result<TokenResponse>>;

public sealed class RefreshTokenCommandValidator : AbstractValidator<RefreshTokenCommand>
{
    public RefreshTokenCommandValidator()
    {
        RuleFor(command => command.RefreshToken).NotEmpty();
    }
}

/// <summary>Rotates a refresh token: the presented one is revoked and a new pair is issued.</summary>
public sealed class RefreshTokenCommandHandler(
    IUserRepository users,
    IRefreshTokenRepository refreshTokens,
    IUnitOfWork unitOfWork,
    IAccessTokenGenerator accessTokenGenerator,
    IRefreshTokenService refreshTokenService,
    TimeProvider timeProvider)
    : ICommandHandler<RefreshTokenCommand, Result<TokenResponse>>
{
    public async ValueTask<Result<TokenResponse>> Handle(RefreshTokenCommand command, CancellationToken cancellationToken)
    {
        var now = timeProvider.GetUtcNow();
        var current = await refreshTokens.GetByHashAsync(refreshTokenService.Hash(command.RefreshToken), cancellationToken);

        if (current is null || current.IsExpired(now))
        {
            return AuthenticationErrors.InvalidRefreshToken;
        }

        if (current.IsRevoked)
        {
            // Reuse of a rotated token: it was stolen. Revoke the whole family so neither copy works anymore.
            await refreshTokens.RevokeFamilyAsync(current.FamilyId, now, cancellationToken);
            return AuthenticationErrors.InvalidRefreshToken;
        }

        var user = await users.GetByIdAsync(current.UserId, cancellationToken);
        if (user is null || !user.IsActive)
        {
            await refreshTokens.RevokeFamilyAsync(current.FamilyId, now, cancellationToken);
            return AuthenticationErrors.InvalidRefreshToken;
        }

        var generated = refreshTokenService.Generate();
        var next = current.Rotate(generated.Hash, now, refreshTokenService.Lifetime);
        if (next.IsFailure)
        {
            return AuthenticationErrors.InvalidRefreshToken;
        }

        refreshTokens.Add(next.Value);

        try
        {
            await unitOfWork.SaveChangesAsync(cancellationToken);
        }
        catch (ConcurrencyConflictException)
        {
            // Another request rotated the same token first: treat this one as a reuse.
            return AuthenticationErrors.InvalidRefreshToken;
        }

        var accessToken = accessTokenGenerator.Generate(user);
        return new TokenResponse(accessToken.Value, "Bearer", accessToken.ExpiresAt, generated.Value, next.Value.ExpiresAt);
    }
}
