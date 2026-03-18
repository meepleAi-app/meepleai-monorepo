using Api.BoundedContexts.DocumentProcessing.Application.DTOs;
using Api.BoundedContexts.DocumentProcessing.Application.Queries;
using Api.BoundedContexts.DocumentProcessing.Domain.Entities;
using Api.BoundedContexts.DocumentProcessing.Domain.Enums;
using Api.BoundedContexts.DocumentProcessing.Domain.Repositories;
using Api.SharedKernel.Application.Interfaces;

namespace Api.BoundedContexts.DocumentProcessing.Application.Handlers;

/// <summary>
/// Handles retrieving pipeline processing timing metrics for a PDF document.
/// RAG Sandbox: Pipeline Metrics panel.
/// </summary>
internal class GetPdfPipelineMetricsQueryHandler
    : IQueryHandler<GetPdfPipelineMetricsQuery, PdfPipelineMetricsDto?>
{
    private readonly IPdfDocumentRepository _documentRepository;

    public GetPdfPipelineMetricsQueryHandler(IPdfDocumentRepository documentRepository)
    {
        _documentRepository = documentRepository ?? throw new ArgumentNullException(nameof(documentRepository));
    }

    public async Task<PdfPipelineMetricsDto?> Handle(
        GetPdfPipelineMetricsQuery query,
        CancellationToken cancellationToken)
    {
        var doc = await _documentRepository.GetByIdAsync(query.PdfId, cancellationToken).ConfigureAwait(false);
        if (doc is null)
        {
            return null;
        }

        var steps = BuildStepMetrics(doc);

        return new PdfPipelineMetricsDto(
            PdfId: doc.Id,
            FileName: doc.FileName.Value,
            ProcessingState: doc.ProcessingState.ToString(),
            ProgressPercentage: doc.ProgressPercentage,
            UploadedAt: doc.UploadedAt,
            ProcessedAt: doc.ProcessedAt,
            TotalDuration: doc.TotalDuration,
            Steps: steps
        );
    }

    private static IReadOnlyList<PipelineStepMetricDto> BuildStepMetrics(PdfDocument doc)
    {
        // Each step's start time is known; its "completed" time is the start of the next step.
        var stepDefinitions = new (string Name, int Order, DateTime? StartedAt, PdfProcessingState State)[]
        {
            ("Uploading",   1, doc.UploadingStartedAt,  PdfProcessingState.Uploading),
            ("Extracting",  2, doc.ExtractingStartedAt,  PdfProcessingState.Extracting),
            ("Chunking",    3, doc.ChunkingStartedAt,    PdfProcessingState.Chunking),
            ("Embedding",   4, doc.EmbeddingStartedAt,   PdfProcessingState.Embedding),
            ("Indexing",    5, doc.IndexingStartedAt,     PdfProcessingState.Indexing),
        };

        var steps = new List<PipelineStepMetricDto>(stepDefinitions.Length);

        for (var i = 0; i < stepDefinitions.Length; i++)
        {
            var (name, order, startedAt, state) = stepDefinitions[i];

            // Determine the end time: start of the next step, or ProcessedAt for the last step
            DateTime? completedAt = null;
            if (startedAt.HasValue)
            {
                if (i + 1 < stepDefinitions.Length && stepDefinitions[i + 1].StartedAt.HasValue)
                {
                    completedAt = stepDefinitions[i + 1].StartedAt;
                }
                else if (doc.ProcessingState == PdfProcessingState.Ready && i == stepDefinitions.Length - 1)
                {
                    completedAt = doc.ProcessedAt;
                }
            }

            var duration = startedAt.HasValue && completedAt.HasValue
                ? completedAt.Value - startedAt.Value
                : (TimeSpan?)null;

            var status = DetermineStepStatus(doc.ProcessingState, state, startedAt);

            steps.Add(new PipelineStepMetricDto(
                StepName: name,
                StepOrder: order,
                StartedAt: startedAt,
                CompletedAt: completedAt,
                Duration: duration,
                Status: status
            ));
        }

        return steps;
    }

    private static string DetermineStepStatus(
        PdfProcessingState currentState,
        PdfProcessingState stepState,
        DateTime? startedAt)
    {
        if (!startedAt.HasValue)
        {
            // Step has not started yet
            return currentState == PdfProcessingState.Failed ? "Skipped" : "Pending";
        }

        if (currentState == stepState)
        {
            // Document is currently in this step
            return currentState == PdfProcessingState.Failed ? "Failed" : "InProgress";
        }

        if ((int)currentState > (int)stepState || currentState == PdfProcessingState.Ready)
        {
            return "Completed";
        }

        // If the document failed and this step had started but a later step is the current state,
        // it means this step completed before failure
        if (currentState == PdfProcessingState.Failed)
        {
            return "Completed";
        }

        return "Pending";
    }
}
