using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using System.Text.Json.Serialization;
using ProjectFlow.Application.Authentication;
using ProjectFlow.Application.Authentication.Login;
using ProjectFlow.Application.Authentication.Register;

namespace ProjectFlow.Api.IntegrationTests.Infrastructure;

/// <summary>A registered user and an <see cref="HttpClient"/> that sends their access token.</summary>
public sealed record ApiUser(Guid Id, string Email, HttpClient Client);

public static class ApiUsers
{
    public const string Password = "Secret123";

    /// <summary>Same JSON settings as the API (enums as strings).</summary>
    public static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web)
    {
        Converters = { new JsonStringEnumConverter() },
    };

    public static async Task<ApiUser> CreateUserAsync(this ProjectFlowApiFactory api, string name = "user")
    {
        var email = $"{name}-{TestData.Unique()}@example.com";
        var client = api.CreateClient();

        var register = await client.PostAsJsonAsync("/api/auth/register", new RegisterUserCommand(email, Password, name));
        register.EnsureSuccessStatusCode();
        var user = (await register.Content.ReadFromJsonAsync<UserResponse>(Json))!;

        var login = await client.PostAsJsonAsync("/api/auth/login", new LoginCommand(email, Password));
        login.EnsureSuccessStatusCode();
        var tokens = (await login.Content.ReadFromJsonAsync<TokenResponse>(Json))!;

        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", tokens.AccessToken);
        return new ApiUser(user.Id, email, client);
    }

    public static Task<HttpResponseMessage> PostJsonAsync<T>(this HttpClient client, string url, T body) =>
        client.PostAsJsonAsync(url, body, Json);

    public static Task<HttpResponseMessage> PutJsonAsync<T>(this HttpClient client, string url, T body) =>
        client.PutAsJsonAsync(url, body, Json);

    public static async Task<T> ReadAsync<T>(this HttpResponseMessage response) =>
        (await response.Content.ReadFromJsonAsync<T>(Json))!;

    public static async Task<string?> ReadErrorCodeAsync(this HttpResponseMessage response) =>
        (await response.Content.ReadFromJsonAsync<JsonElement>()).TryGetProperty("code", out var code) ? code.GetString() : null;
}
