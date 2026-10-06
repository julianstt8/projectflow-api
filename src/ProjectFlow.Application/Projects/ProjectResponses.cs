using ProjectFlow.Domain.Projects;

namespace ProjectFlow.Application.Projects;

/// <param name="MyRole">The current user's project role; <see langword="null"/> for organization admins without one.</param>
public sealed record ProjectSummaryResponse(Guid Id, string Key, string Name, bool IsArchived, ProjectRole? MyRole);

public sealed record ProjectDetailsResponse(
    Guid Id,
    string Key,
    string Name,
    string? Description,
    bool IsArchived,
    DateTimeOffset CreatedAt,
    IReadOnlyList<ProjectMemberResponse> Members);

public sealed record ProjectMemberResponse(Guid UserId, string Email, string FullName, ProjectRole Role);
