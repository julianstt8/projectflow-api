using Mediator;
using Microsoft.AspNetCore.Mvc;
using ProjectFlow.Api.Authorization;
using ProjectFlow.Application.Comments;
using ProjectFlow.Domain.Common;
using ProjectFlow.Domain.Projects;

namespace ProjectFlow.Api.Controllers;

/// <summary>Comments on a task (RF-09). Any language and Unicode text (accents, ñ, emoji) is supported.</summary>
[Route("api/organizations/{organizationId:guid}/projects/{projectId:guid}/tasks/{taskId:guid}/comments")]
public sealed class CommentsController(ISender sender) : ApiControllerBase
{
    /// <summary>Comments of the task, oldest first.</summary>
    [HttpGet]
    [RequireProjectPermission(ProjectPermission.ViewProject)]
    [ProducesResponseType<IReadOnlyList<CommentResponse>>(StatusCodes.Status200OK)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> List(Guid organizationId, Guid projectId, Guid taskId, CancellationToken cancellationToken)
    {
        var result = await sender.Send(new GetCommentsQuery(projectId, taskId), cancellationToken);

        return result.IsSuccess ? Ok(result.Value) : ErrorResult(result.Error);
    }

    /// <summary>Adds a comment. Viewers cannot comment.</summary>
    [HttpPost]
    [RequireProjectPermission(ProjectPermission.Comment)]
    [ProducesResponseType<CommentResponse>(StatusCodes.Status201Created)]
    [ProducesResponseType<ValidationProblemDetails>(StatusCodes.Status400BadRequest)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status403Forbidden)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status404NotFound)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status422UnprocessableEntity)]
    public async Task<IActionResult> Add(Guid organizationId, Guid projectId, Guid taskId, CommentRequest request, CancellationToken cancellationToken)
    {
        var result = await sender.Send(new AddCommentCommand(projectId, taskId, request.Body), cancellationToken);

        return result.IsSuccess
            ? StatusCode(StatusCodes.Status201Created, result.Value)
            : ErrorResult(result.Error);
    }

    /// <summary>Edits a comment. Only its author can (403 otherwise).</summary>
    [HttpPut("{commentId:guid}")]
    [RequireProjectPermission(ProjectPermission.Comment)]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType<ValidationProblemDetails>(StatusCodes.Status400BadRequest)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status403Forbidden)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status404NotFound)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status422UnprocessableEntity)]
    public Task<IActionResult> Edit(Guid organizationId, Guid projectId, Guid taskId, Guid commentId, CommentRequest request, CancellationToken cancellationToken) =>
        SendAsync(new EditCommentCommand(projectId, taskId, commentId, request.Body), cancellationToken);

    /// <summary>Deletes a comment: its author, or a project manager or admin.</summary>
    [HttpDelete("{commentId:guid}")]
    [RequireProjectPermission(ProjectPermission.Comment)]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status403Forbidden)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status404NotFound)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status422UnprocessableEntity)]
    public Task<IActionResult> Delete(Guid organizationId, Guid projectId, Guid taskId, Guid commentId, CancellationToken cancellationToken) =>
        SendAsync(new DeleteCommentCommand(organizationId, projectId, taskId, commentId), cancellationToken);

    private async Task<IActionResult> SendAsync(ICommand<Result> command, CancellationToken cancellationToken)
    {
        var result = await sender.Send(command, cancellationToken);

        return result.IsSuccess ? NoContent() : ErrorResult(result.Error);
    }
}

/// <summary>Text of a comment.</summary>
/// <param name="Body">The comment, up to 5000 characters, in any language (accents, ñ and emoji are kept as written).</param>
public sealed record CommentRequest(string Body);
