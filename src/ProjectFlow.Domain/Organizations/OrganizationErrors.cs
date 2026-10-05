using ProjectFlow.Domain.Common;

namespace ProjectFlow.Domain.Organizations;

public static class OrganizationErrors
{
    public static readonly Error NameRequired =
        Error.Validation("Organization.NameRequired", "The organization name is required.");

    public static readonly Error NameTooLong =
        Error.Validation("Organization.NameTooLong", $"The organization name cannot exceed {Organization.NameMaxLength} characters.");

    public static readonly Error SlugRequired =
        Error.Validation("Organization.SlugRequired", "The organization slug is required.");

    public static readonly Error SlugInvalid =
        Error.Validation(
            "Organization.SlugInvalid",
            $"The slug must be {Slug.MinLength}-{Slug.MaxLength} lowercase letters, digits or single hyphens, without leading or trailing hyphens.");

    public static readonly Error UserRequired =
        Error.Validation("Organization.UserRequired", "A valid user id is required.");

    public static readonly Error RoleInvalid =
        Error.Validation("Organization.RoleInvalid", "The organization role is not valid.");

    public static readonly Error MemberAlreadyExists =
        Error.Conflict("Organization.MemberAlreadyExists", "The user is already a member of the organization.");

    public static readonly Error MemberNotFound =
        Error.NotFound("Organization.MemberNotFound", "The user is not a member of the organization.");

    public static readonly Error LastAdmin =
        Error.Conflict("Organization.LastAdmin", "The last admin of an organization cannot be removed or demoted.");
}
