namespace Api.BoundedContexts.DocumentProcessing.Application.DTOs;

/// <summary>
/// DTO for pipeline processing timing metrics of a PDF document.
/// RAG Sandbox: Provides per-step duration data for the pipeline visualization.
/// </summary>
internal record PdfPipelineMetricsDto(
    Guid PdfId,
    string FileName,
    string ProcessingState,
    int ProgressPercentage,
    DateTime UploadedAt,
    DateTime? ProcessedAt,
    TimeSpan? TotalDuration,
    IReadOnlyList<PipelineStepMetricDto> Steps
);

/// <summary>
/// Timing metric for a single pipeline processing step.
/// </summary>
internal record PipelineStepMetricDto(
    string StepName,
    int StepOrder,
    DateTime? StartedAt,
    DateTime? CompletedAt,
    TimeSpan? Duration,
    string Status
);
