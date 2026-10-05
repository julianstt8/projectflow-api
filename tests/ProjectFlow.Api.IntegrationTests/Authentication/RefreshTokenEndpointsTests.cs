using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using ProjectFlow.Api.IntegrationTests.Infrastructure;
using ProjectFlow.Application.Authentication;
using ProjectFlow.Application.Authentication.Login;
using ProjectFlow.Application.Authentication.Logout;
using ProjectFlow.Application.Authentication.Refresh;
using ProjectFlow.Application.Authentication.Register;

namespace ProjectFlow.Api.IntegrationTests.Authentication;

[Collection(ApiCollection.Name)]
public class RefreshTokenEndpointsTests(ProjectFlowApiFactory api)
{
    private const string Password = "Secret123";

    private readonly HttpClient _client = api.CreateClient();

    [Fact]
    public async Task Refresh_returns_a_new_token_pair_that_works()
    {
        var login = await RegisterAndLoginAsync();

        var refreshed = await RefreshAsync(login.RefreshToken);

        Assert.Equal(HttpStatusCode.OK, refreshed.StatusCode);
        var tokens = await refreshed.Content.ReadFromJsonAsync<TokenResponse>();
        Assert.NotNull(tokens);
        Assert.NotEqual(login.RefreshToken, tokens.RefreshToken);
        Assert.Equal(HttpStatusCode.OK, (await GetMeAsync(tokens.AccessToken)).StatusCode);
    }

    [Fact]
    public async Task A_refresh_token_works_only_once()
    {
        var login = await RegisterAndLoginAsync();
        await RefreshAsync(login.RefreshToken);

        var second = await RefreshAsync(login.RefreshToken);

        Assert.Equal(HttpStatusCode.Unauthorized, second.StatusCode);
        Assert.Equal("Authentication.InvalidRefreshToken", (await second.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("code").GetString());
    }

    [Fact]
    public async Task Reusing_a_rotated_token_also_kills_the_token_that_replaced_it()
    {
        var login = await RegisterAndLoginAsync();
        var rotated = await (await RefreshAsync(login.RefreshToken)).Content.ReadFromJsonAsync<TokenResponse>();

        // An attacker replays the old token...
        await RefreshAsync(login.RefreshToken);

        // ...so the legitimate client's newer token is revoked too and both must log in again.
        Assert.Equal(HttpStatusCode.Unauthorized, (await RefreshAsync(rotated!.RefreshToken)).StatusCode);
    }

    [Fact]
    public async Task Two_simultaneous_refreshes_with_the_same_token_succeed_only_once()
    {
        var login = await RegisterAndLoginAsync();

        var responses = await Task.WhenAll(RefreshAsync(login.RefreshToken), RefreshAsync(login.RefreshToken));

        // Either PostgreSQL's xmin check rejects the slower save, or the slower request sees a revoked token.
        Assert.Single(responses, response => response.StatusCode == HttpStatusCode.OK);
        Assert.Single(responses, response => response.StatusCode == HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task Logout_ends_the_session()
    {
        var login = await RegisterAndLoginAsync();

        var logout = await _client.PostAsJsonAsync("/api/auth/logout", new LogoutCommand(login.RefreshToken));

        Assert.Equal(HttpStatusCode.NoContent, logout.StatusCode);
        Assert.Equal(HttpStatusCode.Unauthorized, (await RefreshAsync(login.RefreshToken)).StatusCode);
    }

    [Fact]
    public async Task Logout_with_an_unknown_token_also_returns_204()
    {
        var logout = await _client.PostAsJsonAsync("/api/auth/logout", new LogoutCommand("not-a-real-token"));

        Assert.Equal(HttpStatusCode.NoContent, logout.StatusCode);
    }

    [Fact]
    public async Task Only_the_hash_of_the_refresh_token_is_stored()
    {
        var login = await RegisterAndLoginAsync();

        await using var dbContext = api.CreateDbContext(organizationId: null);
        var hashes = await dbContext.RefreshTokens.Select(token => token.TokenHash).ToListAsync();

        Assert.DoesNotContain(login.RefreshToken, hashes);
        Assert.All(hashes, hash => Assert.Matches("^[0-9a-f]{64}$", hash));
    }

    private async Task<TokenResponse> RegisterAndLoginAsync()
    {
        var email = $"user-{TestData.Unique()}@example.com";
        (await _client.PostAsJsonAsync("/api/auth/register", new RegisterUserCommand(email, Password, "Test User"))).EnsureSuccessStatusCode();

        var login = await _client.PostAsJsonAsync("/api/auth/login", new LoginCommand(email, Password));
        login.EnsureSuccessStatusCode();
        return (await login.Content.ReadFromJsonAsync<TokenResponse>())!;
    }

    private Task<HttpResponseMessage> RefreshAsync(string refreshToken) =>
        _client.PostAsJsonAsync("/api/auth/refresh", new RefreshTokenCommand(refreshToken));

    private Task<HttpResponseMessage> GetMeAsync(string accessToken)
    {
        var request = new HttpRequestMessage(HttpMethod.Get, "/api/auth/me");
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", accessToken);
        return _client.SendAsync(request);
    }
}
