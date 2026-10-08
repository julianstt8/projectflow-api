using System.Collections.Concurrent;
using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.Logging;
using ProjectFlow.Api.IntegrationTests.Infrastructure;
using ProjectFlow.Application.Authentication.Login;
using ProjectFlow.Application.Authentication.Register;

namespace ProjectFlow.Api.IntegrationTests.Observability;

/// <summary>Structured request logs with a correlation id, no secrets or personal data, and a database health check.</summary>
[Collection(ApiCollection.Name)]
public class ObservabilityTests(ProjectFlowApiFactory api)
{
    [Fact]
    public async Task Every_response_carries_the_correlation_id_that_problem_details_reports()
    {
        var response = await api.CreateClient().GetAsync("/api/organizations");

        var correlationId = Assert.Single(response.Headers.GetValues("X-Correlation-Id"));
        var problem = await response.Content.ReadFromJsonAsync<JsonElement>();
        Assert.Matches("^[0-9a-f]{32}$", correlationId);
        Assert.Contains(correlationId, problem.GetProperty("traceId").GetString());
    }

    [Fact]
    public async Task Requests_are_logged_with_method_path_status_and_correlation_id()
    {
        var logs = new CapturingLoggerProvider();
        using var host = HostWithLogs(logs);

        var response = await host.CreateClient().GetAsync("/api/organizations");

        var correlationId = Assert.Single(response.Headers.GetValues("X-Correlation-Id"));
        var line = Assert.Single(logs.Entries, entry => entry.Message.StartsWith("HTTP GET /api/organizations", StringComparison.Ordinal));
        Assert.Contains("responded 401", line.Message);
        Assert.Equal(correlationId, line.Properties["CorrelationId"]?.ToString());
    }

    [Fact]
    public async Task Logs_contain_no_passwords_tokens_emails_or_query_strings()
    {
        var logs = new CapturingLoggerProvider();
        using var host = HostWithLogs(logs);
        var client = host.CreateClient();
        var email = $"private-{TestData.Unique()}@example.com";
        const string password = "VerySecret123";

        await client.PostAsJsonAsync("/api/auth/register", new RegisterUserCommand(email, password, "Private Person"));
        var login = await client.PostAsJsonAsync("/api/auth/login", new LoginCommand(email, password));
        var accessToken = (await login.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("accessToken").GetString()!;
        await client.GetAsync("/api/organizations?q=confidential-search-term");

        var everything = string.Join('\n', logs.Entries.Select(entry => entry.Message + " " + string.Join(' ', entry.Properties.Values)));
        Assert.Contains("HTTP POST /api/auth/login responded 200", everything);
        Assert.DoesNotContain(password, everything);
        Assert.DoesNotContain(email, everything);
        Assert.DoesNotContain(accessToken, everything);
        Assert.DoesNotContain("confidential-search-term", everything);
    }

    [Fact]
    public async Task Health_is_unhealthy_when_the_database_is_unreachable()
    {
        using var broken = api.WithWebHostBuilder(builder => builder
            .UseSetting("ConnectionStrings:Default", "Host=127.0.0.1;Port=1;Database=none;Username=none;Password=none;Timeout=2")
            .UseSetting("Database:MigrateOnStartup", "false"));

        var response = await broken.CreateClient().GetAsync("/health");

        Assert.Equal(HttpStatusCode.ServiceUnavailable, response.StatusCode);
        Assert.Equal("Unhealthy", await response.Content.ReadAsStringAsync());
    }

    private WebApplicationFactory<Program> HostWithLogs(CapturingLoggerProvider logs) =>
        api.WithWebHostBuilder(builder => builder.ConfigureLogging(logging => logging.AddProvider(logs)));

    private sealed record LogEntry(string Category, LogLevel Level, string Message, IReadOnlyDictionary<string, object?> Properties);

    /// <summary>Keeps every log entry (message and structured properties) written while the test runs.</summary>
    private sealed class CapturingLoggerProvider : ILoggerProvider, ISupportExternalScope
    {
        private IExternalScopeProvider _scopes = new LoggerExternalScopeProvider();

        public ConcurrentQueue<LogEntry> Entries { get; } = new();

        public ILogger CreateLogger(string categoryName) => new CapturingLogger(categoryName, this);

        public void SetScopeProvider(IExternalScopeProvider scopeProvider) => _scopes = scopeProvider;

        public void Dispose()
        {
        }

        private sealed class CapturingLogger(string category, CapturingLoggerProvider provider) : ILogger
        {
            public IDisposable? BeginScope<TState>(TState state)
                where TState : notnull => provider._scopes.Push(state);

            public bool IsEnabled(LogLevel logLevel) => true;

            public void Log<TState>(LogLevel logLevel, EventId eventId, TState state, Exception? exception, Func<TState, Exception?, string> formatter)
            {
                var properties = new Dictionary<string, object?>();
                if (state is IEnumerable<KeyValuePair<string, object?>> values)
                {
                    foreach (var (key, value) in values)
                    {
                        properties[key] = value;
                    }
                }

                provider._scopes.ForEachScope(
                    (scope, all) =>
                    {
                        if (scope is IEnumerable<KeyValuePair<string, object?>> scopeValues)
                        {
                            foreach (var (key, value) in scopeValues)
                            {
                                all.TryAdd(key, value);
                            }
                        }
                    },
                    properties);

                provider.Entries.Enqueue(new LogEntry(category, logLevel, formatter(state, exception), properties));
            }
        }
    }
}
