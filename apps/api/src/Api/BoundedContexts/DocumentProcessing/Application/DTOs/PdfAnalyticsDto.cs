namespace Api.BoundedContexts.DocumentProcessing.Application.DTOs;

/// <summary>
/// DTO for PDF analytics response.
/// Issue #3715.2: System-wide PDF processing analytics.
/// </summary>
public record PdfAnalyticsDto
{
    public required int TotalUploaded { get; init; }
    public required int SuccessCount { get; init; }
    public required int FailedCount { get; init; }
    public required decimal SuccessRate { get; init; }  // Percentage
    public required TimeSpan? AvgProcessingTime { get; init; }
    public required TimeSpan? P95ProcessingTime { get; init; }
    public required long TotalStorageBytes { get; init; }
    public required Dictionary<string, long> StorageByTier { get; init; }
    public required List<DailyUploadStats> UploadsByDay { get; init; }
}

public record DailyUploadStats
{
    public required DateOnly Date { get; init; }
    public required int TotalCount { get; init; }
    public required int SuccessCount { get; init; }
    public required int FailedCount { get; init; }
}
