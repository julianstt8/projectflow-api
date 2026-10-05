using System.Text.RegularExpressions;
using ProjectFlow.Domain.Common;
using ProjectFlow.Domain.Projects;

namespace ProjectFlow.Domain.Labels;

public sealed partial class Label : Entity
{
    public const int NameMaxLength = 50;

    private Label(Guid id, Guid projectId, Guid organizationId, string name, string color)
        : base(id)
    {
        ProjectId = projectId;
        OrganizationId = organizationId;
        Name = name;
        Color = color;
    }

    public Guid ProjectId { get; }

    public Guid OrganizationId { get; }

    public string Name { get; private set; }

    /// <summary>Hex color in the form <c>#RRGGBB</c>, stored uppercase.</summary>
    public string Color { get; private set; }

    public static Result<Label> Create(Project project, string name, string color, DateTimeOffset now)
    {
        var canChange = project.EnsureCanBeChanged();
        if (canChange.IsFailure)
        {
            return canChange.Error;
        }

        var details = ValidateDetails(name, color);
        if (details.IsFailure)
        {
            return details.Error;
        }

        var (validName, validColor) = details.Value;
        return new Label(Guid.CreateVersion7(now), project.Id, project.OrganizationId, validName, validColor);
    }

    public Result Update(string name, string color)
    {
        var details = ValidateDetails(name, color);
        if (details.IsFailure)
        {
            return details.Error;
        }

        (Name, Color) = details.Value;
        return Result.Success();
    }

    private static Result<(string Name, string Color)> ValidateDetails(string? name, string? color)
    {
        var validName = Text.Required(name, NameMaxLength, LabelErrors.NameRequired, LabelErrors.NameTooLong);
        if (validName.IsFailure)
        {
            return validName.Error;
        }

        var normalizedColor = color?.Trim().ToUpperInvariant();
        if (normalizedColor is null || !ColorPattern().IsMatch(normalizedColor))
        {
            return LabelErrors.ColorInvalid;
        }

        return (validName.Value, normalizedColor);
    }

    [GeneratedRegex("^#[0-9A-F]{6}$", RegexOptions.CultureInvariant)]
    private static partial Regex ColorPattern();
}
