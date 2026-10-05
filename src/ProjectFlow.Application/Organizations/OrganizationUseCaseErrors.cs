using ProjectFlow.Domain.Common;

namespace ProjectFlow.Application.Organizations;

public static class OrganizationUseCaseErrors
{
    public static readonly Error SlugAlreadyTaken =
        Error.Conflict("Organization.SlugAlreadyTaken", "Another organization already uses this slug.");

    public static readonly Error NotFound =
        Error.NotFound("Organization.NotFound", "The organization does not exist.");

    public static readonly Error UserNotFound =
        Error.NotFound("Organization.UserNotFound", "No user is registered with this e-mail.");

    public static readonly Error OnlyAdminsCanRemoveOthers =
        Error.Forbidden("Organization.OnlyAdminsCanRemoveOthers", "Only an admin can remove other members.");
}
