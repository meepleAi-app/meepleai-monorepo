using System.Globalization;
using Api.Infrastructure;
using Api.Infrastructure.Entities.SharedGameCatalog;
using Api.Middleware.Exceptions;
using Api.Services;
using Api.SharedKernel.Application.Interfaces;
using Api.SharedKernel.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace Api.BoundedContexts.SharedGameCatalog.Application.Commands;

/// <summary>
/// Handler for updating an existing shared game with fresh data from BoardGameGeek.
/// Supports selective field updates based on FieldsToUpdate list.
/// Issue: Admin Add Shared Game from BGG flow - "Propose Update" functionality
/// </summary>
/// <remarks>
/// Note: This handler uses MeepleAiDbContext directly for relationship management
/// (designers, publishers, categories, mechanics) which requires EF Core tracked entities
/// with Include operations. This is a legitimate infrastructure concern that the
/// repository abstraction doesn't support without leaking EF-specific concepts into the domain.
/// </remarks>
internal sealed class UpdateSharedGameFromBggCommandHandler : ICommandHandler<UpdateSharedGameFromBggCommand, Guid>
{
    private readonly IUnitOfWork _unitOfWork;
    private readonly IBggApiService _bggApiService;
    private readonly MeepleAiDbContext _dbContext;
    private readonly ILogger<UpdateSharedGameFromBggCommandHandler> _logger;

#pragma warning disable S1075 // URIs should not be hardcoded - Default/Fallback placeholder for games without BGG images
    private const string DefaultPlaceholderImageUrl = "https://via.placeholder.com/300x300?text=No+Image";
#pragma warning restore S1075

    public UpdateSharedGameFromBggCommandHandler(
        IUnitOfWork unitOfWork,
        IBggApiService bggApiService,
        MeepleAiDbContext dbContext,
        ILogger<UpdateSharedGameFromBggCommandHandler> logger)
    {
        _unitOfWork = unitOfWork ?? throw new ArgumentNullException(nameof(unitOfWork));
        _bggApiService = bggApiService ?? throw new ArgumentNullException(nameof(bggApiService));
        _dbContext = dbContext ?? throw new ArgumentNullException(nameof(dbContext));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    public async Task<Guid> Handle(UpdateSharedGameFromBggCommand command, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(command);

        _logger.LogInformation(
            "Updating game {GameId} from BGG {BggId} with fields: {Fields}",
            command.GameId,
            command.BggId,
            command.FieldsToUpdate is null or { Count: 0 } ? "ALL" : string.Join(", ", command.FieldsToUpdate));

        // Get the tracked entity with all relationships for update
        var gameEntity = await _dbContext.Set<SharedGameEntity>()
            .Include(g => g.Designers)
            .Include(g => g.Publishers)
            .Include(g => g.Categories)
            .Include(g => g.Mechanics)
            .FirstOrDefaultAsync(g => g.Id == command.GameId, cancellationToken)
            .ConfigureAwait(false);

        if (gameEntity is null)
        {
            throw new NotFoundException("SharedGame", command.GameId.ToString());
        }

        // Fetch fresh BGG data
        var bggDetails = await _bggApiService.GetGameDetailsAsync(command.BggId, cancellationToken).ConfigureAwait(false);
        if (bggDetails is null)
        {
            throw new NotFoundException("BggGame", command.BggId.ToString(CultureInfo.InvariantCulture));
        }

        // Determine which fields to update
        IReadOnlySet<string> fieldsToUpdate = command.FieldsToUpdate is null or { Count: 0 }
            ? BggUpdatableFields.All
            : new HashSet<string>(command.FieldsToUpdate, StringComparer.OrdinalIgnoreCase);

        // Update basic fields selectively
        UpdateBasicFields(gameEntity, bggDetails, fieldsToUpdate);

        // Update relationships selectively
        if (fieldsToUpdate.Contains(BggUpdatableFields.Designers))
        {
            await UpdateDesignersAsync(gameEntity, bggDetails.Designers, cancellationToken).ConfigureAwait(false);
        }

        if (fieldsToUpdate.Contains(BggUpdatableFields.Publishers))
        {
            await UpdatePublishersAsync(gameEntity, bggDetails.Publishers, cancellationToken).ConfigureAwait(false);
        }

        if (fieldsToUpdate.Contains(BggUpdatableFields.Categories))
        {
            await UpdateCategoriesAsync(gameEntity, bggDetails.Categories, cancellationToken).ConfigureAwait(false);
        }

        if (fieldsToUpdate.Contains(BggUpdatableFields.Mechanics))
        {
            await UpdateMechanicsAsync(gameEntity, bggDetails.Mechanics, cancellationToken).ConfigureAwait(false);
        }

        // Update audit fields
        gameEntity.ModifiedBy = command.UserId;
        gameEntity.ModifiedAt = DateTime.UtcNow;

        // Save all changes
        await _unitOfWork.SaveChangesAsync(cancellationToken).ConfigureAwait(false);

        _logger.LogInformation(
            "Successfully updated game {GameId} from BGG {BggId}",
            command.GameId,
            command.BggId);

        return command.GameId;
    }

    private static void UpdateBasicFields(
        SharedGameEntity entity,
        Models.BggGameDetailsDto bggDetails,
        IReadOnlySet<string> fieldsToUpdate)
    {
        if (fieldsToUpdate.Contains(BggUpdatableFields.Title))
        {
            entity.Title = bggDetails.Name;
        }

        if (fieldsToUpdate.Contains(BggUpdatableFields.Description) && !string.IsNullOrWhiteSpace(bggDetails.Description))
        {
            entity.Description = bggDetails.Description;
        }

        if (fieldsToUpdate.Contains(BggUpdatableFields.YearPublished) && bggDetails.YearPublished.HasValue)
        {
            entity.YearPublished = bggDetails.YearPublished.Value;
        }

        if (fieldsToUpdate.Contains(BggUpdatableFields.MinPlayers) && bggDetails.MinPlayers.HasValue)
        {
            entity.MinPlayers = bggDetails.MinPlayers.Value;
        }

        if (fieldsToUpdate.Contains(BggUpdatableFields.MaxPlayers) && bggDetails.MaxPlayers.HasValue)
        {
            entity.MaxPlayers = bggDetails.MaxPlayers.Value;
        }

        if (fieldsToUpdate.Contains(BggUpdatableFields.PlayingTime))
        {
            entity.PlayingTimeMinutes = bggDetails.PlayingTime ?? bggDetails.MinPlayTime ?? 60;
        }

        if (fieldsToUpdate.Contains(BggUpdatableFields.MinAge) && bggDetails.MinAge.HasValue)
        {
            entity.MinAge = bggDetails.MinAge.Value;
        }

        if (fieldsToUpdate.Contains(BggUpdatableFields.ComplexityRating) && bggDetails.AverageWeight.HasValue)
        {
            entity.ComplexityRating = (decimal?)Math.Round((decimal)bggDetails.AverageWeight.Value, 2);
        }

        if (fieldsToUpdate.Contains(BggUpdatableFields.AverageRating) && bggDetails.AverageRating.HasValue)
        {
            entity.AverageRating = (decimal?)Math.Round((decimal)bggDetails.AverageRating.Value, 2);
        }

        if (fieldsToUpdate.Contains(BggUpdatableFields.ImageUrl))
        {
            entity.ImageUrl = bggDetails.ImageUrl ?? DefaultPlaceholderImageUrl;
        }

        if (fieldsToUpdate.Contains(BggUpdatableFields.ThumbnailUrl))
        {
            entity.ThumbnailUrl = bggDetails.ThumbnailUrl ?? bggDetails.ImageUrl ?? DefaultPlaceholderImageUrl;
        }
    }

    private async Task UpdateDesignersAsync(
        SharedGameEntity gameEntity,
        IList<string> designerNames,
        CancellationToken cancellationToken)
    {
        // Clear existing designers
        gameEntity.Designers.Clear();

        if (designerNames is null || designerNames.Count == 0)
            return;

        foreach (var designerName in designerNames.Distinct(StringComparer.Ordinal))
        {
            if (string.IsNullOrWhiteSpace(designerName))
                continue;

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

            gameEntity.Designers.Add(designer);
        }
    }

    private async Task UpdatePublishersAsync(
        SharedGameEntity gameEntity,
        IList<string> publisherNames,
        CancellationToken cancellationToken)
    {
        gameEntity.Publishers.Clear();

        if (publisherNames is null || publisherNames.Count == 0)
            return;

        foreach (var publisherName in publisherNames.Distinct(StringComparer.Ordinal))
        {
            if (string.IsNullOrWhiteSpace(publisherName))
                continue;

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

            gameEntity.Publishers.Add(publisher);
        }
    }

    private async Task UpdateCategoriesAsync(
        SharedGameEntity gameEntity,
        IList<string> categoryNames,
        CancellationToken cancellationToken)
    {
        gameEntity.Categories.Clear();

        if (categoryNames is null || categoryNames.Count == 0)
            return;

        foreach (var categoryName in categoryNames.Distinct(StringComparer.Ordinal))
        {
            if (string.IsNullOrWhiteSpace(categoryName))
                continue;

            var category = await _dbContext.GameCategories
                .AsNoTracking()
                .FirstOrDefaultAsync(c => c.Name == categoryName, cancellationToken)
                .ConfigureAwait(false);

            if (category is null)
            {
                var slug = categoryName.ToLowerInvariant().Replace(" ", "-");
                category = new GameCategoryEntity
                {
                    Id = Guid.NewGuid(),
                    Name = categoryName,
                    Slug = slug,
                    CreatedAt = DateTime.UtcNow
                };
                await _dbContext.GameCategories.AddAsync(category, cancellationToken).ConfigureAwait(false);
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

            gameEntity.Categories.Add(category);
        }
    }

    private async Task UpdateMechanicsAsync(
        SharedGameEntity gameEntity,
        IList<string> mechanicNames,
        CancellationToken cancellationToken)
    {
        gameEntity.Mechanics.Clear();

        if (mechanicNames is null || mechanicNames.Count == 0)
            return;

        foreach (var mechanicName in mechanicNames.Distinct(StringComparer.Ordinal))
        {
            if (string.IsNullOrWhiteSpace(mechanicName))
                continue;

            var mechanic = await _dbContext.GameMechanics
                .AsNoTracking()
                .FirstOrDefaultAsync(m => m.Name == mechanicName, cancellationToken)
                .ConfigureAwait(false);

            if (mechanic is null)
            {
                var slug = mechanicName.ToLowerInvariant().Replace(" ", "-");
                mechanic = new GameMechanicEntity
                {
                    Id = Guid.NewGuid(),
                    Name = mechanicName,
                    Slug = slug,
                    CreatedAt = DateTime.UtcNow
                };
                await _dbContext.GameMechanics.AddAsync(mechanic, cancellationToken).ConfigureAwait(false);
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

            gameEntity.Mechanics.Add(mechanic);
        }
    }
}
