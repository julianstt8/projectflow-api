using System.Net;
using System.Text.Json;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using ProjectFlow.Api.IntegrationTests.Infrastructure;

namespace ProjectFlow.Api.IntegrationTests.Documentation;

/// <summary>
/// The OpenAPI documents (English and Spanish) and the Scalar reference: every endpoint explained in both languages,
/// with who can call it, its error codes and examples, secured correctly and only in Development.
/// </summary>
[Collection(ApiCollection.Name)]
public class ApiReferenceTests(ProjectFlowApiFactory api)
{
    private static readonly string[] HttpMethods = ["get", "post", "put", "delete", "patch"];

    [Theory]
    [InlineData("en")]
    [InlineData("es")]
    public async Task Each_language_has_its_document_with_the_bearer_scheme(string language)
    {
        var document = await DocumentAsync(language);

        Assert.Equal("ProjectFlow API", document.GetProperty("info").GetProperty("title").GetString());
        var bearer = document.GetProperty("components").GetProperty("securitySchemes").GetProperty("Bearer");
        Assert.Equal("http", bearer.GetProperty("type").GetString());
        Assert.Equal("bearer", bearer.GetProperty("scheme").GetString());
        Assert.Equal("JWT", bearer.GetProperty("bearerFormat").GetString());
    }

    [Fact]
    public async Task Every_operation_has_a_title_and_description_in_both_languages()
    {
        var english = Operations(await DocumentAsync("en"));
        var spanish = Operations(await DocumentAsync("es"));

        Assert.True(english.Count > 50, $"Only {english.Count} operations documented.");
        Assert.Equal(english.Keys.Order(), spanish.Keys.Order());
        Assert.DoesNotContain(english.Keys, key => !Translated(english[key], spanish[key], "summary"));
        Assert.DoesNotContain(english.Keys, key => !Translated(english[key], spanish[key], "description"));
    }

    [Theory]
    [InlineData("en", "Who can call it:")]
    [InlineData("es", "Quién puede llamarlo:")]
    public async Task Every_operation_says_who_can_call_it(string language, string label)
    {
        var operations = Operations(await DocumentAsync(language));

        Assert.DoesNotContain(operations, operation => !operation.Value.GetProperty("description").GetString()!.Contains(label));
    }

    [Theory]
    [InlineData("post /api/auth/login", "Anyone: no token needed.")]
    [InlineData("get /api/organizations", "Any logged-in user.")]
    [InlineData("post /api/organizations/{organizationId}/members", "Admins of the organization.")]
    [InlineData("delete /api/organizations/{organizationId}/members/{userId}", "Any member of the organization.")]
    [InlineData("get /api/organizations/{organizationId}/projects/{projectId}/tasks", "in the project: project manager, developer, viewer.")]
    [InlineData("post /api/organizations/{organizationId}/projects/{projectId}/tasks", "in the project: project manager, developer.")]
    [InlineData("put /api/organizations/{organizationId}/projects/{projectId}/tasks/{taskId}", "Developers only on tasks they reported or are assigned to.")]
    [InlineData("delete /api/organizations/{organizationId}/projects/{projectId}/tasks/{taskId}", "in the project: project manager.")]
    public async Task Who_can_call_follows_the_authorization_of_the_endpoint(string operation, string expected)
    {
        var operations = Operations(await DocumentAsync("en"));

        Assert.Contains(expected, operations[operation].GetProperty("description").GetString());
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
        var operations = Operations(await DocumentAsync("en"));

        var requiresBearer = operations[operation].TryGetProperty("security", out var security)
            && security.EnumerateArray().Any(requirement => requirement.TryGetProperty("Bearer", out _));

        Assert.Equal(secured, requiresBearer);
    }

    [Theory]
    [InlineData("post /api/organizations/{organizationId}/projects/{projectId}/tasks/{taskId}/status", "422", "Task.InvalidTransition")]
    [InlineData("post /api/organizations/{organizationId}/projects/{projectId}/tasks/{taskId}/status", "403", "Task.NotYourTask")]
    [InlineData("post /api/organizations/{organizationId}/projects/{projectId}/tasks/{taskId}/status", "401", "Authentication.Required")]
    [InlineData("post /api/organizations/{organizationId}/projects/{projectId}/tasks/{taskId}/status", "404", "Project.NotFound")]
    [InlineData("post /api/organizations/{organizationId}/projects/{projectId}/sprints/{sprintId}/start", "409", "Sprint.AnotherSprintActive")]
    [InlineData("post /api/organizations/{organizationId}/members", "404", "Organization.NotFound")]
    [InlineData("post /api/organizations/{organizationId}/members", "400", "Validation.Failed")]
    [InlineData("post /api/auth/login", "401", "Authentication.InvalidCredentials")]
    public async Task Error_responses_list_their_codes(string operation, string status, string code)
    {
        var operations = Operations(await DocumentAsync("en"));

        Assert.Contains($"`{code}`", operations[operation].GetProperty("responses").GetProperty(status).GetProperty("description").GetString());
    }

    [Fact]
    public async Task Codes_without_a_reachable_path_are_not_documented()
    {
        var operations = Operations(await DocumentAsync("en"));

        // Only project managers and admins can reopen tasks, and they can change any task.
        var reopen = operations["post /api/organizations/{organizationId}/projects/{projectId}/tasks/{taskId}/reopen"];
        Assert.DoesNotContain("Task.NotYourTask", reopen.GetRawText());
        // Login is anonymous: a 401 there means wrong credentials, never a missing token.
        Assert.DoesNotContain("Authentication.Required", operations["post /api/auth/login"].GetRawText());
        // Viewers can view: the view permission never answers 403.
        Assert.False(operations["get /api/organizations/{organizationId}/projects/{projectId}/tasks"].GetProperty("responses").TryGetProperty("403", out _));
    }

    [Theory]
    [InlineData("en")]
    [InlineData("es")]
    public async Task Every_error_code_is_explained_and_every_error_response_has_a_problem_example(string language)
    {
        var operations = Operations(await DocumentAsync(language));

        var errorResponses = operations.SelectMany(operation => operation.Value.GetProperty("responses").EnumerateObject()
            .Where(response => int.Parse(response.Name) >= 400)
            .Select(response => (Key: $"{operation.Key} {response.Name}", Response: response.Value)))
            .ToList();

        Assert.NotEmpty(errorResponses);
        Assert.DoesNotContain(errorResponses, error => error.Response.GetProperty("description").GetString()!
            .Split('\n').Skip(1).Any(line => line.EndsWith("`: ", StringComparison.Ordinal) || !line.StartsWith("- `", StringComparison.Ordinal)));
        Assert.DoesNotContain(errorResponses, error =>
            !error.Response.GetProperty("content").TryGetProperty("application/problem+json", out var problem)
            || !problem.GetProperty("example").TryGetProperty("code", out _));
    }

    [Fact]
    public async Task Every_body_has_an_example()
    {
        var document = await DocumentAsync("en");
        var schemas = document.GetProperty("components").GetProperty("schemas");

        var bodies = Operations(document).SelectMany(operation => BodySchemas(operation.Value)).Distinct().ToList();

        Assert.True(bodies.Count > 30, $"Only {bodies.Count} bodies found.");
        Assert.DoesNotContain(bodies, schema => !schemas.GetProperty(schema).TryGetProperty("examples", out _));
    }

    [Fact]
    public async Task Examples_are_realistic()
    {
        var schemas = (await DocumentAsync("es")).GetProperty("components").GetProperty("schemas");

        var login = schemas.GetProperty("LoginCommand").GetProperty("examples")[0];
        var comment = schemas.GetProperty("CommentRequest").GetProperty("examples")[0];
        var task = schemas.GetProperty("TaskResponse").GetProperty("examples")[0];

        Assert.Equal("ana.admin@example.com", login.GetProperty("email").GetString());
        Assert.StartsWith("¡Corregido!", comment.GetProperty("body").GetString());
        Assert.Equal("WEB-12", task.GetProperty("key").GetString());
        Assert.Equal("InProgress", task.GetProperty("status").GetString());
    }

    [Fact]
    public async Task Request_fields_and_parameters_are_described_in_both_languages()
    {
        var english = await DocumentAsync("en");
        var spanish = await DocumentAsync("es");

        var missingFields = RequestFields(english, spanish).Where(field => !Translated(field.English, field.Spanish, "description")).Select(field => field.Name);
        var missingParameters = Parameters(english, spanish).Where(parameter => !Translated(parameter.English, parameter.Spanish, "description")).Select(parameter => parameter.Name);

        Assert.Empty(missingFields);
        Assert.Empty(missingParameters);
        Assert.Contains("WEB-12", english.GetProperty("components").GetProperty("schemas").GetProperty("CreateProjectRequest")
            .GetProperty("properties").GetProperty("key").GetProperty("description").GetString());
    }

    [Fact]
    public async Task Query_parameters_are_camel_case_like_the_json_fields()
    {
        var search = Operations(await DocumentAsync("en"))["get /api/organizations/{organizationId}/projects/{projectId}/tasks"];

        var names = search.GetProperty("parameters").EnumerateArray()
            .Where(parameter => parameter.GetProperty("in").GetString() == "query")
            .Select(parameter => parameter.GetProperty("name").GetString())
            .ToList();

        Assert.Contains("pageSize", names);
        Assert.Contains("status", names);
        Assert.All(names, name => Assert.True(char.IsLower(name![0]), name));
    }

    [Theory]
    [InlineData("en", "Authentication", "Access")]
    [InlineData("es", "Autenticación", "Acceso")]
    public async Task Tags_are_named_described_ordered_and_grouped(string language, string firstTag, string firstGroup)
    {
        var document = await DocumentAsync(language);

        var tags = document.GetProperty("tags").EnumerateArray().ToList();
        var groups = document.GetProperty("x-tagGroups").EnumerateArray().ToList();
        var taggedOperations = Operations(document).Values.Select(operation => operation.GetProperty("tags")[0].GetString()).ToHashSet();

        Assert.Equal(firstTag, tags[0].GetProperty("name").GetString());
        Assert.All(tags, tag => Assert.False(string.IsNullOrWhiteSpace(tag.GetProperty("description").GetString())));
        Assert.Equal(firstGroup, groups[0].GetProperty("name").GetString());
        Assert.Equal(
            tags.Select(tag => tag.GetProperty("name").GetString()).Order(),
            groups.SelectMany(group => group.GetProperty("tags").EnumerateArray().Select(tag => tag.GetString())).Order());
        Assert.Subset(tags.Select(tag => tag.GetProperty("name").GetString()).ToHashSet(), taggedOperations);
    }

    [Fact]
    public async Task Scalar_offers_both_languages_and_the_root_redirects_to_it()
    {
        var client = api.CreateClient(new WebApplicationFactoryClientOptions { AllowAutoRedirect = false });

        var root = await client.GetAsync("/");
        var reference = await api.CreateClient().GetAsync("/scalar");
        var html = await reference.Content.ReadAsStringAsync();

        Assert.Equal(HttpStatusCode.Redirect, root.StatusCode);
        Assert.Equal("/scalar", root.Headers.Location?.ToString());
        Assert.Equal(HttpStatusCode.OK, reference.StatusCode);
        Assert.Equal("text/html", reference.Content.Headers.ContentType?.MediaType);
        Assert.Contains("\"url\":\"openapi/en.json\"", html);
        Assert.Contains("\"url\":\"openapi/es.json\"", html);
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
        Assert.Contains((await client.GetAsync("/openapi/en.json")).StatusCode, notExposed);
        Assert.Contains((await client.GetAsync("/openapi/es.json")).StatusCode, notExposed);
        Assert.Contains((await client.GetAsync("/scalar")).StatusCode, notExposed);
        Assert.Contains((await client.GetAsync("/")).StatusCode, notExposed);
    }

    private async Task<JsonElement> DocumentAsync(string language)
    {
        var response = await api.CreateClient().GetAsync($"/openapi/{language}.json");
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

    /// <summary>Present and non-empty in both languages, and actually translated.</summary>
    private static bool Translated(JsonElement english, JsonElement spanish, string property) =>
        english.TryGetProperty(property, out var en) && spanish.TryGetProperty(property, out var es)
        && !string.IsNullOrWhiteSpace(en.GetString()) && !string.IsNullOrWhiteSpace(es.GetString())
        && en.GetString() != es.GetString();

    /// <summary>Component names of the request body and of the 2xx response bodies (or their items).</summary>
    private static IEnumerable<string> BodySchemas(JsonElement operation)
    {
        var bodies = new List<JsonElement>();
        if (operation.TryGetProperty("requestBody", out var request))
        {
            bodies.AddRange(request.GetProperty("content").EnumerateObject().Select(media => media.Value));
        }

        bodies.AddRange(operation.GetProperty("responses").EnumerateObject()
            .Where(response => int.Parse(response.Name) < 300 && response.Value.TryGetProperty("content", out _))
            .SelectMany(response => response.Value.GetProperty("content").EnumerateObject().Select(media => media.Value)));

        foreach (var schema in bodies.Select(media => media.GetProperty("schema")))
        {
            var reference = schema.TryGetProperty("items", out var items) ? items : schema;
            if (reference.TryGetProperty("$ref", out var name))
            {
                yield return name.GetString()!.Split('/')[^1];
            }
        }
    }

    private static IEnumerable<(string Name, JsonElement English, JsonElement Spanish)> RequestFields(JsonElement english, JsonElement spanish)
    {
        var englishSchemas = english.GetProperty("components").GetProperty("schemas");
        var spanishSchemas = spanish.GetProperty("components").GetProperty("schemas");
        var requests = Operations(english).Values
            .Where(operation => operation.TryGetProperty("requestBody", out _))
            .SelectMany(operation => BodySchemas(operation).Take(1))
            .Distinct();

        foreach (var name in requests)
        {
            yield return (name, englishSchemas.GetProperty(name), spanishSchemas.GetProperty(name));
            foreach (var property in englishSchemas.GetProperty(name).GetProperty("properties").EnumerateObject())
            {
                yield return ($"{name}.{property.Name}", property.Value,
                    spanishSchemas.GetProperty(name).GetProperty("properties").GetProperty(property.Name));
            }
        }
    }

    private static IEnumerable<(string Name, JsonElement English, JsonElement Spanish)> Parameters(JsonElement english, JsonElement spanish)
    {
        var spanishOperations = Operations(spanish);
        foreach (var (key, operation) in Operations(english))
        {
            if (!operation.TryGetProperty("parameters", out var parameters))
            {
                continue;
            }

            var spanishParameters = spanishOperations[key].GetProperty("parameters").EnumerateArray().ToList();
            foreach (var (parameter, index) in parameters.EnumerateArray().Select((parameter, index) => (parameter, index)))
            {
                yield return ($"{key} {parameter.GetProperty("name").GetString()}", parameter, spanishParameters[index]);
            }
        }
    }
}
