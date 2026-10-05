using ProjectFlow.Domain.Organizations;

namespace ProjectFlow.Application.Organizations;

public sealed record OrganizationSummaryResponse(Guid Id, string Name, string Slug, OrganizationRole Role);

public sealed record OrganizationDetailsResponse(
    Guid Id,
    string Name,
    string Slug,
    DateTimeOffset CreatedAt,
    IReadOnlyList<OrganizationMemberResponse> Members);

public sealed record OrganizationMemberResponse(
    Guid UserId,
    string Email,
    string FullName,
    OrganizationRole Role,
    DateTimeOffset JoinedAt);
