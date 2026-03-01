using Api.BoundedContexts.DocumentProcessing.Domain.Repositories;
using Api.BoundedContexts.SharedGameCatalog.Domain.Repositories;
using Api.BoundedContexts.UserLibrary.Application.DTOs;
using Api.BoundedContexts.UserLibrary.Application.Queries;
using Api.BoundedContexts.UserLibrary.Domain.Repositories;
using Api.SharedKernel.Application.Interfaces;
using Api.Middleware.Exceptions;

namespace Api.BoundedContexts.UserLibrary.Application.Handlers;

/// <summary>
/// Handler for GetGameWizardPreviewQuery.
/// Fetches combined game preview data from SharedGameCatalog, DocumentProcessing, and UserLibrary contexts.
/// Issue #4823: Backend Game Preview API - Unified Wizard Data Endpoint
/// Epic #4817: User Collection Wizard
/// </summary>
internal class GetGameWizardPreviewQueryHandler : IQueryHandler<GetGameWizardPreviewQuery, GameWizardPreviewDto>
{
    private readonly ISharedGameRepository _sharedGameRepository;
    private readonly IPdfDocumentRepository _pdfDocumentRepository;
    private readonly IUserLibraryRepository _userLibraryRepository;

    public GetGameWizardPreviewQueryHandler(
        ISharedGameRepository sharedGameRepository,
        IPdfDocumentRepository pdfDocumentRepository,
        IUserLibraryRepository userLibraryRepository)
    {
        _sharedGameRepository = sharedGameRepository ?? throw new ArgumentNullException(nameof(sharedGameRepository));
        _pdfDocumentRepository = pdfDocumentRepository ?? throw new ArgumentNullException(nameof(pdfDocumentRepository));
        _userLibraryRepository = userLibraryRepository ?? throw new ArgumentNullException(nameof(userLibraryRepository));
    }

    public async Task<GameWizardPreviewDto> Handle(
        GetGameWizardPreviewQuery query,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(query);

        // Fetch game from SharedGameCatalog
        var game = await _sharedGameRepository
            .GetByIdAsync(query.GameId, cancellationToken)
            .ConfigureAwait(false);

        if (game is null)
        {
            throw new NotFoundException($"Game with ID {query.GameId} not found in catalog");
        }

        // Fetch PDF documents and library status in parallel
        var documentsTask = _pdfDocumentRepository
            .FindByGameIdAsync(query.GameId, cancellationToken);
        var libraryEntryTask = _userLibraryRepository
            .GetByUserAndGameAsync(query.UserId, query.GameId, cancellationToken);

        await Task.WhenAll(documentsTask, libraryEntryTask).ConfigureAwait(false);

        var documents = await documentsTask.ConfigureAwait(false);
        var libraryEntry = await libraryEntryTask.ConfigureAwait(false);

        var documentSummaries = documents
            .Select(d => new PdfDocumentSummaryDto(
                Id: d.Id,
                FileName: d.FileName.Value,
                PageCount: d.PageCount,
                Status: d.ProcessingStatus,
                DocumentType: d.DocumentType?.Value ?? "base"
            ))
            .ToList();

        var categories = game.Categories
            .Select(c => c.Name)
            .ToList();

        var mechanics = game.Mechanics
            .Select(m => m.Name)
            .ToList();

        return new GameWizardPreviewDto(
            GameId: game.Id,
            Title: game.Title,
            ImageUrl: game.ImageUrl,
            ThumbnailUrl: game.ThumbnailUrl,
            MinPlayers: game.MinPlayers,
            MaxPlayers: game.MaxPlayers,
            PlayingTimeMinutes: game.PlayingTimeMinutes,
            ComplexityRating: game.ComplexityRating,
            AverageRating: game.AverageRating,
            YearPublished: game.YearPublished,
            Description: game.Description,
            Source: query.Source,
            Documents: documentSummaries,
            DocumentCount: documentSummaries.Count,
            IsInUserLibrary: libraryEntry is not null,
            LibraryEntryId: libraryEntry?.Id,
            Categories: categories,
            Mechanics: mechanics
        );
    }
}
