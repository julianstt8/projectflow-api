using Microsoft.EntityFrameworkCore;
using Npgsql;

namespace ProjectFlow.Api.IntegrationTests.Persistence;

internal static class DatabaseAssert
{
    /// <summary>Asserts that <paramref name="action"/> is rejected by PostgreSQL with <paramref name="sqlState"/> on <paramref name="constraint"/>.</summary>
    public static async Task RejectedAsync(Func<Task> action, string sqlState, string constraint)
    {
        var exception = await Record.ExceptionAsync(action);

        var postgresException = exception switch
        {
            PostgresException direct => direct,
            DbUpdateException { InnerException: PostgresException inner } => inner,
            _ => throw new Xunit.Sdk.XunitException($"Expected a PostgreSQL error {sqlState} on {constraint}, got: {exception?.GetType().Name ?? "no exception"}"),
        };

        Assert.Equal(sqlState, postgresException.SqlState);
        Assert.Equal(constraint, postgresException.ConstraintName);
    }
}
