using Api.BoundedContexts.UserLibrary.Application.DTOs;
using Api.BoundedContexts.UserLibrary.Application.Queries;
using Api.BoundedContexts.UserLibrary.Domain.Repositories;
using Api.SharedKernel.Domain.Exceptions;
using MediatR;
using Microsoft.Extensions.Logging;

namespace Api.BoundedContexts.UserLibrary.Application.Handlers;

/// <summary>
/// Handler for GetGamePdfsQuery.
/// Retrieves all PDFs associated with a game (user custom + shared catalog).
/// Issue #3152: Game Detail Split View - PDF selector support
/// </summary>
internal class GetGamePdfsQueryHandler : IRequestHandler<GetGamePdfsQuery, List<GamePdfDto>>
{
    private readonly IUserLibraryRepository _libraryRepository;
    private readonly ILogger<GetGamePdfsQueryHandler> _logger;

    public GetGamePdfsQueryHandler(
        IUserLibraryRepository libraryRepository,
        ILogger<GetGamePdfsQueryHandler> logger)
    {
        _libraryRepository = libraryRepository ?? throw new ArgumentNullException(nameof(libraryRepository));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    public async Task<List<GamePdfDto>> Handle(
        GetGamePdfsQuery request,
        CancellationToken cancellationToken)
    {
        var pdfs = new List<GamePdfDto>();

        // Get user's library entry for this game
        var libraryEntry = await _libraryRepository
            .GetByUserAndGameAsync(request.UserId, request.GameId, cancellationToken)
            .ConfigureAwait(false);

        // If user doesn't have this game in library, return empty list
        if (libraryEntry == null)
        {
            _logger.LogWarning(
                "User {UserId} requested PDFs for game {GameId} but doesn't own it",
                request.UserId, request.GameId);
            return pdfs;
        }

        // Add custom user-uploaded PDF if exists
        if (libraryEntry.HasCustomPdf() && libraryEntry.CustomPdfMetadata != null)
        {
            var customPdf = new GamePdfDto(
                Id: libraryEntry.CustomPdfMetadata.Url,
                Name: libraryEntry.CustomPdfMetadata.OriginalFileName.Replace(".pdf", "", StringComparison.OrdinalIgnoreCase),
                PageCount: 0, // FUTURE: Extract page count from PDF metadata (Issue #3152)
                FileSizeBytes: libraryEntry.CustomPdfMetadata.FileSizeBytes,
                UploadedAt: libraryEntry.CustomPdfMetadata.UploadedAt,
                Source: "Custom",
                Language: DetectLanguageFromFilename(libraryEntry.CustomPdfMetadata.OriginalFileName)
            );

            pdfs.Add(customPdf);
        }

        // FUTURE: Issue #3152 - Add shared catalog PDFs from DocumentProcessing context
        // Query DocumentProcessing bounded context for standard PDFs
        // var catalogPdfs = await _documentRepository.GetByGameIdAsync(request.GameId, cancellationToken);
        // pdfs.AddRange(catalogPdfs.Select(ToCatalogPdfDto));

        // TEMPORARY: Add mock standard PDF for development
        if (pdfs.Count == 0)
        {
            // Fallback: provide standard rulebook placeholder
            pdfs.Add(new GamePdfDto(
                Id: $"/pdfs/standard-rules-{request.GameId}.pdf",
                Name: "Regolamento Standard",
                PageCount: 24,
                FileSizeBytes: 5_000_000,
                UploadedAt: DateTime.UtcNow.AddDays(-30),
                Source: "Catalog",
                Language: "IT"
            ));
        }

        _logger.LogInformation(
            "Retrieved {Count} PDFs for game {GameId}, user {UserId}",
            pdfs.Count, request.GameId, request.UserId);

        return pdfs;
    }

    /// <summary>
    /// Detects language from filename (basic heuristic).
    /// FUTURE: Use more sophisticated detection (Issue #3152)
    /// </summary>
    private static string? DetectLanguageFromFilename(string filename)
    {
        var lower = filename.ToLowerInvariant();
        if (lower.Contains("-it") || lower.Contains("_it") || lower.Contains("(it)") || lower.Contains("italiano"))
            return "IT";
        if (lower.Contains("-en") || lower.Contains("_en") || lower.Contains("(en)") || lower.Contains("english"))
            return "EN";
        if (lower.Contains("-es") || lower.Contains("_es") || lower.Contains("(es)") || lower.Contains("espanol"))
            return "ES";
        if (lower.Contains("-fr") || lower.Contains("_fr") || lower.Contains("(fr)") || lower.Contains("francais"))
            return "FR";
        if (lower.Contains("-de") || lower.Contains("_de") || lower.Contains("(de)") || lower.Contains("deutsch"))
            return "DE";

        return null; // Unknown language
    }
}
