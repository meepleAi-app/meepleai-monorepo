using System.Text.Json;
using Api.BoundedContexts.SharedGameCatalog.Domain.Entities;
using Api.Infrastructure;
using Api.SharedKernel.Application.Interfaces;
using Microsoft.EntityFrameworkCore;

namespace Api.BoundedContexts.SharedGameCatalog.Application.Queries.GetSharedGameDocuments;

/// <summary>
/// Handler for GetSharedGameDocumentsQuery.
/// Joins SharedGameDocuments with PdfDocuments to provide full document details.
/// Issue #119: Per-SharedGame Document Overview.
/// </summary>
internal sealed class GetSharedGameDocumentsQueryHandler
    : IQueryHandler<GetSharedGameDocumentsQuery, GetSharedGameDocumentsResult>
{
    private readonly MeepleAiDbContext _dbContext;
    private readonly ILogger<GetSharedGameDocumentsQueryHandler> _logger;

    public GetSharedGameDocumentsQueryHandler(
        MeepleAiDbContext dbContext,
        ILogger<GetSharedGameDocumentsQueryHandler> logger)
    {
        _dbContext = dbContext ?? throw new ArgumentNullException(nameof(dbContext));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    public async Task<GetSharedGameDocumentsResult> Handle(
        GetSharedGameDocumentsQuery query,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(query);

        _logger.LogInformation(
            "Getting all documents for shared game {SharedGameId}",
            query.SharedGameId);

        // Query raw data from DB (two tables joined)
        var rawDocuments = await _dbContext.SharedGameDocuments
            .Where(d => d.SharedGameId == query.SharedGameId)
            .Join(
                _dbContext.PdfDocuments,
                doc => doc.PdfDocumentId,
                pdf => pdf.Id,
                (doc, pdf) => new
                {
                    doc.Id,
                    doc.SharedGameId,
                    doc.PdfDocumentId,
                    doc.DocumentType,
                    doc.Version,
                    doc.IsActive,
                    doc.TagsJson,
                    doc.CreatedAt,
                    doc.CreatedBy,
                    doc.ApprovalStatus,
                    pdf.FileName,
                    pdf.ProcessingState,
                    pdf.FileSizeBytes,
                    pdf.UploadedAt
                })
            .OrderByDescending(d => d.CreatedAt)
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);

        // Project to DTOs in-memory (ParseTags can't be translated by EF)
        var documents = rawDocuments.Select(d => new SharedGameDocumentDetailDto(
            d.Id,
            d.SharedGameId,
            d.PdfDocumentId,
            ((SharedGameDocumentType)d.DocumentType).ToString(),
            d.Version,
            d.IsActive,
            ParseTags(d.TagsJson),
            d.CreatedAt,
            d.CreatedBy,
            ((DocumentApprovalStatus)d.ApprovalStatus).ToString(),
            d.FileName,
            d.ProcessingState,
            d.FileSizeBytes,
            d.UploadedAt
        )).ToList();

        return new GetSharedGameDocumentsResult(
            query.SharedGameId,
            documents,
            documents.Count);
    }

    private static IReadOnlyList<string> ParseTags(string? tagsJson)
    {
        if (string.IsNullOrWhiteSpace(tagsJson))
            return Array.Empty<string>();

        try
        {
            return JsonSerializer.Deserialize<List<string>>(tagsJson) ?? (IReadOnlyList<string>)Array.Empty<string>();
        }
        catch
        {
            return Array.Empty<string>();
        }
    }
}
