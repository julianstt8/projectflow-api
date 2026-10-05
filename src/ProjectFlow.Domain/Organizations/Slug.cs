using System.Text.RegularExpressions;
using ProjectFlow.Domain.Common;

namespace ProjectFlow.Domain.Organizations;

public sealed partial record Slug
{
    public const int MinLength = 3;
    public const int MaxLength = 63;

    private Slug(string value)
    {
        Value = value;
    }

    public string Value { get; }

    public static Result<Slug> Create(string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return OrganizationErrors.SlugRequired;
        }

        var normalized = value.Trim().ToLowerInvariant();

        if (normalized.Length is < MinLength or > MaxLength || !Pattern().IsMatch(normalized))
        {
            return OrganizationErrors.SlugInvalid;
        }

        return new Slug(normalized);
    }

    public override string ToString() => Value;

    [GeneratedRegex("^[a-z0-9]+(?:-[a-z0-9]+)*$", RegexOptions.CultureInvariant)]
    private static partial Regex Pattern();
}
