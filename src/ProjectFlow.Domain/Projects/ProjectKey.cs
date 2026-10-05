using System.Text.RegularExpressions;
using ProjectFlow.Domain.Common;

namespace ProjectFlow.Domain.Projects;

/// <summary>Short project code used as task key prefix, e.g. <c>PRJ</c> in <c>PRJ-12</c>.</summary>
public sealed partial record ProjectKey
{
    public const int MinLength = 2;
    public const int MaxLength = 10;

    private ProjectKey(string value)
    {
        Value = value;
    }

    public string Value { get; }

    public static Result<ProjectKey> Create(string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return ProjectErrors.KeyRequired;
        }

        var normalized = value.Trim().ToUpperInvariant();

        if (!Pattern().IsMatch(normalized))
        {
            return ProjectErrors.KeyInvalid;
        }

        return new ProjectKey(normalized);
    }

    public override string ToString() => Value;

    [GeneratedRegex("^[A-Z][A-Z0-9]{1,9}$", RegexOptions.CultureInvariant)]
    private static partial Regex Pattern();
}
