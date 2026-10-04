using Api.BoundedContexts.DocumentProcessing.Domain.Repositories;
using Api.BoundedContexts.SharedGameCatalog.Domain.Repositories;
using Api.BoundedContexts.UserLibrary.Application.DTOs;
using Api.BoundedContexts.UserLibrary.Application.Queries;
using Api.BoundedContexts.UserLibrary.Domain.Repositories;
using Api.SharedKernel.Application.Interfaces;
using Api.Middleware.Exceptions;

namespace Api.BoundedContexts.UserLibrary.Application.Queries;

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

        // 🔴 Sequenziale, non `Task.WhenAll`. Entrambi i repository derivano da `RepositoryBase`,
        // che tiene il `MeepleAiDbContext` **scoped** della richiesta
        // (SharedKernel/Infrastructure/RepositoryBase.cs), e un DbContext non è thread-safe: due
        // query avviate insieme su quella stessa istanza fanno lanciare `ConcurrencyDetector` con
        // «A second operation was started on this context instance before a previous operation
        // completed», e la rotta risponde 500.
        //
        // Non è teoria: `GET /api/v1/wizard/game-preview/{gameId}` dava 500 su 6 richieste su 6
        // (tre giochi reali, due tentativi ciascuno), con `ConcurrencyDetector.EnterCriticalSection`
        // in testa allo stack. È la stessa causa di #4059 su `/admin/kb/pipeline/health`, trovata
        // su questa rotta da un esame adversarial di quella correzione — che aveva toccato solo
        // l'endpoint esploso.
        //
        // Il parallelismo qui non ha nulla da guadagnare: sono due letture brevi sulla stessa
        // connessione, che il pool serializza comunque. Se un giorno servisse davvero, la strada è
        // un contesto per operazione via `IDbContextFactory`, non `WhenAll` su quello condiviso.
        var documents = await _pdfDocumentRepository
            .FindByGameIdAsync(query.GameId, cancellationToken)
            .ConfigureAwait(false);
        var libraryEntry = await _userLibraryRepository
            .GetByUserAndGameAsync(query.UserId, query.GameId, cancellationToken)
            .ConfigureAwait(false);

        var documentSummaries = documents
            .Select(d => new PdfDocumentSummaryDto(
                Id: d.Id,
                FileName: d.FileName.Value,
                PageCount: d.PageCount,
                Status: d.ProcessingState.ToString(),
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
