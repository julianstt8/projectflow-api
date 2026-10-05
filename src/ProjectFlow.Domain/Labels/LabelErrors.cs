using ProjectFlow.Domain.Common;

namespace ProjectFlow.Domain.Labels;

public static class LabelErrors
{
    public static readonly Error NameRequired =
        Error.Validation("Label.NameRequired", "The label name is required.");

    public static readonly Error NameTooLong =
        Error.Validation("Label.NameTooLong", $"The label name cannot exceed {Label.NameMaxLength} characters.");

    public static readonly Error ColorInvalid =
        Error.Validation("Label.ColorInvalid", "The color must be a hex value like #1D76DB.");
}
