using Api.BoundedContexts.SharedGameCatalog.Application.DTOs;
using Api.BoundedContexts.SharedGameCatalog.Domain.Repositories;
using Api.BoundedContexts.UserLibrary.Domain.Repositories;
using Api.Middleware.Exceptions;
using Api.SharedKernel.Application.Interfaces;

namespace Api.BoundedContexts.SharedGameCatalog.Application.Queries.GetGameDocumentsForUser;

/// <summary>
/// Handler for GetGameDocumentsForUserQuery.
/// Returns only active (IsActive = true) documents for games the user is authorised to access.
/// Access is granted when the game is RAG-public, or the user has the game in their library
/// (ownership declared or not).
/// </summary>
internal sealed class GetGameDocumentsForUserHandler
    : IQueryHandler<GetGameDocumentsForUserQuery, IReadOnlyList<SharedGameDocumentDto>>
{
    private readonly ISharedGameRepository _sharedGameRepository;
    private readonly ISharedGameDocumentRepository _documentRepository;
    private readonly IUserLibraryRepository _libraryRepository;
    private readonly ILogger<GetGameDocumentsForUserHandler> _logger;

    public GetGameDocumentsForUserHandler(
        ISharedGameRepository sharedGameRepository,
        ISharedGameDocumentRepository documentRepository,
        IUserLibraryRepository libraryRepository,
        ILogger<GetGameDocumentsForUserHandler> logger)
    {
        _sharedGameRepository = sharedGameRepository ?? throw new ArgumentNullException(nameof(sharedGameRepository));
        _documentRepository = documentRepository ?? throw new ArgumentNullException(nameof(documentRepository));
        _libraryRepository = libraryRepository ?? throw new ArgumentNullException(nameof(libraryRepository));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    public async Task<IReadOnlyList<SharedGameDocumentDto>> Handle(
        GetGameDocumentsForUserQuery query,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(query);

        // 1. Verify the game exists
        var game = await _sharedGameRepository.GetByIdAsync(query.GameId, cancellationToken)
            .ConfigureAwait(false);

        if (game is null)
        {
            throw new NotFoundException("SharedGame", query.GameId.ToString());
        }

        // 2. Check access:
        //    - IsRagPublic: any authenticated user may access
        //    - Otherwise: user must have the game in their library
        bool hasAccess = game.IsRagPublic;

        if (!hasAccess)
        {
            var libraryEntry = await _libraryRepository
                .GetByUserAndGameAsync(query.UserId, query.GameId, cancellationToken)
                .ConfigureAwait(false);

            hasAccess = libraryEntry is not null;
        }

        if (!hasAccess)
        {
            _logger.LogWarning(
                "User {UserId} attempted to access documents for game {GameId} without permission",
                query.UserId, query.GameId);

            return Array.Empty<SharedGameDocumentDto>();
        }

        // 3. Return only active documents
        var documents = await _documentRepository
            .GetActiveDocumentsAsync(query.GameId, cancellationToken)
            .ConfigureAwait(false);

        _logger.LogInformation(
            "Returning {Count} active documents for game {GameId} to user {UserId}",
            documents.Count, query.GameId, query.UserId);

        return documents.Select(d => new SharedGameDocumentDto(
            d.Id,
            d.SharedGameId,
            d.PdfDocumentId,
            d.DocumentType,
            d.Version,
            d.IsActive,
            d.Tags,
            d.CreatedAt,
            d.CreatedBy)).ToList();
    }
}
