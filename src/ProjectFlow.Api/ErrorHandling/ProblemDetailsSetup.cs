using ProjectFlow.Domain.Common;

namespace ProjectFlow.Api.ErrorHandling;

internal static class ProblemDetailsSetup
{
    /// <summary>HTTP status for each kind of expected failure. The single place where this mapping lives.</summary>
    public static int ToStatusCode(this ErrorType type) => type switch
    {
        ErrorType.Validation => StatusCodes.Status400BadRequest,
        ErrorType.Unauthorized => StatusCodes.Status401Unauthorized,
        ErrorType.Forbidden => StatusCodes.Status403Forbidden,
        ErrorType.NotFound => StatusCodes.Status404NotFound,
        ErrorType.Conflict => StatusCodes.Status409Conflict,
        ErrorType.BusinessRule => StatusCodes.Status422UnprocessableEntity,
        _ => StatusCodes.Status500InternalServerError,
    };

    /// <summary>
    /// Every error response is RFC 9457 ProblemDetails with a stable machine-readable <c>code</c>, including
    /// the ones produced by the framework itself (401 challenge, unknown route, model binding, 500).
    /// </summary>
    public static IServiceCollection AddProjectFlowProblemDetails(this IServiceCollection services)
    {
        services.AddProblemDetails(options => options.CustomizeProblemDetails = context =>
        {
            context.ProblemDetails.Extensions.TryAdd("code", DefaultCode(context.ProblemDetails.Status));
        });
        services.AddExceptionHandler<ValidationExceptionHandler>();
        services.AddExceptionHandler<PersistenceExceptionHandler>();

        return services;
    }

    private static string DefaultCode(int? status) => status switch
    {
        StatusCodes.Status400BadRequest => "Request.Invalid",
        StatusCodes.Status401Unauthorized => "Authentication.Required",
        StatusCodes.Status403Forbidden => "Authorization.Forbidden",
        StatusCodes.Status404NotFound => "Resource.NotFound",
        StatusCodes.Status405MethodNotAllowed => "Request.MethodNotAllowed",
        StatusCodes.Status409Conflict => "Conflict",
        StatusCodes.Status415UnsupportedMediaType => "Request.UnsupportedMediaType",
        >= 500 => "Server.Error",
        _ => "Request.Failed",
    };
}
