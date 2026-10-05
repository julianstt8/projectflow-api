using ProjectFlow.Domain.Common;

namespace ProjectFlow.Domain.Epics;

public static class EpicErrors
{
    public static readonly Error NameRequired =
        Error.Validation("Epic.NameRequired", "The epic name is required.");

    public static readonly Error NameTooLong =
        Error.Validation("Epic.NameTooLong", $"The epic name cannot exceed {Epic.NameMaxLength} characters.");

    public static readonly Error DescriptionTooLong =
        Error.Validation("Epic.DescriptionTooLong", $"The epic description cannot exceed {Epic.DescriptionMaxLength} characters.");

    public static readonly Error AlreadyClosed =
        Error.Conflict("Epic.AlreadyClosed", "The epic is already closed.");

    public static readonly Error AlreadyOpen =
        Error.Conflict("Epic.AlreadyOpen", "The epic is already open.");
}
