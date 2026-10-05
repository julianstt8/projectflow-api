namespace ProjectFlow.Infrastructure.Persistence;

internal static class PersistenceConstants
{
    public const string ConnectionStringName = "Default";

    /// <summary>Max length of enum columns stored as text.</summary>
    public const int EnumMaxLength = 20;

    /// <summary>
    /// Shadow concurrency token mapped to PostgreSQL's <c>xmin</c> system column, so concurrent updates
    /// (e.g. two tasks taking the same <c>NextTaskNumber</c>) fail instead of silently overwriting each other.
    /// </summary>
    public const string RowVersion = "Version";
}
