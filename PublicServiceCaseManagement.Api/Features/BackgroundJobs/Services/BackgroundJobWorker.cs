namespace PublicServiceCaseManagement.Api.Features.BackgroundJobs.Services;

public sealed class BackgroundJobWorker(
    IServiceScopeFactory scopeFactory,
    IConfiguration configuration,
    ILogger<BackgroundJobWorker> logger) : BackgroundService
{
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        var lastPrunedAt = DateTimeOffset.MinValue;
        var concurrency = Math.Clamp(
            configuration.GetValue("BackgroundJobs:Concurrency", 2),
            1,
            16);
        var pollingMilliseconds = Math.Clamp(
            configuration.GetValue("BackgroundJobs:PollingMilliseconds", 1000),
            100,
            60_000);

        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                if (DateTimeOffset.UtcNow - lastPrunedAt >= TimeSpan.FromHours(24))
                {
                    await PruneExpiredAsync(stoppingToken);
                    lastPrunedAt = DateTimeOffset.UtcNow;
                }

                var tasks = Enumerable.Range(0, concurrency)
                    .Select(_ => ProcessOneAsync(stoppingToken))
                    .ToArray();
                var results = await Task.WhenAll(tasks);
                if (!results.Any(processed => processed))
                    await Task.Delay(pollingMilliseconds, stoppingToken);
            }

            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
                break;
            }
            catch (Exception)
            {
                logger.LogWarning(
                    "Background job polling failed; the worker will retry after the configured polling delay.");
                await Task.Delay(pollingMilliseconds, stoppingToken);
            }
        }
    }

    private async Task PruneExpiredAsync(CancellationToken cancellationToken)
    {
        await using var scope = scopeFactory.CreateAsyncScope();
        await scope.ServiceProvider
            .GetRequiredService<BackgroundJobProcessor>()
            .PruneExpiredAsync(cancellationToken);
    }

    private async Task<bool> ProcessOneAsync(CancellationToken cancellationToken)
    {
        await using var scope = scopeFactory.CreateAsyncScope();
        var processor = scope.ServiceProvider
            .GetRequiredService<BackgroundJobProcessor>();
        return await processor.ProcessOneAsync(cancellationToken);
    }
}
