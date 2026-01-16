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
/// Handler for configuring custom AI agent for a game in user's library.
/// </summary>
internal class ConfigureGameAgentCommandHandler : ICommandHandler<ConfigureGameAgentCommand, UserLibraryEntryDto>
{
    private readonly IUserLibraryRepository _libraryRepository;
    private readonly ISharedGameRepository _sharedGameRepository;
    private readonly IUnitOfWork _unitOfWork;
    private readonly ILogger<ConfigureGameAgentCommandHandler> _logger;

    public ConfigureGameAgentCommandHandler(
        IUserLibraryRepository libraryRepository,
        ISharedGameRepository sharedGameRepository,
        IUnitOfWork unitOfWork,
        ILogger<ConfigureGameAgentCommandHandler> logger)
    {
        _libraryRepository = libraryRepository ?? throw new ArgumentNullException(nameof(libraryRepository));
        _sharedGameRepository = sharedGameRepository ?? throw new ArgumentNullException(nameof(sharedGameRepository));
        _unitOfWork = unitOfWork ?? throw new ArgumentNullException(nameof(unitOfWork));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    public async Task<UserLibraryEntryDto> Handle(ConfigureGameAgentCommand command, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(command);

        // Find existing library entry
        var entry = await _libraryRepository.GetByUserAndGameAsync(command.UserId, command.GameId, cancellationToken)
            .ConfigureAwait(false)
            ?? throw new DomainException($"Game {command.GameId} not found in user's library");

        // Get shared game for DTO
        var sharedGame = await _sharedGameRepository.GetByIdAsync(command.GameId, cancellationToken).ConfigureAwait(false)
            ?? throw new DomainException($"Game with ID {command.GameId} not found in catalog");

        // Create agent configuration value object
        var agentConfig = AgentConfiguration.Create(
            llmModel: command.AgentConfig.LlmModel,
            temperature: command.AgentConfig.Temperature,
            maxTokens: command.AgentConfig.MaxTokens,
            personality: command.AgentConfig.Personality,
            detailLevel: command.AgentConfig.DetailLevel,
            personalNotes: command.AgentConfig.PersonalNotes
        );

        // Configure agent through domain method
        entry.ConfigureAgent(agentConfig);

        // Persist changes
        await _unitOfWork.SaveChangesAsync(cancellationToken).ConfigureAwait(false);

        _logger.LogInformation("Configured custom agent for game {GameId} in library for user {UserId}", command.GameId, command.UserId);

        // Map to DTO
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
            CustomPdf: entry.CustomPdfMetadata is not null
                ? new CustomPdfDto(
                    Url: entry.CustomPdfMetadata.Url,
                    UploadedAt: entry.CustomPdfMetadata.UploadedAt,
                    FileSizeBytes: entry.CustomPdfMetadata.FileSizeBytes,
                    OriginalFileName: entry.CustomPdfMetadata.OriginalFileName
                )
                : null
        );
    }
}
