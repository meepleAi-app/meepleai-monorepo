using Api.BoundedContexts.SystemConfiguration.Application.Commands;
using Api.BoundedContexts.SystemConfiguration.Application.DTOs;
using Api.BoundedContexts.SystemConfiguration.Application.Services;
using Api.BoundedContexts.SystemConfiguration.Domain.Enums;
using Api.BoundedContexts.SystemConfiguration.Domain.Repositories;
using Api.Infrastructure;
using Api.Middleware.Exceptions;
using MediatR;
using Microsoft.Extensions.Logging;

namespace Api.BoundedContexts.SystemConfiguration.Application.Commands;

/// <summary>
/// Handler for UpdateTierRoutingCommand.
/// Issue #2596: LLM tier routing update with cache invalidation.
/// </summary>
internal sealed class UpdateTierRoutingCommandHandler : IRequestHandler<UpdateTierRoutingCommand, TierRoutingDto>
{
    private readonly IAiModelConfigurationRepository _repository;
    private readonly ILlmTierRoutingService _tierRoutingService;
    private readonly MeepleAiDbContext _dbContext;
    private readonly ILogger<UpdateTierRoutingCommandHandler> _logger;

    public UpdateTierRoutingCommandHandler(
        IAiModelConfigurationRepository repository,
        ILlmTierRoutingService tierRoutingService,
        MeepleAiDbContext dbContext,
        ILogger<UpdateTierRoutingCommandHandler> logger)
    {
        _repository = repository;
        _tierRoutingService = tierRoutingService;
        _dbContext = dbContext;
        _logger = logger;
    }

    public async Task<TierRoutingDto> Handle(UpdateTierRoutingCommand request, CancellationToken cancellationToken)
    {
        _logger.LogInformation(
            "Updating tier routing for {Tier}: Production={ProductionModel}, Test={TestModel}",
            request.Tier, request.ProductionModelId, request.TestModelId);

        // Validate that the specified models exist
        var productionModel = await _repository.GetByModelIdAsync(request.ProductionModelId, cancellationToken)
            .ConfigureAwait(false);

        var testModel = await _repository.GetByModelIdAsync(request.TestModelId, cancellationToken)
            .ConfigureAwait(false);

        if (productionModel == null)
        {
            throw new NotFoundException($"Production model '{request.ProductionModelId}' not found");
        }

        if (testModel == null)
        {
            throw new NotFoundException($"Test model '{request.TestModelId}' not found");
        }

        // Get all existing tier routings for this tier
        var existingConfigs = await _repository.GetByTierAsync(request.Tier, cancellationToken: cancellationToken)
            .ConfigureAwait(false);

        // Update production model routing
        await UpdateTierRoutingForEnvironmentAsync(
            request.Tier,
            LlmEnvironmentType.Production,
            productionModel,
            existingConfigs,
            cancellationToken).ConfigureAwait(false);

        // Update test model routing
        await UpdateTierRoutingForEnvironmentAsync(
            request.Tier,
            LlmEnvironmentType.Test,
            testModel,
            existingConfigs,
            cancellationToken).ConfigureAwait(false);

        // Save changes
        await _dbContext.SaveChangesAsync(cancellationToken).ConfigureAwait(false);

        // Invalidate cache
        await _tierRoutingService.InvalidateCacheAsync(cancellationToken).ConfigureAwait(false);

        _logger.LogInformation(
            "Successfully updated tier routing for {Tier}",
            request.Tier);

        return new TierRoutingDto
        {
            Tier = request.Tier,
            TierName = request.Tier.ToString(),
            ProductionModelId = productionModel.ModelId,
            ProductionModelName = productionModel.DisplayName,
            ProductionProvider = productionModel.Provider,
            TestModelId = testModel.ModelId,
            TestModelName = testModel.DisplayName,
            TestProvider = testModel.Provider,
            EstimatedMonthlyCostUsd = 0 // Reset on update
        };
    }

    private async Task UpdateTierRoutingForEnvironmentAsync(
        LlmUserTier tier,
        LlmEnvironmentType environment,
        Domain.Entities.AiModelConfiguration newModel,
        IReadOnlyList<Domain.Entities.AiModelConfiguration> existingConfigs,
        CancellationToken cancellationToken)
    {
        // Find existing default for this tier/environment
        var existingDefault = existingConfigs.FirstOrDefault(c =>
            c.EnvironmentType == environment && c.IsDefaultForTier);

        if (existingDefault != null && existingDefault.Id != newModel.Id)
        {
            // Remove default flag from old model
            existingDefault.SetAsDefaultForTier(false);
            await _repository.UpdateAsync(existingDefault, cancellationToken).ConfigureAwait(false);
        }

        // Set new model as default for this tier/environment
        newModel.SetTierRouting(tier, environment, true);
        await _repository.UpdateAsync(newModel, cancellationToken).ConfigureAwait(false);
    }
}
