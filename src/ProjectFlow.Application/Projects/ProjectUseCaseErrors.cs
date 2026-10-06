using ProjectFlow.Domain.Common;

namespace ProjectFlow.Application.Projects;

public static class ProjectUseCaseErrors
{
    public static readonly Error KeyAlreadyTaken =
        Error.Conflict("Project.KeyAlreadyTaken", "Another project of the organization already uses this key.");

    public static readonly Error NotFound =
        Error.NotFound("Project.NotFound", "The project does not exist.");

    public static readonly Error UserNotFound =
        Error.NotFound("Project.UserNotFound", "No user is registered with this e-mail.");

    public static readonly Error UserNotInOrganization =
        Error.BusinessRule("Project.UserNotInOrganization", "Only members of the organization can join its projects.");
}
