using Microsoft.AspNetCore.Mvc;
using ProjectFlow.Api.ErrorHandling;
using ProjectFlow.Domain.Common;

namespace ProjectFlow.Api.Controllers;

[ApiController]
public abstract class ApiControllerBase : ControllerBase
{
    /// <summary>Turns an expected failure into a ProblemDetails response with the error code as an extension.</summary>
    protected ObjectResult ErrorResult(Error error)
    {
        var statusCode = error.Type.ToStatusCode();

        var problem = ProblemDetailsFactory.CreateProblemDetails(HttpContext, statusCode, title: error.Description);
        problem.Extensions["code"] = error.Code;

        return new ObjectResult(problem) { StatusCode = statusCode };
    }
}
