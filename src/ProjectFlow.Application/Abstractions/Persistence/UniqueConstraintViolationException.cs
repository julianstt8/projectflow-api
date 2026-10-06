namespace ProjectFlow.Application.Abstractions.Persistence;

/// <summary>
/// The database rejected a duplicate value. Use cases check uniqueness first; this covers the race where
/// two requests pass that check at the same time and the unique index stops the second one.
/// </summary>
public sealed class UniqueConstraintViolationException(string? constraintName, Exception innerException)
    : Exception("A record with the same unique value already exists.", innerException)
{
    public string? ConstraintName { get; } = constraintName;
}
