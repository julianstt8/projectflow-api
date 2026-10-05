namespace ProjectFlow.Domain.Common;

public enum ErrorType
{
    None,
    Validation,
    NotFound,
    Conflict,
    Forbidden,
}

public sealed record Error(string Code, string Description, ErrorType Type)
{
    public static readonly Error None = new(string.Empty, string.Empty, ErrorType.None);

    public static Error Validation(string code, string description) => new(code, description, ErrorType.Validation);

    public static Error NotFound(string code, string description) => new(code, description, ErrorType.NotFound);

    public static Error Conflict(string code, string description) => new(code, description, ErrorType.Conflict);

    public static Error Forbidden(string code, string description) => new(code, description, ErrorType.Forbidden);
}
