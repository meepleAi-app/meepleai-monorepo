using Api.BoundedContexts.SharedGameCatalog.Domain.Aggregates;
using Api.BoundedContexts.SharedGameCatalog.Domain.Entities;
using Api.BoundedContexts.SharedGameCatalog.Domain.Enums;
using Api.BoundedContexts.SharedGameCatalog.Domain.Repositories;
using Api.SharedKernel.Application.Interfaces;
using Api.SharedKernel.Infrastructure.Persistence;
using FluentValidation;

namespace Api.BoundedContexts.SharedGameCatalog.Application.Commands;

// ── DTOs ─────────────────────────────────────────────────────────────

public sealed record ConfirmExcelImportRequest(
    IReadOnlyList<NewGameRequest> NewGames,
    IReadOnlyList<ModifyGameRequest> ModifiedGames,
    IReadOnlyList<Guid> RemovedGameIds);

public sealed record NewGameRequest(
    string Title,
    int? BggId,
    int? YearPublished,
    int? MinPlayers,
    int? MaxPlayers,
    int? PlayingTimeMinutes,
    int? MinAge,
    string? Description);

public sealed record ModifyGameRequest(
    Guid GameId,
    string Title,
    int? BggId,
    int? YearPublished,
    int? MinPlayers,
    int? MaxPlayers,
    int? PlayingTimeMinutes,
    int? MinAge,
    string? Description);

public sealed record ConfirmExcelImportResult(
    int Created,
    int Updated,
    int Deleted,
    int ProtectedSkipped,
    IReadOnlyList<string> Errors);

// ── Command ──────────────────────────────────────────────────────────

public sealed record ConfirmExcelImportCommand(
    ConfirmExcelImportRequest Request,
    Guid UserId) : ICommand<ConfirmExcelImportResult>;

// ── Validator ────────────────────────────────────────────────────────

internal sealed class ConfirmExcelImportCommandValidator
    : AbstractValidator<ConfirmExcelImportCommand>
{
    public ConfirmExcelImportCommandValidator()
    {
        RuleFor(x => x.UserId).NotEmpty();
        RuleFor(x => x.Request).NotNull();
        RuleFor(x => x.Request.NewGames.Count + x.Request.ModifiedGames.Count + x.Request.RemovedGameIds.Count)
            .GreaterThan(0)
            .When(x => x.Request is not null)
            .WithMessage("At least one change must be specified");
    }
}

// ── Handler ──────────────────────────────────────────────────────────

internal sealed class ConfirmExcelImportCommandHandler
    : ICommandHandler<ConfirmExcelImportCommand, ConfirmExcelImportResult>
{
    private readonly ISharedGameRepository _repository;
    private readonly IUnitOfWork _unitOfWork;
    private readonly TimeProvider _timeProvider;
    private readonly ILogger<ConfirmExcelImportCommandHandler> _logger;

    public ConfirmExcelImportCommandHandler(
        ISharedGameRepository repository,
        IUnitOfWork unitOfWork,
        TimeProvider timeProvider,
        ILogger<ConfirmExcelImportCommandHandler> logger)
    {
        _repository = repository ?? throw new ArgumentNullException(nameof(repository));
        _unitOfWork = unitOfWork ?? throw new ArgumentNullException(nameof(unitOfWork));
        _timeProvider = timeProvider ?? throw new ArgumentNullException(nameof(timeProvider));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    public async Task<ConfirmExcelImportResult> Handle(
        ConfirmExcelImportCommand command, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(command);

        var errors = new List<string>();
        var created = 0;
        var updated = 0;
        var deleted = 0;
        var protectedSkipped = 0;
        var request = command.Request;

        // ── Create new games ──
        foreach (var newGame in request.NewGames)
        {
            try
            {
                var game = SharedGame.CreateSkeleton(
                    newGame.Title, command.UserId, _timeProvider, newGame.BggId);

                await _repository.AddAsync(game, cancellationToken).ConfigureAwait(false);
                await _unitOfWork.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
                created++;
            }
            catch (Exception ex)
            {
                errors.Add($"Failed to create '{newGame.Title}': {ex.Message}");
                _logger.LogWarning(ex, "Failed to create game '{Title}' from Excel confirm", newGame.Title);
            }
        }

        // ── Update modified games (Draft only) ──
        foreach (var mod in request.ModifiedGames)
        {
            try
            {
                var game = await _repository
                    .GetByIdAsync(mod.GameId, cancellationToken)
                    .ConfigureAwait(false);

                if (game is null)
                {
                    errors.Add($"Game {mod.GameId} not found");
                    continue;
                }

                if (game.Status != GameStatus.Draft)
                {
                    protectedSkipped++;
                    continue;
                }

                // Skeleton/Failed games: only assign BGG ID (UpdateInfo fails on zero-filled fields)
                if (game.GameDataStatus is GameDataStatus.Skeleton or GameDataStatus.Failed)
                {
                    if (mod.BggId.HasValue && !game.BggId.HasValue)
                    {
                        game.AssignBggId(mod.BggId.Value, command.UserId);
                    }
                }
                else
                {
                    // Enriched/Complete games: full update with domain validation
                    game.UpdateInfo(
                        mod.Title,
                        mod.YearPublished ?? game.YearPublished,
                        !string.IsNullOrWhiteSpace(mod.Description) ? mod.Description : game.Description,
                        mod.MinPlayers ?? game.MinPlayers,
                        mod.MaxPlayers ?? game.MaxPlayers,
                        mod.PlayingTimeMinutes ?? game.PlayingTimeMinutes,
                        mod.MinAge ?? game.MinAge,
                        game.ComplexityRating,
                        game.AverageRating,
                        game.ImageUrl,
                        game.ThumbnailUrl,
                        game.Rules,
                        command.UserId);
                }

                _repository.Update(game);
                await _unitOfWork.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
                updated++;
            }
            catch (Exception ex)
            {
                errors.Add($"Failed to update game {mod.GameId}: {ex.Message}");
                _logger.LogWarning(ex, "Failed to update game {GameId} from Excel confirm", mod.GameId);
            }
        }

        // ── Soft-delete removed games (Draft only) ──
        foreach (var gameId in request.RemovedGameIds)
        {
            try
            {
                var game = await _repository
                    .GetByIdAsync(gameId, cancellationToken)
                    .ConfigureAwait(false);

                if (game is null)
                {
                    errors.Add($"Game {gameId} not found for deletion");
                    continue;
                }

                if (game.Status != GameStatus.Draft)
                {
                    protectedSkipped++;
                    continue;
                }

                game.Delete(command.UserId);
                _repository.Update(game);
                await _unitOfWork.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
                deleted++;
            }
            catch (Exception ex)
            {
                errors.Add($"Failed to delete game {gameId}: {ex.Message}");
                _logger.LogWarning(ex, "Failed to delete game {GameId} from Excel confirm", gameId);
            }
        }

        _logger.LogInformation(
            "Excel import confirmed: {Created} created, {Updated} updated, {Deleted} deleted, {Protected} protected, {Errors} errors",
            created, updated, deleted, protectedSkipped, errors.Count);

        return new ConfirmExcelImportResult(created, updated, deleted, protectedSkipped, errors);
    }
}
