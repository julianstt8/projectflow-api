using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Authorization.Policy;
using Microsoft.AspNetCore.Mvc;

namespace ProjectFlow.Api.Authorization;

/// <summary>
/// Describes a resource that must look non-existent to the user (an organization they are not a member
/// of, a project they cannot see). Use <see cref="For"/> to fail an authorization requirement with it.
/// </summary>
internal sealed record NotFoundFailureReason(string Code, string Title)
{
    public AuthorizationFailureReason For(IAuthorizationHandler handler) => new NotFoundReason(handler, this);

    internal sealed class NotFoundReason(IAuthorizationHandler handler, NotFoundFailureReason reason)
        : AuthorizationFailureReason(handler, reason.Title)
    {
        public NotFoundFailureReason Reason { get; } = reason;
    }
}

/// <summary>
/// Answers 404 instead of 403 when authorization failed because the user cannot see the resource at all,
/// so outsiders cannot even tell whether it exists. Users who can see it but lack a permission still get 403.
/// </summary>
internal sealed class NotFoundAuthorizationResultHandler(IProblemDetailsService problemDetailsService)
    : IAuthorizationMiddlewareResultHandler
{
    private readonly AuthorizationMiddlewareResultHandler _defaultHandler = new();

    public async Task HandleAsync(
        RequestDelegate next,
        HttpContext context,
        AuthorizationPolicy policy,
        PolicyAuthorizationResult authorizeResult)
    {
        var notFound = authorizeResult.Forbidden
            ? authorizeResult.AuthorizationFailure?.FailureReasons
                .OfType<NotFoundFailureReason.NotFoundReason>()
                .Select(reason => reason.Reason)
                .FirstOrDefault()
            : null;

        if (notFound is null)
        {
            await _defaultHandler.HandleAsync(next, context, policy, authorizeResult);
            return;
        }

        context.Response.StatusCode = StatusCodes.Status404NotFound;
        await problemDetailsService.WriteAsync(new ProblemDetailsContext
        {
            HttpContext = context,
            ProblemDetails = new ProblemDetails
            {
                Status = StatusCodes.Status404NotFound,
                Title = notFound.Title,
                Extensions = { ["code"] = notFound.Code },
            },
        });
    }
}
