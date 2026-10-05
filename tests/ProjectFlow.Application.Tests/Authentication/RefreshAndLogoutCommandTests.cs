using ProjectFlow.Application.Authentication;
using ProjectFlow.Application.Authentication.Login;
using ProjectFlow.Application.Authentication.Logout;
using ProjectFlow.Application.Authentication.Refresh;
using ProjectFlow.Domain.Users;

namespace ProjectFlow.Application.Tests.Authentication;

public class RefreshAndLogoutCommandTests
{
    private static readonly DateTimeOffset LoginTime = new(2026, 10, 5, 12, 0, 0, TimeSpan.Zero);

    private readonly InMemoryUserRepository _users = new();
    private readonly InMemoryRefreshTokenRepository _refreshTokens = new();
    private readonly FakeUnitOfWork _unitOfWork = new();
    private readonly FakeRefreshTokenService _refreshTokenService = new();
    private readonly FakePasswordHasher _hasher = new();
    private readonly User _ana;
    private DateTimeOffset _now = LoginTime;

    public RefreshAndLogoutCommandTests()
    {
        _ana = User.Create(Email.Create("ana@example.com").Value, _hasher.Hash("Secret123"), "Ana", LoginTime).Value;
        _users.Add(_ana);
    }

    [Fact]
    public async Task Refresh_rotates_the_token_within_the_same_family()
    {
        var login = await LoginAsync();
        _now = LoginTime.AddMinutes(20);

        var result = await RefreshAsync(login.RefreshToken);

        Assert.True(result.IsSuccess);
        Assert.Equal("refresh-2", result.Value.RefreshToken);
        Assert.Equal(_now.AddDays(7), result.Value.RefreshTokenExpiresAt);
        var first = _refreshTokens.Tokens[0];
        var second = _refreshTokens.Tokens[1];
        Assert.True(first.IsRevoked);
        Assert.Equal(second.Id, first.ReplacedById);
        Assert.Equal(first.FamilyId, second.FamilyId);
        Assert.False(second.IsRevoked);
    }

    [Fact]
    public async Task Reusing_a_rotated_token_revokes_the_whole_family()
    {
        var login = await LoginAsync();
        var rotated = await RefreshAsync(login.RefreshToken);

        var reuse = await RefreshAsync(login.RefreshToken);

        Assert.Equal(AuthenticationErrors.InvalidRefreshToken, reuse.Error);
        Assert.All(_refreshTokens.Tokens, token => Assert.True(token.IsRevoked));
        Assert.Equal(AuthenticationErrors.InvalidRefreshToken, (await RefreshAsync(rotated.Value.RefreshToken)).Error);
    }

    [Fact]
    public async Task Reuse_does_not_affect_other_sessions_of_the_same_user()
    {
        var phone = await LoginAsync();
        var laptop = await LoginAsync();
        await RefreshAsync(phone.RefreshToken);

        await RefreshAsync(phone.RefreshToken);

        Assert.True((await RefreshAsync(laptop.RefreshToken)).IsSuccess);
    }

    [Fact]
    public async Task Expired_token_is_rejected()
    {
        var login = await LoginAsync();
        _now = LoginTime.AddDays(7);

        var result = await RefreshAsync(login.RefreshToken);

        Assert.Equal(AuthenticationErrors.InvalidRefreshToken, result.Error);
    }

    [Theory]
    [InlineData("unknown-token")]
    [InlineData("")]
    public async Task Unknown_token_is_rejected(string refreshToken)
    {
        await LoginAsync();

        Assert.Equal(AuthenticationErrors.InvalidRefreshToken, (await RefreshAsync(refreshToken)).Error);
    }

    [Fact]
    public async Task Inactive_user_cannot_refresh_and_loses_the_session()
    {
        var login = await LoginAsync();
        _ana.Deactivate();

        var result = await RefreshAsync(login.RefreshToken);

        Assert.Equal(AuthenticationErrors.InvalidRefreshToken, result.Error);
        Assert.True(_refreshTokens.Tokens[0].IsRevoked);
    }

    [Fact]
    public async Task Concurrent_refresh_of_the_same_token_lets_only_one_succeed()
    {
        var login = await LoginAsync();
        _unitOfWork.FailNextSaveWithConflict = true;

        var result = await RefreshAsync(login.RefreshToken);

        Assert.Equal(AuthenticationErrors.InvalidRefreshToken, result.Error);
    }

    [Fact]
    public async Task Logout_revokes_the_session_and_ignores_unknown_tokens()
    {
        var login = await LoginAsync();
        var handler = new LogoutCommandHandler(_refreshTokens, _refreshTokenService, new MutableTimeProvider(() => _now));

        await handler.Handle(new LogoutCommand("unknown-token"), CancellationToken.None);
        Assert.False(_refreshTokens.Tokens[0].IsRevoked);

        await handler.Handle(new LogoutCommand(login.RefreshToken), CancellationToken.None);
        Assert.True(_refreshTokens.Tokens[0].IsRevoked);
        Assert.Equal(AuthenticationErrors.InvalidRefreshToken, (await RefreshAsync(login.RefreshToken)).Error);
    }

    private async Task<TokenResponse> LoginAsync()
    {
        var handler = new LoginCommandHandler(
            _users,
            _refreshTokens,
            _unitOfWork,
            _hasher,
            new FakeAccessTokenGenerator(),
            _refreshTokenService,
            new MutableTimeProvider(() => _now));

        return (await handler.Handle(new LoginCommand("ana@example.com", "Secret123"), CancellationToken.None)).Value;
    }

    private async Task<Domain.Common.Result<TokenResponse>> RefreshAsync(string refreshToken)
    {
        var handler = new RefreshTokenCommandHandler(
            _users,
            _refreshTokens,
            _unitOfWork,
            new FakeAccessTokenGenerator(),
            _refreshTokenService,
            new MutableTimeProvider(() => _now));

        return await handler.Handle(new RefreshTokenCommand(refreshToken), CancellationToken.None);
    }

    private sealed class MutableTimeProvider(Func<DateTimeOffset> now) : TimeProvider
    {
        public override DateTimeOffset GetUtcNow() => now();
    }
}
