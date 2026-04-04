using Api.BoundedContexts.DocumentProcessing.Application.DTOs;
using Api.BoundedContexts.DocumentProcessing.Application.Queries;
using Api.BoundedContexts.DocumentProcessing.Infrastructure.Services;
using Api.SharedKernel.Application.Interfaces;
using Microsoft.Extensions.Logging;

namespace Api.BoundedContexts.DocumentProcessing.Application.Queries;

internal sealed class ExtractBggGamesFromPdfQueryHandler : IQueryHandler<ExtractBggGamesFromPdfQuery, List<BggGameDto>>
{
    private readonly IBggGameExtractor _bggGameExtractor;
    private readonly ILogger<ExtractBggGamesFromPdfQueryHandler> _logger;

    public ExtractBggGamesFromPdfQueryHandler(
        IBggGameExtractor bggGameExtractor,
        ILogger<ExtractBggGamesFromPdfQueryHandler> logger)
    {
        _bggGameExtractor = bggGameExtractor;
        _logger = logger;
    }

    public async Task<List<BggGameDto>> Handle(
        ExtractBggGamesFromPdfQuery query,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(query);

        _logger.LogInformation(
            "Extracting BGG games from PDF: {PdfFilePath}",
            query.PdfFilePath);

        var games = await _bggGameExtractor.ExtractGamesAsync(query.PdfFilePath, cancellationToken).ConfigureAwait(false);

        _logger.LogInformation(
            "Successfully extracted {GameCount} games from PDF",
            games.Count);

        return games;
    }
}
