using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace ProjectFlow.Infrastructure.Persistence.Seeding;

/// <summary>
/// Checks every hour (and on startup) whether the demo data is due for a reset. A fixed time of day would be
/// missed while Render's free service sleeps, so the check runs whenever the API is awake.
/// </summary>
internal sealed class DemoDataResetService(
    IServiceScopeFactory scopes,
    IOptions<DemoOptions> options,
    TimeProvider timeProvider,
    ILogger<DemoDataResetService> logger) : BackgroundService
{
    private static readonly TimeSpan CheckInterval = TimeSpan.FromHours(1);

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        var maxAge = TimeSpan.FromHours(options.Value.ResetIntervalHours);
        using var timer = new PeriodicTimer(CheckInterval, timeProvider);

        do
        {
            try
            {
                await using var scope = scopes.CreateAsyncScope();
                await scope.ServiceProvider.GetRequiredService<DemoDataReset>().ResetIfStaleAsync(maxAge, stoppingToken);
            }
            catch (Exception exception) when (exception is not OperationCanceledException)
            {
                // A failed reset must not stop the API; the next check tries again.
                logger.LogError(exception, "Demo data reset failed");
            }
        }
        while (await timer.WaitForNextTickAsync(stoppingToken));
    }
}
