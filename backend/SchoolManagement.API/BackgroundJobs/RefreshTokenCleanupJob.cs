using SchoolManagement.Application.Interfaces;
using SchoolManagement.Application.Settings;

namespace SchoolManagement.API.BackgroundJobs;

/// <summary>
/// Once a day, deletes refresh-token families that ended more than RetentionDays ago.
/// Without it the table grows by one row per user every 15 minutes, forever.
///
/// It waits a few minutes after startup so it never competes with the app (or the test
/// database setup) while starting. A failed run is logged and retried the next day: losing
/// one cleanup is harmless.
/// </summary>
public sealed class RefreshTokenCleanupJob : BackgroundService
{
    private static readonly TimeSpan StartupDelay = TimeSpan.FromMinutes(5);
    private static readonly TimeSpan Interval = TimeSpan.FromHours(24);

    private readonly IServiceScopeFactory _scopeFactory;
    private readonly RefreshTokenSettings _settings;
    private readonly ILogger<RefreshTokenCleanupJob> _logger;

    public RefreshTokenCleanupJob(
        IServiceScopeFactory scopeFactory,
        RefreshTokenSettings settings,
        ILogger<RefreshTokenCleanupJob> logger)
    {
        _scopeFactory = scopeFactory;
        _settings = settings;
        _logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        if (!_settings.CleanupEnabled)
            return;

        try
        {
            await Task.Delay(StartupDelay, stoppingToken);

            using var timer = new PeriodicTimer(Interval);
            do
            {
                await RunOnceAsync(stoppingToken);
            }
            while (await timer.WaitForNextTickAsync(stoppingToken));
        }
        catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
        {
            // Normal shutdown.
        }
    }

    private async Task RunOnceAsync(CancellationToken stoppingToken)
    {
        try
        {
            // The job is a singleton; the service and DbContext are scoped, so each run gets its own scope.
            using var scope = _scopeFactory.CreateScope();
            var refreshTokens = scope.ServiceProvider.GetRequiredService<IRefreshTokenService>();

            var deleted = await refreshTokens.PurgeExpiredAsync(stoppingToken);
            _logger.LogInformation("Refresh token cleanup deleted {DeletedCount} expired row(s)", deleted);
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            _logger.LogError(ex, "Refresh token cleanup failed; it will run again in {Interval}", Interval);
        }
    }
}
