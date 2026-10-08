using System.Net;
using System.Text.Json;
using Microsoft.AspNetCore.Hosting;
using ProjectFlow.Api.IntegrationTests.Infrastructure;

namespace ProjectFlow.Api.IntegrationTests.Documentation;

/// <summary>The OpenAPI document and the Scalar reference: complete, secured correctly and only in Development.</summary>
[Collection(ApiCollection.Name)]
public class ApiReferenceTests(ProjectFlowApiFactory api)
{
    private static readonly string[] HttpMethods = ["get", "post", "put", "delete", "patch"];

    [Fact]
    public async Task Document_describes_the_api_and_the_bearer_scheme()
    {
        var document = await DocumentAsync();

        Assert.Equal("ProjectFlow API", document.GetProperty("info").GetProperty("title").GetString());
        var bearer = document.GetProperty("components").GetProperty("securitySchemes").GetProperty("Bearer");
        Assert.Equal("http", bearer.GetProperty("type").GetString());
        Assert.Equal("bearer", bearer.GetProperty("scheme").GetString());
        Assert.Equal("JWT", bearer.GetProperty("bearerFormat").GetString());
    }

    [Fact]
    public async Task Every_operation_has_a_summary()
    {
        var operations = Operations(await DocumentAsync());

        var withoutSummary = operations
            .Where(operation => !operation.Value.TryGetProperty("summary", out var summary) || string.IsNullOrWhiteSpace(summary.GetString()))
            .Select(operation => operation.Key)
            .ToList();

        Assert.True(operations.Count > 40, $"Only {operations.Count} operations documented.");
        Assert.Empty(withoutSummary);
    }

    [Theory]
    [InlineData("post /api/auth/register", false)]
    [InlineData("post /api/auth/login", false)]
    [InlineData("post /api/auth/refresh", false)]
    [InlineData("get /api/auth/me", true)]
    [InlineData("get /api/organizations", true)]
    [InlineData("post /api/organizations/{organizationId}/projects/{projectId}/tasks", true)]
    public async Task Only_protected_operations_require_the_bearer_token(string operation, bool secured)
    {
        var operations = Operations(await DocumentAsync());

        var requiresBearer = operations[operation].TryGetProperty("security", out var security)
            && security.EnumerateArray().Any(requirement => requirement.TryGetProperty("Bearer", out _));

        Assert.Equal(secured, requiresBearer);
    }

    [Fact]
    public async Task Request_bodies_have_realistic_examples()
    {
        var schemas = (await DocumentAsync()).GetProperty("components").GetProperty("schemas");

        var login = schemas.GetProperty("LoginCommand").GetProperty("examples")[0];
        var comment = schemas.GetProperty("CommentRequest").GetProperty("examples")[0];

        Assert.Equal("ana.admin@example.com", login.GetProperty("email").GetString());
        Assert.StartsWith("¡Corregido!", comment.GetProperty("body").GetString());
    }

    [Fact]
    public async Task Scalar_reference_is_served_and_the_root_redirects_to_it()
    {
        var client = api.CreateClient(new Microsoft.AspNetCore.Mvc.Testing.WebApplicationFactoryClientOptions { AllowAutoRedirect = false });

        var root = await client.GetAsync("/");
        var reference = await api.CreateClient().GetAsync("/scalar");

        Assert.Equal(HttpStatusCode.Redirect, root.StatusCode);
        Assert.Equal("/scalar", root.Headers.Location?.ToString());
        Assert.Equal(HttpStatusCode.OK, reference.StatusCode);
        Assert.Equal("text/html", reference.Content.Headers.ContentType?.MediaType);
    }

    [Fact]
    public async Task Outside_development_the_reference_is_not_exposed()
    {
        using var production = api.WithWebHostBuilder(builder => builder
            .UseEnvironment("Production")
            .UseSetting("Jwt:SigningKey", new string('k', 64)));
        var client = production.CreateClient();

        // Anonymous requests to routes that do not exist are challenged (401) by the secure-by-default policy.
        HttpStatusCode[] notExposed = [HttpStatusCode.NotFound, HttpStatusCode.Unauthorized];
        Assert.Contains((await client.GetAsync("/openapi/v1.json")).StatusCode, notExposed);
        Assert.Contains((await client.GetAsync("/scalar")).StatusCode, notExposed);
        Assert.Contains((await client.GetAsync("/")).StatusCode, notExposed);
    }

    private async Task<JsonElement> DocumentAsync()
    {
        var response = await api.CreateClient().GetAsync("/openapi/v1.json");
        response.EnsureSuccessStatusCode();
        return JsonDocument.Parse(await response.Content.ReadAsStringAsync()).RootElement;
    }

    /// <summary>"method /path" → operation object.</summary>
    private static Dictionary<string, JsonElement> Operations(JsonElement document) =>
        document.GetProperty("paths").EnumerateObject()
            .SelectMany(path => path.Value.EnumerateObject()
                .Where(method => HttpMethods.Contains(method.Name))
                .Select(method => (Key: $"{method.Name} {path.Name}", method.Value)))
            .ToDictionary(operation => operation.Key, operation => operation.Value);
}
