using CareerVault.Api.Models;

namespace CareerVault.Api.Services;

public sealed class TelegramBackgroundWorker(
    TelegramUpdateQueue queue,
    IServiceScopeFactory scopeFactory,
    ILogger<TelegramBackgroundWorker> logger) : BackgroundService
{
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        await foreach (var update in queue.ReadAllAsync(stoppingToken))
        {
            await ProcessUpdateAsync(update, stoppingToken);
        }
    }

    private async Task ProcessUpdateAsync(TelegramUpdate update, CancellationToken stoppingToken)
    {
        try
        {
            using var scope = scopeFactory.CreateScope();
            var handler = scope.ServiceProvider.GetRequiredService<TelegramUpdateHandler>();
            await handler.HandleAsync(update, stoppingToken);
        }
        catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
        {
            throw;
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Erro inesperado no processamento em background do update Telegram {UpdateId}", update.UpdateId);
        }
    }
}
