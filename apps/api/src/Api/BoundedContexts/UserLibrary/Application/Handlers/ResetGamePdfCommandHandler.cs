using Api.BoundedContexts.SharedGameCatalog.Domain.Repositories;
using Api.BoundedContexts.UserLibrary.Application.Commands;
using Api.BoundedContexts.UserLibrary.Application.DTOs;
using Api.BoundedContexts.UserLibrary.Domain.Repositories;
using Api.SharedKernel.Application.Interfaces;
using Api.SharedKernel.Domain.Exceptions;
using Api.SharedKernel.Infrastructure.Persistence;

namespace Api.BoundedContexts.UserLibrary.Application.Handlers;

/// <summary>
/// Handler for resetting to SharedGame's default PDF rulebook.
/// </summary>
internal class ResetGamePdfCommandHandler : ICommandHandler<ResetGamePdfCommand, UserLibraryEntryDto>
{
    private readonly IUserLibraryRepository _libraryRepository;
    private readonly ISharedGameRepository _sharedGameRepository;
    private readonly IUnitOfWork _unitOfWork;

    public ResetGamePdfCommandHandler(
        IUserLibraryRepository libraryRepository,
        ISharedGameRepository sharedGameRepository,
        IUnitOfWork unitOfWork)
    {
        _libraryRepository = libraryRepository ?? throw new ArgumentNullException(nameof(libraryRepository));
        _sharedGameRepository = sharedGameRepository ?? throw new ArgumentNullException(nameof(sharedGameRepository));
        _unitOfWork = unitOfWork ?? throw new ArgumentNullException(nameof(unitOfWork));
    }

    public async Task<UserLibraryEntryDto> Handle(ResetGamePdfCommand command, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(command);

        var entry = await _libraryRepository.GetByUserAndGameAsync(command.UserId, command.GameId, cancellationToken)
            .ConfigureAwait(false)
            ?? throw new DomainException($"Game {command.GameId} not found in user's library");

        var sharedGame = await _sharedGameRepository.GetByIdAsync(command.GameId, cancellationToken).ConfigureAwait(false)
            ?? throw new DomainException($"Game with ID {command.GameId} not found in catalog");

        entry.ResetPdfToShared();

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
            CustomPdf: null // Always null after reset
        );
    }
}
