using System.Runtime.CompilerServices;
using System.Net.Http.Headers;
using System.Text.Json;
using Microsoft.Extensions.Options;
using Opiniones.ExtractionWorker.Configuration;
using Opiniones.ExtractionWorker.Models;

namespace Opiniones.ExtractionWorker.Extraction;

public sealed class ApiExtractor(HttpClient httpClient, IOptions<ApiSourceOptions> options, ILogger<ApiExtractor> logger) : ExtractorBase
{
    private static readonly JsonSerializerOptions JsonOptions = new() { PropertyNameCaseInsensitive = true };
    private readonly ApiSourceOptions _options = options.Value;
    public override string SourceName => "RestApi";
    public override bool IsEnabled => _options.Enabled;

    public override async IAsyncEnumerable<ExtractedOpinion> ExtractAsync([EnumeratorCancellation] CancellationToken cancellationToken)
    {
        if (!Uri.TryCreate(_options.Endpoint, UriKind.Absolute, out var endpoint))
            throw new InvalidOperationException("Configure Sources:Api:Endpoint con la URL HTTPS del servicio de comentarios.");
        if (endpoint.Scheme != Uri.UriSchemeHttps && !endpoint.IsLoopback)
            throw new InvalidOperationException("La API debe usar HTTPS; solo localhost puede usar HTTP durante desarrollo.");
        using var request = new HttpRequestMessage(HttpMethod.Get, endpoint);
        var bearer = _options.BearerToken;
        if (string.IsNullOrWhiteSpace(bearer)) bearer = Environment.GetEnvironmentVariable("Sources__Api__BearerToken");
        if (!string.IsNullOrWhiteSpace(bearer)) request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", bearer);
        using var response = await httpClient.SendAsync(request, HttpCompletionOption.ResponseHeadersRead, cancellationToken);
        response.EnsureSuccessStatusCode();
        await using var body = await response.Content.ReadAsStreamAsync(cancellationToken);
        long count = 0;
        await foreach (var comment in JsonSerializer.DeserializeAsyncEnumerable<ApiComment>(body, JsonOptions, cancellationToken))
        {
            cancellationToken.ThrowIfCancellationRequested();
            if (comment is null || string.IsNullOrWhiteSpace(comment.IdComment))
                throw new InvalidDataException("Cada comentario de la API debe incluir idComment.");
            yield return new ExtractedOpinion
            {
                Source = SourceName,
                SourceRecordId = comment.IdComment,
                Channel = comment.Channel ?? comment.Source,
                CustomerId = comment.IdClient,
                ProductId = comment.IdProduct,
                OpinionDate = DateTimeOffset.TryParse(comment.Date, out var date) ? date : null,
                Comment = comment.Comment,
                Sentiment = comment.Sentiment,
                Engagement = comment.Engagement
            };
            count++;
        }
        logger.LogInformation("La API devolvió {Count} comentarios.", count);
    }

    private sealed class ApiComment
    {
        public string? IdComment { get; init; }
        public string? IdClient { get; init; }
        public string? IdProduct { get; init; }
        public string? Channel { get; init; }
        public string? Source { get; init; }
        public string? Date { get; init; }
        public string? Comment { get; init; }
        public string? Sentiment { get; init; }
        public int? Engagement { get; init; }
    }
}

