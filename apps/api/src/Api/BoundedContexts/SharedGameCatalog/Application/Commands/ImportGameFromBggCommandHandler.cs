using Api.BoundedContexts.SharedGameCatalog.Domain.Aggregates;
using Api.BoundedContexts.SharedGameCatalog.Domain.Repositories;
using Api.BoundedContexts.SharedGameCatalog.Domain.ValueObjects;
using Api.Infrastructure;
using Api.Infrastructure.Entities.SharedGameCatalog;
using Api.Services;
using Api.SharedKernel.Application.Interfaces;
using Api.SharedKernel.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace Api.BoundedContexts.SharedGameCatalog.Application.Commands;

/// <summary>
/// Handler for importing a board game from BoardGameGeek.
/// Fetches game metadata from BGG API and creates a SharedGame in Draft status.
/// Handles duplicate detection and relationship mapping (designers, publishers, categories, mechanics).
/// </summary>
internal sealed class ImportGameFromBggCommandHandler : ICommandHandler<ImportGameFromBggCommand, Guid>
{
    private readonly ISharedGameRepository _repository;
    private readonly IUnitOfWork _unitOfWork;
    private readonly IBggApiService _bggApiService;
    private readonly MeepleAiDbContext _dbContext;
    private readonly ILogger<ImportGameFromBggCommandHandler> _logger;

#pragma warning disable S1075 // URIs should not be hardcoded - Default/Fallback placeholder for games without BGG images
    private const string DefaultPlaceholderImageUrl = "https://via.placeholder.com/300x300?text=No+Image";
#pragma warning restore S1075

    public ImportGameFromBggCommandHandler(
        ISharedGameRepository repository,
        IUnitOfWork unitOfWork,
        IBggApiService bggApiService,
        MeepleAiDbContext dbContext,
        ILogger<ImportGameFromBggCommandHandler> logger)
    {
        _repository = repository ?? throw new ArgumentNullException(nameof(repository));
        _unitOfWork = unitOfWork ?? throw new ArgumentNullException(nameof(unitOfWork));
        _bggApiService = bggApiService ?? throw new ArgumentNullException(nameof(bggApiService));
        _dbContext = dbContext ?? throw new ArgumentNullException(nameof(dbContext));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    public async Task<Guid> Handle(ImportGameFromBggCommand command, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(command);

        _logger.LogInformation("Importing game from BGG: BggId={BggId}", command.BggId);

        // Check for duplicate BGG ID
        var exists = await _repository.ExistsByBggIdAsync(command.BggId, cancellationToken).ConfigureAwait(false);
        if (exists)
        {
            throw new InvalidOperationException($"A game with BGG ID {command.BggId} already exists in the catalog");
        }

        // Fetch game details from BGG API
        var bggDetails = await _bggApiService.GetGameDetailsAsync(command.BggId, cancellationToken).ConfigureAwait(false);
        if (bggDetails is null)
        {
            throw new InvalidOperationException($"Game with BGG ID {command.BggId} not found on BoardGameGeek");
        }

        _logger.LogInformation(
            "BGG game details fetched: {Name} ({Year})",
            bggDetails.Name, bggDetails.YearPublished);

        // Map BGG data to SharedGame aggregate
        var sharedGame = MapBggDetailsToSharedGame(bggDetails, command.BggId, command.UserId);

        // Add aggregate to repository (converts to entity)
        await _repository.AddAsync(sharedGame, cancellationToken).ConfigureAwait(false);

        // IMPORTANT: SaveChanges is deferred to allow relationship setup
        // We need the SharedGameEntity to be tracked before setting navigation properties

        // Get the tracked entity to setup relationships
        // Use Find() which checks the change tracker first before querying the database
        // This is necessary because the entity was just added and not yet persisted
        var gameEntity = await _dbContext.Set<SharedGameEntity>()
            .FindAsync([sharedGame.Id], cancellationToken)
            .ConfigureAwait(false);

        if (gameEntity is null)
        {
            throw new InvalidOperationException("Failed to track SharedGameEntity after AddAsync");
        }

        // Setup relationships (many-to-many)
        await SetupDesignersAsync(gameEntity, bggDetails.Designers, cancellationToken).ConfigureAwait(false);
        await SetupPublishersAsync(gameEntity, bggDetails.Publishers, cancellationToken).ConfigureAwait(false);
        await SetupCategoriesAsync(gameEntity, bggDetails.Categories, cancellationToken).ConfigureAwait(false);
        await SetupMechanicsAsync(gameEntity, bggDetails.Mechanics, cancellationToken).ConfigureAwait(false);

        // Commit all changes (game + relationships)
        await _unitOfWork.SaveChangesAsync(cancellationToken).ConfigureAwait(false);

        _logger.LogInformation(
            "Successfully imported game from BGG: {GameId} - {Title} (BggId: {BggId})",
            sharedGame.Id, sharedGame.Title, command.BggId);

        return sharedGame.Id;
    }

    private SharedGame MapBggDetailsToSharedGame(Models.BggGameDetailsDto bggDetails, int bggId, Guid userId)
    {
        // Map ratings (BGG uses different scales)
        var complexityRating = bggDetails.AverageWeight.HasValue
            ? (decimal?)Math.Round((decimal)bggDetails.AverageWeight.Value, 2)
            : null;

        var averageRating = bggDetails.AverageRating.HasValue
            ? (decimal?)Math.Round((decimal)bggDetails.AverageRating.Value, 2)
            : null;

        // Use PlayingTime or MinPlayTime as fallback
        var playingTimeMinutes = bggDetails.PlayingTime ?? bggDetails.MinPlayTime ?? 60;

        // Default images if missing
        var imageUrl = bggDetails.ImageUrl ?? DefaultPlaceholderImageUrl;
        var thumbnailUrl = bggDetails.ThumbnailUrl ?? imageUrl;

        // Create rules (if description is available)
        GameRules? rules = null;
        if (!string.IsNullOrWhiteSpace(bggDetails.Description))
        {
            // BGG description as initial rules content
            rules = GameRules.Create(bggDetails.Description, "en");
        }

        return SharedGame.Create(
            title: bggDetails.Name,
            yearPublished: bggDetails.YearPublished ?? DateTime.UtcNow.Year,
            description: bggDetails.Description ?? "Imported from BoardGameGeek",
            minPlayers: bggDetails.MinPlayers ?? 1,
            maxPlayers: bggDetails.MaxPlayers ?? 4,
            playingTimeMinutes: playingTimeMinutes,
            minAge: bggDetails.MinAge ?? 8,
            complexityRating: complexityRating,
            averageRating: averageRating,
            imageUrl: imageUrl,
            thumbnailUrl: thumbnailUrl,
            rules: rules,
            createdBy: userId,
            bggId: bggId);
    }

    private async Task SetupDesignersAsync(
        SharedGameEntity gameEntity,
        IList<string> designerNames,
        CancellationToken cancellationToken)
    {
        if (designerNames is null || designerNames.Count == 0)
            return;

        foreach (var designerName in designerNames.Distinct(StringComparer.Ordinal))
        {
            if (string.IsNullOrWhiteSpace(designerName))
                continue;

            // Find or create designer
            // Use AsNoTracking to avoid tracking issues, then attach if found
            var designer = await _dbContext.GameDesigners
                .AsNoTracking()
                .FirstOrDefaultAsync(d => d.Name == designerName, cancellationToken)
                .ConfigureAwait(false);

            if (designer is null)
            {
                designer = new GameDesignerEntity
                {
                    Id = Guid.NewGuid(),
                    Name = designerName,
                    CreatedAt = DateTime.UtcNow
                };
                await _dbContext.GameDesigners.AddAsync(designer, cancellationToken).ConfigureAwait(false);
                _logger.LogDebug("Created new designer: {DesignerName}", designerName);
            }
            else
            {
                // Check if already tracked (parallel imports may share entities)
                var tracked = _dbContext.ChangeTracker.Entries<GameDesignerEntity>()
                    .FirstOrDefault(e => e.Entity.Id == designer.Id);
                if (tracked != null)
                {
                    designer = tracked.Entity;
                }
                else
                {
                    // Attach existing entity as Unchanged to avoid duplicate insert
                    _dbContext.Attach(designer);
                }
            }

            gameEntity.Designers.Add(designer);
        }
    }

    private async Task SetupPublishersAsync(
        SharedGameEntity gameEntity,
        IList<string> publisherNames,
        CancellationToken cancellationToken)
    {
        if (publisherNames is null || publisherNames.Count == 0)
            return;

        foreach (var publisherName in publisherNames.Distinct(StringComparer.Ordinal))
        {
            if (string.IsNullOrWhiteSpace(publisherName))
                continue;

            // Find or create publisher
            // Use AsNoTracking to avoid tracking issues, then attach if found
            var publisher = await _dbContext.GamePublishers
                .AsNoTracking()
                .FirstOrDefaultAsync(p => p.Name == publisherName, cancellationToken)
                .ConfigureAwait(false);

            if (publisher is null)
            {
                publisher = new GamePublisherEntity
                {
                    Id = Guid.NewGuid(),
                    Name = publisherName,
                    CreatedAt = DateTime.UtcNow
                };
                await _dbContext.GamePublishers.AddAsync(publisher, cancellationToken).ConfigureAwait(false);
                _logger.LogDebug("Created new publisher: {PublisherName}", publisherName);
            }
            else
            {
                // Check if already tracked (parallel imports may share entities)
                var tracked = _dbContext.ChangeTracker.Entries<GamePublisherEntity>()
                    .FirstOrDefault(e => e.Entity.Id == publisher.Id);
                if (tracked != null)
                {
                    publisher = tracked.Entity;
                }
                else
                {
                    // Attach existing entity as Unchanged to avoid duplicate insert
                    _dbContext.Attach(publisher);
                }
            }

            gameEntity.Publishers.Add(publisher);
        }
    }

    private async Task SetupCategoriesAsync(
        SharedGameEntity gameEntity,
        IList<string> categoryNames,
        CancellationToken cancellationToken)
    {
        if (categoryNames is null || categoryNames.Count == 0)
            return;

        foreach (var categoryName in categoryNames.Distinct(StringComparer.Ordinal))
        {
            if (string.IsNullOrWhiteSpace(categoryName))
                continue;

            // Find existing category (categories should be pre-seeded)
            // Use AsNoTracking to avoid tracking issues, then attach if found
            var category = await _dbContext.GameCategories
                .AsNoTracking()
                .FirstOrDefaultAsync(c => c.Name == categoryName, cancellationToken)
                .ConfigureAwait(false);

            if (category is null)
            {
                // Create if missing (BGG may have new categories)
                var slug = categoryName.ToLowerInvariant().Replace(" ", "-");
                category = new GameCategoryEntity
                {
                    Id = Guid.NewGuid(),
                    Name = categoryName,
                    Slug = slug,
                    CreatedAt = DateTime.UtcNow
                };
                await _dbContext.GameCategories.AddAsync(category, cancellationToken).ConfigureAwait(false);
                _logger.LogDebug("Created new category: {CategoryName}", categoryName);
            }
            else
            {
                // Check if already tracked (parallel imports may share entities)
                var tracked = _dbContext.ChangeTracker.Entries<GameCategoryEntity>()
                    .FirstOrDefault(e => e.Entity.Id == category.Id);
                if (tracked != null)
                {
                    category = tracked.Entity;
                }
                else
                {
                    // Attach existing entity as Unchanged to avoid duplicate insert
                    _dbContext.Attach(category);
                }
            }

            gameEntity.Categories.Add(category);
        }
    }

    private async Task SetupMechanicsAsync(
        SharedGameEntity gameEntity,
        IList<string> mechanicNames,
        CancellationToken cancellationToken)
    {
        if (mechanicNames is null || mechanicNames.Count == 0)
            return;

        foreach (var mechanicName in mechanicNames.Distinct(StringComparer.Ordinal))
        {
            if (string.IsNullOrWhiteSpace(mechanicName))
                continue;

            // Find existing mechanic (mechanics should be pre-seeded)
            // Use AsNoTracking to avoid tracking issues, then attach if found
            var mechanic = await _dbContext.GameMechanics
                .AsNoTracking()
                .FirstOrDefaultAsync(m => m.Name == mechanicName, cancellationToken)
                .ConfigureAwait(false);

            if (mechanic is null)
            {
                // Create if missing (BGG may have new mechanics)
                var slug = mechanicName.ToLowerInvariant().Replace(" ", "-");
                mechanic = new GameMechanicEntity
                {
                    Id = Guid.NewGuid(),
                    Name = mechanicName,
                    Slug = slug,
                    CreatedAt = DateTime.UtcNow
                };
                await _dbContext.GameMechanics.AddAsync(mechanic, cancellationToken).ConfigureAwait(false);
                _logger.LogDebug("Created new mechanic: {MechanicName}", mechanicName);
            }
            else
            {
                // Check if already tracked (parallel imports may share entities)
                var tracked = _dbContext.ChangeTracker.Entries<GameMechanicEntity>()
                    .FirstOrDefault(e => e.Entity.Id == mechanic.Id);
                if (tracked != null)
                {
                    mechanic = tracked.Entity;
                }
                else
                {
                    // Attach existing entity as Unchanged to avoid duplicate insert
                    _dbContext.Attach(mechanic);
                }
            }

            gameEntity.Mechanics.Add(mechanic);
        }
    }
}
