using Api.BoundedContexts.DocumentProcessing.Application.DTOs;
using Api.SharedKernel.Application.Interfaces;

namespace Api.BoundedContexts.DocumentProcessing.Application.Queries.Queue;

/// <summary>
/// Fetches the most recent ProcessingJob (with Steps and LogEntries) for a given PdfDocumentId.
/// Returns null when no job exists for the document (e.g. legacy PDFs predating the pipeline).
/// Issue #1650: KB Ingestion log tab.
/// </summary>
internal sealed record GetLatestIngestionLogByDocumentIdQuery(Guid DocumentId)
    : IQuery<ProcessingJobDetailDto?>;
