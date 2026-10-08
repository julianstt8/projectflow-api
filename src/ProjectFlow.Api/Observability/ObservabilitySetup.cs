using System.Diagnostics;
using Serilog;
using Serilog.Context;
using Serilog.Events;
using Serilog.Formatting.Compact;

namespace ProjectFlow.Api.Observability;

/// <summary>
/// Structured logging (Serilog, JSON on the console), one log line per request and a correlation id.
/// Nothing that can hold secrets or personal data is logged: no bodies, headers or query strings.
/// </summary>
internal static class ObservabilitySetup
{
    public const string CorrelationIdHeader = "X-Correlation-Id";
    public const string CorrelationIdProperty = "CorrelationId";

    public static WebApplicationBuilder AddProjectFlowLogging(this WebApplicationBuilder builder)
    {
        // Serilog replaces the default providers; providers added later (e.g. by tests) still receive every event.
        builder.Logging.ClearProviders();
        builder.Services.AddSerilog(
            (services, logger) => logger
                .ReadFrom.Configuration(builder.Configuration)
                .ReadFrom.Services(services)
                .Enrich.FromLogContext()
                .WriteTo.Console(new RenderedCompactJsonFormatter()),
            writeToProviders: true);

        return builder;
    }

    /// <summary>
    /// The correlation id is the W3C trace id of the request: the same id that ProblemDetails returns as <c>traceId</c>,
    /// so an error a client reports can be found in the logs.
    /// </summary>
    public static WebApplication UseCorrelationId(this WebApplication app)
    {
        app.Use(async (context, next) =>
        {
            var correlationId = Activity.Current?.TraceId.ToHexString() ?? context.TraceIdentifier;
            context.Response.Headers[CorrelationIdHeader] = correlationId;

            using (LogContext.PushProperty(CorrelationIdProperty, correlationId))
            {
                await next(context);
            }
        });

        return app;
    }

    /// <summary>One line per request with method, path (never the query string), status and duration.</summary>
    public static WebApplication UseProjectFlowRequestLogging(this WebApplication app)
    {
        app.UseSerilogRequestLogging(options =>
        {
            options.MessageTemplate = "HTTP {RequestMethod:l} {RequestPath:l} responded {StatusCode} in {Elapsed:0.0} ms";
            options.GetLevel = (context, _, exception) => exception is not null || context.Response.StatusCode >= 500
                ? LogEventLevel.Error
                : context.Request.Path.StartsWithSegments("/health") ? LogEventLevel.Verbose : LogEventLevel.Information;
        });

        return app;
    }
}
