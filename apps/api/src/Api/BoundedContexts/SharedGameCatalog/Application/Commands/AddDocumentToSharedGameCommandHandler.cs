using Api.BoundedContexts.SharedGameCatalog.Domain.Entities;
using Api.BoundedContexts.SharedGameCatalog.Domain.Events;
using Api.BoundedContexts.SharedGameCatalog.Domain.Repositories;
using Api.BoundedContexts.SharedGameCatalog.Domain.Services;
using Api.Infrastructure;
using Api.SharedKernel.Application.Interfaces;
using Api.SharedKernel.Infrastructure.Persistence;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace Api.BoundedContexts.SharedGameCatalog.Application.Commands;

/// <summary>
/// Handler for adding a document to a shared game.
/// </summary>
internal sealed class AddDocumentToSharedGameCommandHandler : ICommandHandler<AddDocumentToSharedGameCommand, Guid>
{
    private readonly ISharedGameRepository _gameRepository;
    private readonly ISharedGameDocumentRepository _documentRepository;
    private readonly DocumentVersioningService _versioningService;
    private readonly MeepleAiDbContext _context;
    private readonly IUnitOfWork _unitOfWork;
    private readonly IMediator _mediator;
    private readonly ILogger<AddDocumentToSharedGameCommandHandler> _logger;

    public AddDocumentToSharedGameCommandHandler(
        ISharedGameRepository gameRepository,
        ISharedGameDocumentRepository documentRepository,
        DocumentVersioningService versioningService,
        MeepleAiDbContext context,
        IUnitOfWork unitOfWork,
        IMediator mediator,
        ILogger<AddDocumentToSharedGameCommandHandler> logger)
    {
        _gameRepository = gameRepository ?? throw new ArgumentNullException(nameof(gameRepository));
        _documentRepository = documentRepository ?? throw new ArgumentNullException(nameof(documentRepository));
        _versioningService = versioningService ?? throw new ArgumentNullException(nameof(versioningService));
        _context = context ?? throw new ArgumentNullException(nameof(context));
        _unitOfWork = unitOfWork ?? throw new ArgumentNullException(nameof(unitOfWork));
        _mediator = mediator ?? throw new ArgumentNullException(nameof(mediator));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    public async Task<Guid> Handle(AddDocumentToSharedGameCommand command, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(command);

        _logger.LogInformation(
            "Adding document to shared game: {SharedGameId}, Type: {DocumentType}, Version: {Version}",
            command.SharedGameId, command.DocumentType, command.Version);

        // Verify shared game exists
        var game = await _gameRepository.GetByIdAsync(command.SharedGameId, cancellationToken).ConfigureAwait(false);
        if (game is null)
        {
            throw new InvalidOperationException($"Shared game with ID {command.SharedGameId} not found");
        }

        // Verify PDF document exists
        var pdfExists = await _context.PdfDocuments
            .AnyAsync(p => p.Id == command.PdfDocumentId, cancellationToken)
            .ConfigureAwait(false);

        if (!pdfExists)
        {
            throw new InvalidOperationException($"PDF document with ID {command.PdfDocumentId} not found");
        }

        // Validate version doesn't already exist
        await _versioningService.ValidateVersionDoesNotExistAsync(
            command.SharedGameId,
            command.DocumentType,
            command.Version,
            cancellationToken).ConfigureAwait(false);

        // Create the document with authenticated user context
        var document = SharedGameDocument.Create(
            command.SharedGameId,
            command.PdfDocumentId,
            command.DocumentType,
            command.Version,
            command.CreatedBy,
            command.Tags);

        // Set as active if requested
        if (command.SetAsActive)
        {
            await _versioningService.SetActiveVersionAsync(document, cancellationToken).ConfigureAwait(false);
        }

        // Add to repository
        await _documentRepository.AddAsync(document, cancellationToken).ConfigureAwait(false);
        await _unitOfWork.SaveChangesAsync(cancellationToken).ConfigureAwait(false);

        // Publish domain event
        await _mediator.Publish(
            new SharedGameDocumentAddedEvent(
                command.SharedGameId,
                document.Id,
                command.PdfDocumentId,
                command.DocumentType,
                command.Version,
                command.CreatedBy),
            cancellationToken).ConfigureAwait(false);

        _logger.LogInformation(
            "Document added successfully: {DocumentId} to game {SharedGameId}",
            document.Id, command.SharedGameId);

        return document.Id;
    }
}
