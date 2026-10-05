namespace ProjectFlow.Domain.Common;

internal static class Text
{
    public static Result<string> Required(string? value, int maxLength, Error required, Error tooLong)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return required;
        }

        var trimmed = value.Trim();
        if (trimmed.Length > maxLength)
        {
            return tooLong;
        }

        return trimmed;
    }

    public static Result<string?> Optional(string? value, int maxLength, Error tooLong)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return Result.Success<string?>(null);
        }

        var trimmed = value.Trim();
        if (trimmed.Length > maxLength)
        {
            return tooLong;
        }

        return trimmed;
    }
}
