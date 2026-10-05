using System.Text.RegularExpressions;
using ProjectFlow.Domain.Common;

namespace ProjectFlow.Domain.Users;

public sealed partial record Email
{
    public const int MaxLength = 254;

    private Email(string value)
    {
        Value = value;
    }

    public string Value { get; }

    public static Result<Email> Create(string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return UserErrors.EmailRequired;
        }

        var normalized = value.Trim().ToLowerInvariant();

        if (normalized.Length > MaxLength || !Pattern().IsMatch(normalized))
        {
            return UserErrors.EmailInvalid;
        }

        return new Email(normalized);
    }

    public override string ToString() => Value;

    [GeneratedRegex(@"^[^@\s]+@[^@\s]+\.[^@\s]+$", RegexOptions.CultureInvariant)]
    private static partial Regex Pattern();
}
