namespace ProjectFlow.Application.Abstractions.Persistence;

/// <summary>Another request changed the same data first; the current change was not saved.</summary>
public sealed class ConcurrencyConflictException(Exception innerException)
    : Exception("The data was changed by another request. Reload it and try again.", innerException);
