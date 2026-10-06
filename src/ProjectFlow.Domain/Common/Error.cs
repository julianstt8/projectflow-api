namespace ProjectFlow.Domain.Common;

public enum ErrorType
{
    None,
    Validation,
    NotFound,

    /// <summary>The request collides with existing data (duplicate value, concurrent change).</summary>
    Conflict,

    /// <summary>The request is well formed but breaks a business rule (e.g. invalid status transition).</summary>
    BusinessRule,
    Forbidden,
    Unauthorized,
}

public sealed record Error(string Code, string Description, ErrorType Type)
{
    public static readonly Error None = new(string.Empty, string.Empty, ErrorType.None);

    public static Error Validation(string code, string description) => new(code, description, ErrorType.Validation);

    public static Error NotFound(string code, string description) => new(code, description, ErrorType.NotFound);

    public static Error Conflict(string code, string description) => new(code, description, ErrorType.Conflict);

    public static Error BusinessRule(string code, string description) => new(code, description, ErrorType.BusinessRule);

    public static Error Forbidden(string code, string description) => new(code, description, ErrorType.Forbidden);

    public static Error Unauthorized(string code, string description) => new(code, description, ErrorType.Unauthorized);
}
