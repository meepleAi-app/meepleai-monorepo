using Api.BoundedContexts.SharedGameCatalog.Domain.Repositories;
using Api.BoundedContexts.UserLibrary.Application.Commands;
using Api.BoundedContexts.UserLibrary.Application.DTOs;
using Api.BoundedContexts.UserLibrary.Domain.Repositories;
using Api.BoundedContexts.UserLibrary.Domain.ValueObjects;
using Api.SharedKernel.Application.Interfaces;
using Api.SharedKernel.Domain.Exceptions;
using Api.SharedKernel.Infrastructure.Persistence;

namespace Api.BoundedContexts.UserLibrary.Application.Handlers;

/// <summary>
/// Handler for uploading custom PDF rulebook for a game.
/// </summary>
internal class UploadCustomGamePdfCommandHandler : ICommandHandler<UploadCustomGamePdfCommand, UserLibraryEntryDto>
{
    private readonly IUserLibraryRepository _libraryRepository;
    private readonly ISharedGameRepository _sharedGameRepository;
    private readonly IUnitOfWork _unitOfWork;
    private readonly ILogger<UploadCustomGamePdfCommandHandler> _logger;

    public UploadCustomGamePdfCommandHandler(
        IUserLibraryRepository libraryRepository,
        ISharedGameRepository sharedGameRepository,
        IUnitOfWork unitOfWork,
        ILogger<UploadCustomGamePdfCommandHandler> logger)
    {
        _libraryRepository = libraryRepository ?? throw new ArgumentNullException(nameof(libraryRepository));
        _sharedGameRepository = sharedGameRepository ?? throw new ArgumentNullException(nameof(sharedGameRepository));
        _unitOfWork = unitOfWork ?? throw new ArgumentNullException(nameof(unitOfWork));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    public async Task<UserLibraryEntryDto> Handle(UploadCustomGamePdfCommand command, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(command);

        var entry = await _libraryRepository.GetByUserAndGameAsync(command.UserId, command.GameId, cancellationToken)
            .ConfigureAwait(false)
            ?? throw new DomainException($"Game {command.GameId} not found in user's library");

        var sharedGame = await _sharedGameRepository.GetByIdAsync(command.GameId, cancellationToken).ConfigureAwait(false)
            ?? throw new DomainException($"Game with ID {command.GameId} not found in catalog");

        var pdfMetadata = CustomPdfMetadata.Create(
            url: command.PdfUrl,
            fileSizeBytes: command.FileSizeBytes,
            originalFileName: command.OriginalFileName
        );

        entry.UploadCustomPdf(pdfMetadata);

        await _unitOfWork.SaveChangesAsync(cancellationToken).ConfigureAwait(false);

        _logger.LogInformation("Uploaded custom PDF for game {GameId} in library for user {UserId}", command.GameId, command.UserId);

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
            CustomPdf: new CustomPdfDto(
                Url: pdfMetadata.Url,
                UploadedAt: pdfMetadata.UploadedAt,
                FileSizeBytes: pdfMetadata.FileSizeBytes,
                OriginalFileName: pdfMetadata.OriginalFileName
            )
        );
    }
}
