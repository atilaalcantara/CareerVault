using CareerVault.Api.Options;
using Microsoft.Extensions.Options;

namespace CareerVault.Api.Services;

public sealed class EmbeddingBackgroundWorker(
    IServiceScopeFactory scopeFactory,
    IOptions<EmbeddingWorkerOptions> options,
    IOptions<LocalEmbeddingsOptions> embeddingsOptions,
    ILogger<EmbeddingBackgroundWorker> logger) : BackgroundService
{
    private readonly EmbeddingWorkerOptions _workerOptions = options.Value;
    private readonly LocalEmbeddingsOptions _embeddingOptions = embeddingsOptions.Value;

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        if (!_workerOptions.Enabled)
        {
            logger.LogInformation("Embedding worker desabilitado por configuracao.");
            return;
        }

        logger.LogInformation(
            "Embedding worker iniciado. Intervalo: {IntervalSeconds}s; batch: {BatchSize}; paralelismo: {Parallelism}",
            _workerOptions.IntervalSeconds,
            _workerOptions.BatchSize,
            _workerOptions.MaxDegreeOfParallelism);

        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                await ProcessBatchAsync(stoppingToken);
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
                throw;
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "Erro inesperado no worker de embeddings.");
            }

            await Task.Delay(TimeSpan.FromSeconds(_workerOptions.IntervalSeconds), stoppingToken);
        }
    }

    private async Task ProcessBatchAsync(CancellationToken cancellationToken)
    {
        using var scope = scopeFactory.CreateScope();
        var repository = scope.ServiceProvider.GetRequiredService<CareerVaultRepository>();
        var embeddingProvider = scope.ServiceProvider.GetRequiredService<IEmbeddingProvider>();

        var jobs = await repository.ClaimPendingEmbeddingJobsAsync(
            _workerOptions.BatchSize,
            TimeSpan.FromMinutes(_workerOptions.FailedRetryDelayMinutes),
            cancellationToken);

        if (jobs.Count == 0)
        {
            return;
        }

        logger.LogInformation("Worker encontrou {Count} entrada(s) com embedding pendente/stale/falho.", jobs.Count);

        foreach (var job in jobs)
        {
            var embeddingText = EmbeddingTextBuilder.Build(job);
            var recalculatedHash = EmbeddingTextBuilder.ComputeSha256(embeddingText);

            if (!string.Equals(recalculatedHash, job.ContentHash, StringComparison.Ordinal))
            {
                logger.LogInformation(
                    "Content hash divergente para entry {EntryId}. Reprocessando embedding com hash recalculado.",
                    job.Id);
            }

            try
            {
                logger.LogInformation("Gerando embedding para entry {EntryId}.", job.Id);
                var embedding = await embeddingProvider.GenerateAsync(embeddingText, cancellationToken);

                if (embedding.Length != _embeddingOptions.Dimensions)
                {
                    throw new InvalidOperationException(
                        $"Embedding retornou {embedding.Length} dimensoes, esperado: {_embeddingOptions.Dimensions}.");
                }

                await repository.SaveEmbeddingAsync(
                    job.Id,
                    _embeddingOptions.Model,
                    _embeddingOptions.Dimensions,
                    recalculatedHash,
                    embedding,
                    cancellationToken);

                logger.LogInformation("Embedding processado com sucesso para entry {EntryId}.", job.Id);
            }
            catch (Exception ex) when (ex is not OperationCanceledException)
            {
                logger.LogError(ex, "Erro ao gerar ou salvar embedding da entry {EntryId}.", job.Id);
                await repository.MarkEmbeddingFailedAsync(job.Id, ex.Message, cancellationToken);
            }
        }
    }
}
