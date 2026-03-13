using Api.BoundedContexts.DocumentProcessing.Application.DTOs;
using Api.SharedKernel.Application.Interfaces;

namespace Api.BoundedContexts.DocumentProcessing.Application.Queries;

/// <summary>
/// Query to retrieve pipeline processing timing metrics for a PDF document.
/// RAG Sandbox: Pipeline Metrics panel.
/// </summary>
internal record GetPdfPipelineMetricsQuery(
    Guid PdfId
) : IQuery<PdfPipelineMetricsDto?>;
