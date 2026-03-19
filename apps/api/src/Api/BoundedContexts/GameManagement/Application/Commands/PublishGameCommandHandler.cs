using Api.BoundedContexts.GameManagement.Application.Commands;
using Api.BoundedContexts.GameManagement.Application.DTOs;
using Api.BoundedContexts.GameManagement.Domain.Repositories;
using Api.BoundedContexts.GameManagement.Domain.ValueObjects;
using Api.SharedKernel.Application.Interfaces;
using Api.SharedKernel.Infrastructure.Persistence;

namespace Api.BoundedContexts.GameManagement.Application.Commands;

/// <summary>
/// Issue #3481: Handles game publication workflow.
/// Requires admin privileges (enforced at endpoint level).
/// </summary>
internal class PublishGameCommandHandler : ICommandHandler<PublishGameCommand, GameDto>
{
    private readonly IGameRepository _gameRepository;
    private readonly IUnitOfWork _unitOfWork;

    public PublishGameCommandHandler(
        IGameRepository gameRepository,
        IUnitOfWork unitOfWork)
    {
        _gameRepository = gameRepository ?? throw new ArgumentNullException(nameof(gameRepository));
        _unitOfWork = unitOfWork ?? throw new ArgumentNullException(nameof(unitOfWork));
    }

    public async Task<GameDto> Handle(PublishGameCommand command, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(command);

        // Validate approval status
        if (!Enum.IsDefined(command.Status))
            throw new ArgumentException($"Invalid approval status: {command.Status}", nameof(command));

        // Load game
        var game = await _gameRepository.GetByIdAsync(command.GameId, cancellationToken).ConfigureAwait(false)
            ?? throw new InvalidOperationException($"Game with ID {command.GameId} not found");

        // Publish with status
        game.Publish(command.Status);

        // Persist changes
        await _gameRepository.UpdateAsync(game, cancellationToken).ConfigureAwait(false);
        await _unitOfWork.SaveChangesAsync(cancellationToken).ConfigureAwait(false);

        // Return DTO
        return new GameDto(
            Id: game.Id,
            Title: game.Title.Value,
            Publisher: game.Publisher?.Name,
            YearPublished: game.YearPublished?.Value,
            MinPlayers: game.PlayerCount?.Min,
            MaxPlayers: game.PlayerCount?.Max,
            MinPlayTimeMinutes: game.PlayTime?.MinMinutes,
            MaxPlayTimeMinutes: game.PlayTime?.MaxMinutes,
            BggId: game.BggId,
            CreatedAt: game.CreatedAt,
            IconUrl: game.IconUrl,
            ImageUrl: game.ImageUrl,
            SharedGameId: game.SharedGameId,
            IsPublished: game.IsPublished,
            ApprovalStatus: game.ApprovalStatus.ToString(),
            PublishedAt: game.PublishedAt
        );
    }
}
