using System.Text.Json;
using Opiniones.ExtractionWorker.Configuration;
using Opiniones.ExtractionWorker.Models;
using Opiniones.ExtractionWorker.Staging;

namespace Opiniones.ExtractionWorker;

public sealed class ExtractionWorker(
    IEnumerable<Extraction.IExtractor> extractors,
    IStagingWriter stagingWriter,
    Microsoft.Extensions.Options.IOptions<Configuration.ExtractionOptions> options,
    ILogger<ExtractionWorker> logger) : BackgroundService
{
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        var intervalMinutes = Math.Max(1, options.Value.IntervalMinutes);
        logger.LogInformation("Worker iniciado; intervalo entre extracciones: {IntervalMinutes} minutos.", intervalMinutes);

        while (!stoppingToken.IsCancellationRequested)
        {
            var runId = DateTimeOffset.UtcNow.ToString("yyyyMMddTHHmmssZ");
            var enabled = extractors.Where(x => x.IsEnabled).ToArray();
            logger.LogInformation("Inicia extracción {RunId}; fuentes habilitadas: {Sources}",
                runId, string.Join(", ", enabled.Select(x => x.SourceName)));
            if (enabled.Length == 0)
            {
                logger.LogWarning("No hay fuentes de extracción habilitadas.");
            }
            else
            {
                var results = await Task.WhenAll(enabled.Select(x => ExtractOneAsync(x, runId, stoppingToken)));
                var failures = results.Count(x => !x.Success);
                logger.LogInformation("Termina extracción {RunId}: {Count} fuentes correctas, {Failures} con error",
                    runId, results.Length - failures, failures);
                if (failures > 0) Environment.ExitCode = 1;
            }

            await Task.Delay(TimeSpan.FromMinutes(intervalMinutes), stoppingToken);
        }
    }

    private async Task<SourceResult> ExtractOneAsync(Extraction.IExtractor extractor, string runId,
        CancellationToken cancellationToken)
    {
        var timer = System.Diagnostics.Stopwatch.StartNew();
        try
        {
            var count = await stagingWriter.WriteAsync(extractor, options.Value.StagingDirectory, runId, cancellationToken);
            timer.Stop();
            logger.LogInformation("Fuente {Source}: {Count} filas en staging en {ElapsedMs} ms",
                extractor.SourceName, count, timer.ElapsedMilliseconds);
            return new SourceResult(true);
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            throw;
        }
        catch (Exception ex)
        {
            timer.Stop();
            logger.LogError(ex, "Falló la extracción de {Source} después de {ElapsedMs} ms",
                extractor.SourceName, timer.ElapsedMilliseconds);
            return new SourceResult(false);
        }
    }

    private sealed record SourceResult(bool Success);
}
