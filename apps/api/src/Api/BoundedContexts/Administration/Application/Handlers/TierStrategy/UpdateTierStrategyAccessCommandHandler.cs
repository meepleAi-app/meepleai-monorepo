using Api.BoundedContexts.Administration.Application.Commands.TierStrategy;
using Api.BoundedContexts.Administration.Application.DTOs;
using Api.BoundedContexts.KnowledgeBase.Domain.Enums;
using Api.BoundedContexts.SystemConfiguration.Domain.Enums;
using Api.Infrastructure;
using Api.Infrastructure.Entities.KnowledgeBase;
using Api.SharedKernel.Application.Interfaces;
using Api.SharedKernel.Domain.Exceptions;
using Api.SharedKernel.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace Api.BoundedContexts.Administration.Application.Handlers.TierStrategy;

/// <summary>
/// Handler for UpdateTierStrategyAccessCommand.
/// Updates the access configuration for a tier-strategy combination.
/// Issue #3440: Admin UI for tier-strategy configuration.
/// </summary>
internal class UpdateTierStrategyAccessCommandHandler
    : ICommandHandler<UpdateTierStrategyAccessCommand, TierStrategyAccessDto>
{
    private readonly MeepleAiDbContext _dbContext;
    private readonly IUnitOfWork _unitOfWork;
    private readonly ILogger<UpdateTierStrategyAccessCommandHandler> _logger;

    public UpdateTierStrategyAccessCommandHandler(
        MeepleAiDbContext dbContext,
        IUnitOfWork unitOfWork,
        ILogger<UpdateTierStrategyAccessCommandHandler> logger)
    {
        _dbContext = dbContext ?? throw new ArgumentNullException(nameof(dbContext));
        _unitOfWork = unitOfWork ?? throw new ArgumentNullException(nameof(unitOfWork));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    public async Task<TierStrategyAccessDto> Handle(
        UpdateTierStrategyAccessCommand command,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(command);

        // Validate tier
        if (!Enum.TryParse<LlmUserTier>(command.Tier, ignoreCase: true, out _))
        {
            throw new DomainException($"Invalid tier: {command.Tier}. Valid tiers are: {string.Join(", ", Enum.GetNames<LlmUserTier>())}");
        }

        // Validate strategy
        if (!RagStrategyExtensions.TryParse(command.Strategy, out var strategy))
        {
            throw new DomainException($"Invalid strategy: {command.Strategy}. Valid strategies are: FAST, BALANCED, PRECISE, EXPERT, CONSENSUS, CUSTOM");
        }

        var strategyName = strategy.GetDisplayName();

        // Find or create entry
        var existingEntry = await _dbContext.Set<TierStrategyAccessEntity>()
            .FirstOrDefaultAsync(
                e => e.Tier == command.Tier && e.Strategy == strategyName,
                cancellationToken)
            .ConfigureAwait(false);

        if (existingEntry != null)
        {
            // Update existing entry
            existingEntry.IsEnabled = command.IsEnabled;
            existingEntry.UpdatedAt = DateTime.UtcNow;

            _logger.LogInformation(
                "Updated tier-strategy access: {Tier}/{Strategy} = {IsEnabled}",
                command.Tier,
                strategyName,
                command.IsEnabled);
        }
        else
        {
            // Create new entry
            var newEntry = new TierStrategyAccessEntity
            {
                Id = Guid.NewGuid(),
                Tier = command.Tier,
                Strategy = strategyName,
                IsEnabled = command.IsEnabled,
                CreatedAt = DateTime.UtcNow,
                UpdatedAt = DateTime.UtcNow
            };

            _dbContext.Set<TierStrategyAccessEntity>().Add(newEntry);
            existingEntry = newEntry;

            _logger.LogInformation(
                "Created tier-strategy access: {Tier}/{Strategy} = {IsEnabled}",
                command.Tier,
                strategyName,
                command.IsEnabled);
        }

        await _unitOfWork.SaveChangesAsync(cancellationToken).ConfigureAwait(false);

        return new TierStrategyAccessDto(
            Id: existingEntry.Id,
            Tier: existingEntry.Tier,
            Strategy: existingEntry.Strategy,
            IsEnabled: existingEntry.IsEnabled,
            IsDefault: false);
    }
}
