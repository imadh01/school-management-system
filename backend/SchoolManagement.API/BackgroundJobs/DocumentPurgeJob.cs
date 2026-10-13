using SchoolManagement.Application.Interfaces;
using SchoolManagement.Application.Settings;

namespace SchoolManagement.API.BackgroundJobs;

/// <summary>
/// G1b: once a day, removes the FILES of documents deleted more than
/// Documents:DeletedFileRetentionDays ago (default 90). The rows stay, marked FilePurgedAt, as the
/// record that the document existed and who deleted it. Until the purge a deleted file can still be
/// recovered by an administrator.
///
/// Same pattern as RefreshTokenCleanupJob: a few minutes' delay after startup, one run a day, a failed
/// run is logged and simply retried the next day.
/// </summary>
public sealed class DocumentPurgeJob : BackgroundService
{
    private static readonly TimeSpan StartupDelay = TimeSpan.FromMinutes(10);
    private static readonly TimeSpan Interval = TimeSpan.FromHours(24);

    private readonly IServiceScopeFactory _scopeFactory;
    private readonly DocumentSettings _settings;
    private readonly ILogger<DocumentPurgeJob> _logger;

    public DocumentPurgeJob(IServiceScopeFactory scopeFactory, DocumentSettings settings, ILogger<DocumentPurgeJob> logger)
    {
        _scopeFactory = scopeFactory;
        _settings = settings;
        _logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        if (!_settings.PurgeEnabled)
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
            using var scope = _scopeFactory.CreateScope();
            var documents = scope.ServiceProvider.GetRequiredService<IDocumentService>();

            var purged = await documents.PurgeDeletedFilesAsync(stoppingToken);
            _logger.LogInformation(
                "Document purge removed {PurgedCount} file(s) deleted more than {RetentionDays} day(s) ago",
                purged, _settings.DeletedFileRetentionDays);
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            _logger.LogError(ex, "Document purge failed; it will run again in {Interval}", Interval);
        }
    }
}
