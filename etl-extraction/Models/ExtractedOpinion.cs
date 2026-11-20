namespace Opiniones.ExtractionWorker.Models;

public sealed record ExtractedOpinion
{
    public required string Source { get; init; }
    public required string SourceRecordId { get; init; }
    public string? Channel { get; init; }
    public string? CustomerId { get; init; }
    public string? ProductId { get; init; }
    public string? DataSourceId { get; init; }
    public DateTimeOffset? OpinionDate { get; init; }
    public string? Comment { get; init; }
    public string? Sentiment { get; init; }
    public decimal? Score { get; init; }
    public int? Engagement { get; init; }
    public DateTimeOffset ExtractedAtUtc { get; init; } = DateTimeOffset.UtcNow;
}
