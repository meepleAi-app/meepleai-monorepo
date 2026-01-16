using Api.BoundedContexts.DocumentProcessing.Application.DTOs;

namespace Api.BoundedContexts.DocumentProcessing.Infrastructure.Services;

/// <summary>
/// Service for extracting BGG games from PDF documents
/// </summary>
internal interface IBggGameExtractor
{
    /// <summary>
    /// Extracts list of BGG games (Name, ID) from PDF
    /// </summary>
    /// <param name="pdfFilePath">Absolute path to PDF file</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>List of extracted games with validation</returns>
    Task<List<BggGameDto>> ExtractGamesAsync(
        string pdfFilePath,
        CancellationToken cancellationToken = default);
}
