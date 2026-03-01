using Api.BoundedContexts.SharedGameCatalog.Domain.Repositories;
using Api.BoundedContexts.UserLibrary.Application.Commands;
using Api.BoundedContexts.UserLibrary.Application.DTOs;
using Api.BoundedContexts.UserLibrary.Domain.Repositories;
using Api.Middleware.Exceptions;
using Api.SharedKernel.Application.Interfaces;
using Api.SharedKernel.Infrastructure.Persistence;

namespace Api.BoundedContexts.UserLibrary.Application.Handlers;

/// <summary>
/// Handler for removing a private PDF from a library entry.
/// Issue #3651: Triggers PrivatePdfRemovedEvent for vector cleanup.
/// </summary>
internal class RemovePrivatePdfCommandHandler : ICommandHandler<RemovePrivatePdfCommand, UserLibraryEntryDto>
{
    private readonly IUserLibraryRepository _libraryRepository;
    private readonly ISharedGameRepository _sharedGameRepository;
    private readonly IUnitOfWork _unitOfWork;

    public RemovePrivatePdfCommandHandler(
        IUserLibraryRepository libraryRepository,
        ISharedGameRepository sharedGameRepository,
        IUnitOfWork unitOfWork)
    {
        _libraryRepository = libraryRepository ?? throw new ArgumentNullException(nameof(libraryRepository));
        _sharedGameRepository = sharedGameRepository ?? throw new ArgumentNullException(nameof(sharedGameRepository));
        _unitOfWork = unitOfWork ?? throw new ArgumentNullException(nameof(unitOfWork));
    }

    public async Task<UserLibraryEntryDto> Handle(RemovePrivatePdfCommand command, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(command);

        var entry = await _libraryRepository.GetByIdAsync(command.EntryId, cancellationToken)
            .ConfigureAwait(false)
            ?? throw new NotFoundException($"Library entry {command.EntryId} not found");

        // Authorization check: ensure user owns the entry
        if (entry.UserId != command.UserId)
        {
            throw new ForbiddenException("You do not have permission to modify this library entry");
        }

        // Get game details for response
        var sharedGame = await _sharedGameRepository.GetByIdAsync(entry.GameId, cancellationToken).ConfigureAwait(false)
            ?? throw new NotFoundException($"Game with ID {entry.GameId} not found in catalog");

        // Remove private PDF (raises PrivatePdfRemovedEvent)
        entry.RemovePrivatePdf();

        await _libraryRepository.UpdateAsync(entry, cancellationToken).ConfigureAwait(false);
        await _unitOfWork.SaveChangesAsync(cancellationToken).ConfigureAwait(false);

        return new UserLibraryEntryDto(
            Id: entry.Id,
            UserId: entry.UserId,
            GameId: entry.GameId,
            GameTitle: sharedGame.Title,
            GamePublisher: sharedGame.Publishers.FirstOrDefault()?.Name,
            GameYearPublished: sharedGame.YearPublished,
            GameIconUrl: sharedGame.ThumbnailUrl,
            GameImageUrl: sharedGame.ImageUrl,
            AddedAt: entry.AddedAt,
            Notes: entry.Notes?.Value,
            IsFavorite: entry.IsFavorite,
            CurrentState: entry.CurrentState.Value.ToString(),
            StateChangedAt: entry.CurrentState.ChangedAt,
            StateNotes: entry.CurrentState.StateNotes,
            CustomAgentConfig: entry.CustomAgentConfig is not null
                ? new AgentConfigDto(
                    LlmModel: entry.CustomAgentConfig.LlmModel,
                    Temperature: entry.CustomAgentConfig.Temperature,
                    MaxTokens: entry.CustomAgentConfig.MaxTokens,
                    Personality: entry.CustomAgentConfig.Personality,
                    DetailLevel: entry.CustomAgentConfig.DetailLevel,
                    PersonalNotes: entry.CustomAgentConfig.PersonalNotes
                )
                : null,
            CustomPdf: entry.CustomPdfMetadata is not null
                ? new CustomPdfDto(
                    Url: entry.CustomPdfMetadata.Url,
                    UploadedAt: entry.CustomPdfMetadata.UploadedAt,
                    FileSizeBytes: entry.CustomPdfMetadata.FileSizeBytes,
                    OriginalFileName: entry.CustomPdfMetadata.OriginalFileName
                )
                : null,
            HasKb: false,              // No longer has private PDF
            KbCardCount: 0,
            KbIndexedCount: 0,
            KbProcessingCount: 0,
            AgentIsOwned: true
        );
    }
}
