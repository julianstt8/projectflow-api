using ProjectFlow.Domain.Common;

namespace ProjectFlow.Domain.Projects;

public static class ProjectErrors
{
    public static readonly Error KeyRequired =
        Error.Validation("Project.KeyRequired", "The project key is required.");

    public static readonly Error KeyInvalid =
        Error.Validation(
            "Project.KeyInvalid",
            $"The project key must be {ProjectKey.MinLength}-{ProjectKey.MaxLength} uppercase letters or digits and start with a letter.");

    public static readonly Error NameRequired =
        Error.Validation("Project.NameRequired", "The project name is required.");

    public static readonly Error NameTooLong =
        Error.Validation("Project.NameTooLong", $"The project name cannot exceed {Project.NameMaxLength} characters.");

    public static readonly Error DescriptionTooLong =
        Error.Validation("Project.DescriptionTooLong", $"The description cannot exceed {Project.DescriptionMaxLength} characters.");

    public static readonly Error OrganizationRequired =
        Error.Validation("Project.OrganizationRequired", "A valid organization id is required.");

    public static readonly Error UserRequired =
        Error.Validation("Project.UserRequired", "A valid user id is required.");

    public static readonly Error RoleInvalid =
        Error.Validation("Project.RoleInvalid", "The project role is not valid.");

    public static readonly Error Archived =
        Error.Conflict("Project.Archived", "The project is archived and cannot be changed.");

    public static readonly Error NotArchived =
        Error.Conflict("Project.NotArchived", "The project is not archived.");

    public static readonly Error Deleted =
        Error.Conflict("Project.Deleted", "The project has been deleted.");

    public static readonly Error MemberAlreadyExists =
        Error.Conflict("Project.MemberAlreadyExists", "The user is already a member of the project.");

    public static readonly Error MemberNotFound =
        Error.NotFound("Project.MemberNotFound", "The user is not a member of the project.");

    public static readonly Error LastProjectManager =
        Error.Conflict("Project.LastProjectManager", "A project must keep at least one project manager.");
}
