using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.IdentityModel.JsonWebTokens;
using ProjectFlow.Api.Controllers;
using ProjectFlow.Api.IntegrationTests.Infrastructure;
using ProjectFlow.Application.Authentication.Login;
using ProjectFlow.Application.Authentication.Register;

namespace ProjectFlow.Api.IntegrationTests.Authentication;

[Collection(ApiCollection.Name)]
public class AuthEndpointsTests(ProjectFlowApiFactory api)
{
    private const string Password = "Secret123";

    private readonly HttpClient _client = api.CreateClient();

    [Fact]
    public async Task Register_creates_an_account()
    {
        var email = NewEmail();

        var response = await _client.PostAsJsonAsync("/api/auth/register", new RegisterUserCommand(email, Password, "Ana Developer"));

        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        var user = await response.Content.ReadFromJsonAsync<UserResponse>();
        Assert.NotNull(user);
        Assert.Equal(email, user.Email);
        Assert.Equal("Ana Developer", user.FullName);
        Assert.DoesNotContain("password", await response.Content.ReadAsStringAsync(), StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task Register_with_an_existing_email_returns_409()
    {
        var email = NewEmail();
        await RegisterAsync(email);

        var response = await _client.PostAsJsonAsync("/api/auth/register", new RegisterUserCommand(email.ToUpperInvariant(), Password, "Copy"));

        Assert.Equal(HttpStatusCode.Conflict, response.StatusCode);
        Assert.Equal("Authentication.EmailAlreadyRegistered", (await ReadProblemAsync(response)).GetProperty("code").GetString());
    }

    [Fact]
    public async Task Register_with_invalid_input_returns_400_with_field_errors()
    {
        var response = await _client.PostAsJsonAsync("/api/auth/register", new RegisterUserCommand("", "short", ""));

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        Assert.Equal("application/problem+json", response.Content.Headers.ContentType?.MediaType);
        var errors = (await ReadProblemAsync(response)).GetProperty("errors");
        Assert.True(errors.TryGetProperty(nameof(RegisterUserCommand.Email), out _));
        Assert.True(errors.TryGetProperty(nameof(RegisterUserCommand.Password), out _));
        Assert.True(errors.TryGetProperty(nameof(RegisterUserCommand.FullName), out _));
    }

    [Fact]
    public async Task Login_returns_a_token_that_opens_protected_endpoints()
    {
        var email = NewEmail();
        await RegisterAsync(email);

        var login = await _client.PostAsJsonAsync("/api/auth/login", new LoginCommand(email, Password));
        Assert.Equal(HttpStatusCode.OK, login.StatusCode);
        var token = await login.Content.ReadFromJsonAsync<AccessTokenResponse>();
        Assert.NotNull(token);
        Assert.Equal("Bearer", token.TokenType);

        using var request = new HttpRequestMessage(HttpMethod.Get, "/api/auth/me");
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token.AccessToken);
        var me = await _client.SendAsync(request);

        Assert.Equal(HttpStatusCode.OK, me.StatusCode);
        var currentUser = await me.Content.ReadFromJsonAsync<CurrentUserResponse>();
        Assert.Equal(email, currentUser!.Email);
    }

    [Fact]
    public async Task Access_token_carries_no_roles()
    {
        var email = NewEmail();
        await RegisterAsync(email);

        var token = await LoginAsync(email);

        var claimTypes = new JsonWebToken(token).Claims.Select(claim => claim.Type).ToList();
        Assert.Contains(JwtRegisteredClaimNames.Sub, claimTypes);
        Assert.DoesNotContain(claimTypes, type => type.Contains("role", StringComparison.OrdinalIgnoreCase));
    }

    [Theory]
    [InlineData("WrongPassword1")]
    [InlineData(null)]
    public async Task Login_with_wrong_password_or_unknown_user_returns_the_same_401(string? wrongPassword)
    {
        var email = NewEmail();
        await RegisterAsync(email);
        var command = wrongPassword is null
            ? new LoginCommand(NewEmail(), Password)
            : new LoginCommand(email, wrongPassword);

        var response = await _client.PostAsJsonAsync("/api/auth/login", command);

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
        Assert.Equal("Authentication.InvalidCredentials", (await ReadProblemAsync(response)).GetProperty("code").GetString());
    }

    [Theory]
    [InlineData(null)]
    [InlineData("not-a-jwt")]
    [InlineData("eyJhbGciOiJIUzI1NiIsInR5cCI6IkpXVCJ9.eyJzdWIiOiIxIn0.c2lnbmF0dXJl")]
    public async Task Protected_endpoints_reject_missing_or_invalid_tokens(string? token)
    {
        using var request = new HttpRequestMessage(HttpMethod.Get, "/api/auth/me");
        if (token is not null)
        {
            request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);
        }

        var response = await _client.SendAsync(request);

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    private static string NewEmail() => $"user-{TestData.Unique()}@example.com";

    private async Task RegisterAsync(string email)
    {
        var response = await _client.PostAsJsonAsync("/api/auth/register", new RegisterUserCommand(email, Password, "Test User"));
        response.EnsureSuccessStatusCode();
    }

    private async Task<string> LoginAsync(string email)
    {
        var response = await _client.PostAsJsonAsync("/api/auth/login", new LoginCommand(email, Password));
        response.EnsureSuccessStatusCode();
        return (await response.Content.ReadFromJsonAsync<AccessTokenResponse>())!.AccessToken;
    }

    private static async Task<JsonElement> ReadProblemAsync(HttpResponseMessage response) =>
        await response.Content.ReadFromJsonAsync<JsonElement>();
}
