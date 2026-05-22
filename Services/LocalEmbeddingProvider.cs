using ElBruno.LocalEmbeddings;
using ElBruno.LocalEmbeddings.Options;
using Microsoft.Extensions.Options;

namespace CareerVault.Api.Services;

public sealed class LocalEmbeddingProvider : IEmbeddingProvider, IAsyncDisposable
{
    private readonly CareerVault.Api.Options.LocalEmbeddingsOptions _options;
    private readonly ILogger<LocalEmbeddingProvider> _logger;
    private readonly SemaphoreSlim _gate = new(1, 1);
    private LocalEmbeddingGenerator? _generator;

    public LocalEmbeddingProvider(
        IOptions<CareerVault.Api.Options.LocalEmbeddingsOptions> options,
        ILogger<LocalEmbeddingProvider> logger)
    {
        _options = options.Value;
        _logger = logger;
    }

    public async Task<float[]> GenerateAsync(string text, CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(text))
        {
            throw new InvalidOperationException("Nao e possivel gerar embedding para texto vazio.");
        }

        var generator = await GetGeneratorAsync(cancellationToken);
        var embedding = await generator.GenerateEmbeddingAsync(text, cancellationToken: cancellationToken);
        return embedding.Vector.ToArray();
    }

    public async ValueTask DisposeAsync()
    {
        _gate.Dispose();

        if (_generator is not null)
        {
            await _generator.DisposeAsync();
        }
    }

    private async Task<LocalEmbeddingGenerator> GetGeneratorAsync(CancellationToken cancellationToken)
    {
        if (_generator is not null)
        {
            return _generator;
        }

        await _gate.WaitAsync(cancellationToken);
        try
        {
            if (_generator is not null)
            {
                return _generator;
            }

            ConfigureCacheEnvironment();

            var generatorOptions = new ElBruno.LocalEmbeddings.Options.LocalEmbeddingsOptions
            {
                ModelName = _options.Model,
                CacheDirectory = _options.CacheDirectory
            };

            _generator = await LocalEmbeddingGenerator.CreateAsync(generatorOptions, cancellationToken);
            _logger.LogInformation(
                "Provider local de embeddings inicializado. Modelo configurado: {Model}; cache: {CacheDirectory}",
                _options.Model,
                _options.CacheDirectory ?? "padrao-do-runtime");

            return _generator;
        }
        finally
        {
            _gate.Release();
        }
    }

    private void ConfigureCacheEnvironment()
    {
        if (string.IsNullOrWhiteSpace(_options.CacheDirectory))
        {
            return;
        }

        Directory.CreateDirectory(_options.CacheDirectory);
        Environment.SetEnvironmentVariable("XDG_CACHE_HOME", _options.CacheDirectory);
        Environment.SetEnvironmentVariable("HF_HOME", _options.CacheDirectory);
    }
}
