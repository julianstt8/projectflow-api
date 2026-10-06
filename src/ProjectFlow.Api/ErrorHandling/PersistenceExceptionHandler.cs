using Microsoft.AspNetCore.Diagnostics;
using Microsoft.AspNetCore.Mvc;
using ProjectFlow.Application.Abstractions.Persistence;

namespace ProjectFlow.Api.ErrorHandling;

/// <summary>
/// Another request got there first: a concurrent change or a duplicate unique value. Both are answered
/// with 409 so the client can reload and retry, instead of a 500.
/// </summary>
internal sealed class PersistenceExceptionHandler(IProblemDetailsService problemDetailsService) : IExceptionHandler
{
    public async ValueTask<bool> TryHandleAsync(HttpContext httpContext, Exception exception, CancellationToken cancellationToken)
    {
        var code = exception switch
        {
            ConcurrencyConflictException => "Conflict.Concurrency",
            UniqueConstraintViolationException => "Conflict.Duplicate",
            _ => null,
        };

        if (code is null)
        {
            return false;
        }

        httpContext.Response.StatusCode = StatusCodes.Status409Conflict;

        return await problemDetailsService.TryWriteAsync(new ProblemDetailsContext
        {
            HttpContext = httpContext,
            Exception = exception,
            ProblemDetails = new ProblemDetails
            {
                Status = StatusCodes.Status409Conflict,
                Title = exception.Message,
                Extensions = { ["code"] = code },
            },
        });
    }
}
