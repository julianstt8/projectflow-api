using System.Net;
using System.Net.Http.Json;
using System.Text;
using System.Text.Json;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.DependencyInjection;
using ProjectFlow.Api.IntegrationTests.Infrastructure;
using ProjectFlow.Application.Authentication.Register;
using ProjectFlow.Domain.Common;

namespace ProjectFlow.Api.IntegrationTests.ErrorHandling;

/// <summary>
/// Every failure is answered as RFC 9457 ProblemDetails (<c>application/problem+json</c>) with the right
/// status, a stable <c>code</c> and a trace id, and never leaks exception details.
/// </summary>
[Collection(ApiCollection.Name)]
public sealed class ErrorResponseTests(ProjectFlowApiFactory api)
{
    private static WebApplicationFactory<Program>? _probeHost;

    private HttpClient ProbeClient => (_probeHost ??= api.WithWebHostBuilder(builder => builder
        .UseEnvironment("Production")
        .UseSetting("Jwt:SigningKey", new string('k', 64))
        .ConfigureTestServices(services =>
            services.AddControllers().AddApplicationPart(typeof(ErrorProbeController).Assembly)))).CreateClient();

    [Theory]
    [InlineData(ErrorType.Validation, HttpStatusCode.BadRequest)]
    [InlineData(ErrorType.Unauthorized, HttpStatusCode.Unauthorized)]
    [InlineData(ErrorType.Forbidden, HttpStatusCode.Forbidden)]
    [InlineData(ErrorType.NotFound, HttpStatusCode.NotFound)]
    [InlineData(ErrorType.Conflict, HttpStatusCode.Conflict)]
    [InlineData(ErrorType.BusinessRule, HttpStatusCode.UnprocessableEntity)]
    public async Task Each_error_type_maps_to_its_status_and_keeps_its_code(ErrorType type, HttpStatusCode expected)
    {
        var response = await ProbeClient.GetAsync($"/api/error-probe/result/{type}");

        var problem = await AssertProblemAsync(response, expected, $"Probe.{type}");
        Assert.Equal($"Probe {type} error.", problem.GetProperty("title").GetString());
    }

    [Fact]
    public async Task Validation_exception_returns_400_with_field_errors()
    {
        var response = await ProbeClient.GetAsync("/api/error-probe/validation-exception");

        var problem = await AssertProblemAsync(response, HttpStatusCode.BadRequest, "Validation.Failed");
        var messages = problem.GetProperty("errors").GetProperty("Name").EnumerateArray().Select(e => e.GetString());
        Assert.Equal(["Name is required.", "Name is too short."], messages);
    }

    [Theory]
    [InlineData("concurrency-exception", "Conflict.Concurrency")]
    [InlineData("unique-exception", "Conflict.Duplicate")]
    public async Task Persistence_conflicts_return_409(string probe, string code)
    {
        var response = await ProbeClient.GetAsync($"/api/error-probe/{probe}");

        await AssertProblemAsync(response, HttpStatusCode.Conflict, code);
    }

    [Fact]
    public async Task Unhandled_exceptions_return_500_without_leaking_details()
    {
        var response = await ProbeClient.GetAsync("/api/error-probe/unhandled-exception");

        await AssertProblemAsync(response, HttpStatusCode.InternalServerError, "Server.Error");
        var body = await response.Content.ReadAsStringAsync();
        Assert.DoesNotContain("Sensitive internal detail", body);
        Assert.DoesNotContain("InvalidOperationException", body);
    }

    [Fact]
    public async Task Missing_token_returns_401_problem_details()
    {
        var response = await api.CreateClient().GetAsync("/api/organizations");

        await AssertProblemAsync(response, HttpStatusCode.Unauthorized, "Authentication.Required");
    }

    [Fact]
    public async Task Unknown_route_returns_404_problem_details()
    {
        var user = await api.CreateUserAsync();

        var response = await user.Client.GetAsync("/api/does-not-exist");

        await AssertProblemAsync(response, HttpStatusCode.NotFound, "Resource.NotFound");
    }

    [Fact]
    public async Task Malformed_json_returns_400_problem_details()
    {
        using var content = new StringContent("{ \"email\": ", Encoding.UTF8, "application/json");

        var response = await api.CreateClient().PostAsync("/api/auth/register", content);

        await AssertProblemAsync(response, HttpStatusCode.BadRequest, "Request.Invalid");
    }

    [Fact]
    public async Task Two_simultaneous_registrations_with_the_same_email_never_return_500()
    {
        var command = new RegisterUserCommand($"race-{TestData.Unique()}@example.com", "Secret123", "Racer");
        var client = api.CreateClient();

        var responses = await Task.WhenAll(Enumerable.Range(0, 5).Select(_ => client.PostAsJsonAsync("/api/auth/register", command)));

        // The use case check stops most of them; the unique index stops the ones that raced past it.
        Assert.Single(responses, response => response.StatusCode == HttpStatusCode.Created);
        Assert.All(
            responses.Where(response => response.StatusCode != HttpStatusCode.Created),
            response => Assert.Equal(HttpStatusCode.Conflict, response.StatusCode));
    }

    private static async Task<JsonElement> AssertProblemAsync(HttpResponseMessage response, HttpStatusCode status, string code)
    {
        Assert.Equal(status, response.StatusCode);
        Assert.Equal("application/problem+json", response.Content.Headers.ContentType?.MediaType);

        var problem = await response.Content.ReadFromJsonAsync<JsonElement>();
        Assert.Equal((int)status, problem.GetProperty("status").GetInt32());
        Assert.Equal(code, problem.GetProperty("code").GetString());
        Assert.False(string.IsNullOrEmpty(problem.GetProperty("traceId").GetString()));
        return problem;
    }
}
