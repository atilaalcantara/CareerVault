namespace CareerVault.Api.Options;

public sealed class EmbeddingWorkerOptions
{
    public const string SectionName = "EmbeddingWorker";

    public bool Enabled { get; init; } = true;
    public int IntervalSeconds { get; init; } = 30;
    public int BatchSize { get; init; } = 5;
    public int MaxDegreeOfParallelism { get; init; } = 1;
    public int FailedRetryDelayMinutes { get; init; } = 5;
}
