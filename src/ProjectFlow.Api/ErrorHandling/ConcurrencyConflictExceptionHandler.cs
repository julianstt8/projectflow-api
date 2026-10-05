using Microsoft.AspNetCore.Diagnostics;
using Microsoft.AspNetCore.Mvc;
using ProjectFlow.Application.Abstractions.Persistence;

namespace ProjectFlow.Api.ErrorHandling;

/// <summary>Another request saved the same data first: answer 409 so the client can reload and retry.</summary>
internal sealed class ConcurrencyConflictExceptionHandler(IProblemDetailsService problemDetailsService) : IExceptionHandler
{
    public async ValueTask<bool> TryHandleAsync(HttpContext httpContext, Exception exception, CancellationToken cancellationToken)
    {
        if (exception is not ConcurrencyConflictException)
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
                Extensions = { ["code"] = "Concurrency.Conflict" },
            },
        });
    }
}
