using Api.BoundedContexts.Administration.Application.Commands.TierStrategy;
using Api.BoundedContexts.Administration.Application.DTOs;
using Api.BoundedContexts.KnowledgeBase.Domain.Enums;
using Api.Infrastructure;
using Api.Infrastructure.Entities.KnowledgeBase;
using Api.SharedKernel.Application.Interfaces;
using Api.SharedKernel.Domain.Exceptions;
using Api.SharedKernel.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace Api.BoundedContexts.Administration.Application.Commands.TierStrategy;

/// <summary>
/// Handler for UpdateStrategyModelMappingCommand.
/// Updates the model mapping for a strategy.
/// Issue #3440: Admin UI for tier-strategy configuration.
/// </summary>
internal class UpdateStrategyModelMappingCommandHandler
    : ICommandHandler<UpdateStrategyModelMappingCommand, StrategyModelMappingDto>
{
    private readonly MeepleAiDbContext _dbContext;
    private readonly IUnitOfWork _unitOfWork;
    private readonly ILogger<UpdateStrategyModelMappingCommandHandler> _logger;

    public UpdateStrategyModelMappingCommandHandler(
        MeepleAiDbContext dbContext,
        IUnitOfWork unitOfWork,
        ILogger<UpdateStrategyModelMappingCommandHandler> logger)
    {
        _dbContext = dbContext ?? throw new ArgumentNullException(nameof(dbContext));
        _unitOfWork = unitOfWork ?? throw new ArgumentNullException(nameof(unitOfWork));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    public async Task<StrategyModelMappingDto> Handle(
        UpdateStrategyModelMappingCommand command,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(command);

        // Validate strategy
        if (!RagStrategyExtensions.TryParse(command.Strategy, out var strategy))
        {
            throw new DomainException($"Invalid strategy: {command.Strategy}. Valid strategies are: FAST, BALANCED, PRECISE, EXPERT, CONSENSUS, CUSTOM");
        }

        var strategyName = strategy.GetDisplayName();

        // Validate provider
        if (string.IsNullOrWhiteSpace(command.Provider))
        {
            throw new DomainException("Provider cannot be empty");
        }

        // Validate primary model
        if (string.IsNullOrWhiteSpace(command.PrimaryModel))
        {
            throw new DomainException("Primary model cannot be empty");
        }

        // Find or create entry
        var existingEntry = await _dbContext.Set<StrategyModelMappingEntity>()
            .FirstOrDefaultAsync(
                e => e.Strategy == strategyName,
                cancellationToken)
            .ConfigureAwait(false);

        var fallbackModels = command.FallbackModels?.ToArray() ?? Array.Empty<string>();

        if (existingEntry != null)
        {
            // Update existing entry
            existingEntry.Provider = command.Provider;
            existingEntry.PrimaryModel = command.PrimaryModel;
            existingEntry.FallbackModels = fallbackModels;
            existingEntry.UpdatedAt = DateTime.UtcNow;

            _logger.LogInformation(
                "Updated strategy-model mapping: {Strategy} -> {Provider}/{Model}",
                strategyName,
                command.Provider,
                command.PrimaryModel);
        }
        else
        {
            // Create new entry
            var newEntry = new StrategyModelMappingEntity
            {
                Id = Guid.NewGuid(),
                Strategy = strategyName,
                Provider = command.Provider,
                PrimaryModel = command.PrimaryModel,
                FallbackModels = fallbackModels,
                IsCustomizable = true,
                AdminOnly = strategy.RequiresAdmin(),
                CreatedAt = DateTime.UtcNow,
                UpdatedAt = DateTime.UtcNow
            };

            _dbContext.Set<StrategyModelMappingEntity>().Add(newEntry);
            existingEntry = newEntry;

            _logger.LogInformation(
                "Created strategy-model mapping: {Strategy} -> {Provider}/{Model}",
                strategyName,
                command.Provider,
                command.PrimaryModel);
        }

        await _unitOfWork.SaveChangesAsync(cancellationToken).ConfigureAwait(false);

        return new StrategyModelMappingDto(
            Id: existingEntry.Id,
            Strategy: existingEntry.Strategy,
            Provider: existingEntry.Provider,
            PrimaryModel: existingEntry.PrimaryModel,
            FallbackModels: existingEntry.FallbackModels,
            IsCustomizable: existingEntry.IsCustomizable,
            AdminOnly: existingEntry.AdminOnly,
            IsDefault: false);
    }
}
