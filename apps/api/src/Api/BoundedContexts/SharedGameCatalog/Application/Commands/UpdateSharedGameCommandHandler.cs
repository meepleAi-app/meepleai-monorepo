using Api.BoundedContexts.SharedGameCatalog.Domain.Aggregates;
using Api.BoundedContexts.SharedGameCatalog.Domain.Repositories;
using Api.BoundedContexts.SharedGameCatalog.Domain.ValueObjects;
using Api.Infrastructure;
using Api.Infrastructure.Entities.SharedGameCatalog;
using Api.Middleware.Exceptions;
using Api.SharedKernel.Application.Interfaces;
using Api.SharedKernel.Infrastructure.Persistence;
using MediatR;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace Api.BoundedContexts.SharedGameCatalog.Application.Commands;

/// <summary>
/// Handler for updating an existing shared game.
/// Supports core fields (via domain UpdateInfo) and optional manual updates of
/// BggId + taxonomy collections (categories, mechanics, designers, publishers).
/// </summary>
/// <remarks>
/// Uses MeepleAiDbContext directly for relationship management — same pattern as
/// UpdateSharedGameFromBggCommandHandler. The repository abstraction does not
/// support tracked Include for collection-replace semantics.
/// </remarks>
internal sealed class UpdateSharedGameCommandHandler : ICommandHandler<UpdateSharedGameCommand, Unit>
{
    private readonly ISharedGameRepository _repository;
    private readonly IUnitOfWork _unitOfWork;
    private readonly MeepleAiDbContext _dbContext;
    private readonly ILogger<UpdateSharedGameCommandHandler> _logger;

    public UpdateSharedGameCommandHandler(
        ISharedGameRepository repository,
        IUnitOfWork unitOfWork,
        MeepleAiDbContext dbContext,
        ILogger<UpdateSharedGameCommandHandler> logger)
    {
        _repository = repository ?? throw new ArgumentNullException(nameof(repository));
        _unitOfWork = unitOfWork ?? throw new ArgumentNullException(nameof(unitOfWork));
        _dbContext = dbContext ?? throw new ArgumentNullException(nameof(dbContext));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    public async Task<Unit> Handle(UpdateSharedGameCommand command, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(command);

        _logger.LogInformation(
            "Updating shared game: {GameId}, ModifiedBy: {UserId}",
            command.GameId, command.ModifiedBy);

        // ─── 1. Update aggregate core fields via domain method ─────────────
        var game = await _repository.GetByIdAsync(command.GameId, cancellationToken).ConfigureAwait(false);
        if (game is null)
        {
            throw new NotFoundException("SharedGame", command.GameId.ToString());
        }

        GameRules? rules = null;
        if (command.Rules is not null)
        {
            rules = GameRules.Create(command.Rules.Content, command.Rules.Language);
        }

        game.UpdateInfo(
            command.Title, command.YearPublished, command.Description,
            command.MinPlayers, command.MaxPlayers, command.PlayingTimeMinutes,
            command.MinAge, command.ComplexityRating, command.AverageRating,
            command.ImageUrl, command.ThumbnailUrl, rules, command.ModifiedBy);

        _repository.Update(game);

        // ─── 2. Update entity-level fields (BggId + collections) ────────────
        // Only fetched if any non-null new field is present; null = no change.
        var needsEntityUpdate = command.BggId.HasValue
            || command.Categories is not null
            || command.Mechanics is not null
            || command.Designers is not null
            || command.Publishers is not null;

        if (needsEntityUpdate)
        {
            var entity = await _dbContext.Set<SharedGameEntity>()
                .Include(e => e.Categories)
                .Include(e => e.Mechanics)
                .Include(e => e.Designers)
                .Include(e => e.Publishers)
                // #3866: tracked on purpose — the mutation below only reaches the DB on a tracked entity (production defaults to NoTracking, PERF-06).
                .AsTracking()
                .FirstOrDefaultAsync(e => e.Id == command.GameId, cancellationToken)
                .ConfigureAwait(false);

            if (entity is null)
            {
                throw new NotFoundException("SharedGame", command.GameId.ToString());
            }

            if (command.BggId.HasValue)
            {
                entity.BggId = command.BggId.Value;
            }

            if (command.Categories is not null)
            {
                await ReplaceCategoriesAsync(entity, command.Categories, cancellationToken).ConfigureAwait(false);
            }

            if (command.Mechanics is not null)
            {
                await ReplaceMechanicsAsync(entity, command.Mechanics, cancellationToken).ConfigureAwait(false);
            }

            if (command.Designers is not null)
            {
                await ReplaceDesignersAsync(entity, command.Designers, cancellationToken).ConfigureAwait(false);
            }

            if (command.Publishers is not null)
            {
                await ReplacePublishersAsync(entity, command.Publishers, cancellationToken).ConfigureAwait(false);
            }
        }

        await _unitOfWork.SaveChangesAsync(cancellationToken).ConfigureAwait(false);

        _logger.LogInformation(
            "Shared game updated successfully: {GameId}",
            command.GameId);

        return Unit.Value;
    }

    private async Task ReplaceCategoriesAsync(SharedGameEntity entity, List<string> names, CancellationToken ct)
    {
        entity.Categories.Clear();
        foreach (var name in names.Where(n => !string.IsNullOrWhiteSpace(n)).Distinct(StringComparer.Ordinal))
        {
            var category = await _dbContext.GameCategories
                .AsNoTracking()
                .FirstOrDefaultAsync(c => c.Name == name, ct).ConfigureAwait(false);
            if (category is null)
            {
                category = new GameCategoryEntity
                {
                    Id = Guid.NewGuid(),
                    Name = name,
                    Slug = name.ToLowerInvariant().Replace(" ", "-"),
                    CreatedAt = DateTime.UtcNow
                };
                await _dbContext.GameCategories.AddAsync(category, ct).ConfigureAwait(false);
            }
            else
            {
                var tracked = _dbContext.ChangeTracker.Entries<GameCategoryEntity>()
                    .FirstOrDefault(e => e.Entity.Id == category.Id);
                if (tracked != null)
                {
                    category = tracked.Entity;
                }
                else
                {
                    _dbContext.Attach(category);
                }
            }
            entity.Categories.Add(category);
        }
    }

    private async Task ReplaceMechanicsAsync(SharedGameEntity entity, List<string> names, CancellationToken ct)
    {
        entity.Mechanics.Clear();
        foreach (var name in names.Where(n => !string.IsNullOrWhiteSpace(n)).Distinct(StringComparer.Ordinal))
        {
            var mechanic = await _dbContext.GameMechanics
                .AsNoTracking()
                .FirstOrDefaultAsync(m => m.Name == name, ct).ConfigureAwait(false);
            if (mechanic is null)
            {
                mechanic = new GameMechanicEntity
                {
                    Id = Guid.NewGuid(),
                    Name = name,
                    Slug = name.ToLowerInvariant().Replace(" ", "-"),
                    CreatedAt = DateTime.UtcNow
                };
                await _dbContext.GameMechanics.AddAsync(mechanic, ct).ConfigureAwait(false);
            }
            else
            {
                var tracked = _dbContext.ChangeTracker.Entries<GameMechanicEntity>()
                    .FirstOrDefault(e => e.Entity.Id == mechanic.Id);
                if (tracked != null)
                {
                    mechanic = tracked.Entity;
                }
                else
                {
                    _dbContext.Attach(mechanic);
                }
            }
            entity.Mechanics.Add(mechanic);
        }
    }

    private async Task ReplaceDesignersAsync(SharedGameEntity entity, List<string> names, CancellationToken ct)
    {
        entity.Designers.Clear();
        foreach (var name in names.Where(n => !string.IsNullOrWhiteSpace(n)).Distinct(StringComparer.Ordinal))
        {
            var designer = await _dbContext.GameDesigners
                .AsNoTracking()
                .FirstOrDefaultAsync(d => d.Name == name, ct).ConfigureAwait(false);
            if (designer is null)
            {
                designer = new GameDesignerEntity
                {
                    Id = Guid.NewGuid(),
                    Name = name,
                    CreatedAt = DateTime.UtcNow
                };
                await _dbContext.GameDesigners.AddAsync(designer, ct).ConfigureAwait(false);
            }
            else
            {
                var tracked = _dbContext.ChangeTracker.Entries<GameDesignerEntity>()
                    .FirstOrDefault(e => e.Entity.Id == designer.Id);
                if (tracked != null)
                {
                    designer = tracked.Entity;
                }
                else
                {
                    _dbContext.Attach(designer);
                }
            }
            entity.Designers.Add(designer);
        }
    }

    private async Task ReplacePublishersAsync(SharedGameEntity entity, List<string> names, CancellationToken ct)
    {
        entity.Publishers.Clear();
        foreach (var name in names.Where(n => !string.IsNullOrWhiteSpace(n)).Distinct(StringComparer.Ordinal))
        {
            var publisher = await _dbContext.GamePublishers
                .AsNoTracking()
                .FirstOrDefaultAsync(p => p.Name == name, ct).ConfigureAwait(false);
            if (publisher is null)
            {
                publisher = new GamePublisherEntity
                {
                    Id = Guid.NewGuid(),
                    Name = name,
                    CreatedAt = DateTime.UtcNow
                };
                await _dbContext.GamePublishers.AddAsync(publisher, ct).ConfigureAwait(false);
            }
            else
            {
                var tracked = _dbContext.ChangeTracker.Entries<GamePublisherEntity>()
                    .FirstOrDefault(e => e.Entity.Id == publisher.Id);
                if (tracked != null)
                {
                    publisher = tracked.Entity;
                }
                else
                {
                    _dbContext.Attach(publisher);
                }
            }
            entity.Publishers.Add(publisher);
        }
    }
}
