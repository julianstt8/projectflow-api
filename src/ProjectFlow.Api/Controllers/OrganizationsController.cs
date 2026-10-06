using Mediator;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using ProjectFlow.Api.Authorization;
using ProjectFlow.Application.Organizations;
using ProjectFlow.Domain.Organizations;

namespace ProjectFlow.Api.Controllers;

/// <summary>Organizations and their members (RF-02).</summary>
[Route("api/organizations")]
public sealed class OrganizationsController(ISender sender) : ApiControllerBase
{
    /// <summary>Creates an organization; the current user becomes its admin.</summary>
    [HttpPost]
    [ProducesResponseType<OrganizationSummaryResponse>(StatusCodes.Status201Created)]
    [ProducesResponseType<ValidationProblemDetails>(StatusCodes.Status400BadRequest)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status409Conflict)]
    public async Task<IActionResult> Create(CreateOrganizationCommand command, CancellationToken cancellationToken)
    {
        var result = await sender.Send(command, cancellationToken);

        return result.IsSuccess
            ? CreatedAtAction(nameof(Get), new { organizationId = result.Value.Id }, result.Value)
            : ErrorResult(result.Error);
    }

    /// <summary>Organizations the current user belongs to, with their role.</summary>
    [HttpGet]
    [ProducesResponseType<IReadOnlyList<OrganizationSummaryResponse>>(StatusCodes.Status200OK)]
    public async Task<IActionResult> List(CancellationToken cancellationToken) =>
        Ok(await sender.Send(new GetMyOrganizationsQuery(), cancellationToken));

    /// <summary>An organization and its members. Members only.</summary>
    [HttpGet("{organizationId:guid}")]
    [Authorize(Policy = OrganizationPolicies.Member)]
    [ProducesResponseType<OrganizationDetailsResponse>(StatusCodes.Status200OK)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> Get(Guid organizationId, CancellationToken cancellationToken)
    {
        var result = await sender.Send(new GetOrganizationQuery(organizationId), cancellationToken);

        return result.IsSuccess ? Ok(result.Value) : ErrorResult(result.Error);
    }

    /// <summary>Adds a registered user to the organization. Admins only.</summary>
    [HttpPost("{organizationId:guid}/members")]
    [Authorize(Policy = OrganizationPolicies.Admin)]
    [ProducesResponseType<OrganizationMemberResponse>(StatusCodes.Status201Created)]
    [ProducesResponseType<ValidationProblemDetails>(StatusCodes.Status400BadRequest)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status403Forbidden)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status404NotFound)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status409Conflict)]
    public async Task<IActionResult> AddMember(Guid organizationId, AddMemberRequest request, CancellationToken cancellationToken)
    {
        var result = await sender.Send(new AddOrganizationMemberCommand(organizationId, request.Email, request.Role), cancellationToken);

        return result.IsSuccess
            ? StatusCode(StatusCodes.Status201Created, result.Value)
            : ErrorResult(result.Error);
    }

    /// <summary>Changes a member's role. Admins only; the last admin cannot be demoted (422).</summary>
    [HttpPut("{organizationId:guid}/members/{userId:guid}")]
    [Authorize(Policy = OrganizationPolicies.Admin)]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status403Forbidden)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status404NotFound)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status422UnprocessableEntity)]
    public async Task<IActionResult> ChangeMemberRole(
        Guid organizationId,
        Guid userId,
        ChangeMemberRoleRequest request,
        CancellationToken cancellationToken)
    {
        var result = await sender.Send(new ChangeOrganizationMemberRoleCommand(organizationId, userId, request.Role), cancellationToken);

        return result.IsSuccess ? NoContent() : ErrorResult(result.Error);
    }

    /// <summary>Removes a member. Admins can remove anyone; members can remove themselves (leave).</summary>
    [HttpDelete("{organizationId:guid}/members/{userId:guid}")]
    [Authorize(Policy = OrganizationPolicies.Member)]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status403Forbidden)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status404NotFound)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status422UnprocessableEntity)]
    public async Task<IActionResult> RemoveMember(Guid organizationId, Guid userId, CancellationToken cancellationToken)
    {
        var result = await sender.Send(new RemoveOrganizationMemberCommand(organizationId, userId), cancellationToken);

        return result.IsSuccess ? NoContent() : ErrorResult(result.Error);
    }
}

public sealed record AddMemberRequest(string Email, OrganizationRole Role);

public sealed record ChangeMemberRoleRequest(OrganizationRole Role);
