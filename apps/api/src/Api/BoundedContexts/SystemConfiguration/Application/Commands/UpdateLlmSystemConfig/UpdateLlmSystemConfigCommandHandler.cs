using Api.BoundedContexts.SystemConfiguration.Application.Queries;
using Api.BoundedContexts.SystemConfiguration.Application.Services;
using Api.BoundedContexts.SystemConfiguration.Domain.Entities;
using Api.BoundedContexts.SystemConfiguration.Domain.Repositories;
using Api.SharedKernel.Application.Interfaces;

namespace Api.BoundedContexts.SystemConfiguration.Application.Commands.UpdateLlmSystemConfig;

/// <summary>
/// Issue #5495: Handler for UpdateLlmSystemConfigCommand.
/// Upserts config in DB and invalidates the provider cache.
/// </summary>
internal sealed class UpdateLlmSystemConfigCommandHandler
    : ICommandHandler<UpdateLlmSystemConfigCommand, LlmSystemConfigDto>
{
    private readonly ILlmSystemConfigRepository _repository;
    private readonly ILlmSystemConfigProvider _configProvider;
    private readonly ILogger<UpdateLlmSystemConfigCommandHandler> _logger;

    public UpdateLlmSystemConfigCommandHandler(
        ILlmSystemConfigRepository repository,
        ILlmSystemConfigProvider configProvider,
        ILogger<UpdateLlmSystemConfigCommandHandler> logger)
    {
        _repository = repository;
        _configProvider = configProvider;
        _logger = logger;
    }

    public async Task<LlmSystemConfigDto> Handle(UpdateLlmSystemConfigCommand request, CancellationToken cancellationToken)
    {
        // Get existing or create default
        var config = await _repository.GetCurrentAsync(cancellationToken).ConfigureAwait(false)
                     ?? LlmSystemConfig.CreateDefault();

        // Apply updates via domain methods (validation happens here)
        config.UpdateCircuitBreakerSettings(
            request.CircuitBreakerFailureThreshold,
            request.CircuitBreakerOpenDurationSeconds,
            request.CircuitBreakerSuccessThreshold,
            request.UpdatedByUserId);

        config.UpdateBudgetLimits(
            request.DailyBudgetUsd,
            request.MonthlyBudgetUsd,
            request.UpdatedByUserId);

        config.UpdateFallbackChain(request.FallbackChainJson, request.UpdatedByUserId);

        await _repository.UpsertAsync(config, cancellationToken).ConfigureAwait(false);

        // Invalidate cache so changes take effect immediately
        _configProvider.InvalidateCache();

        _logger.LogInformation(
            "LLM system config updated by user {UserId} (CB: {Failure}/{Open}s/{Success}, Budget: ${Daily}/{Monthly})",
            request.UpdatedByUserId, request.CircuitBreakerFailureThreshold,
            request.CircuitBreakerOpenDurationSeconds, request.CircuitBreakerSuccessThreshold,
            request.DailyBudgetUsd, request.MonthlyBudgetUsd);

        return new LlmSystemConfigDto(
            config.CircuitBreakerFailureThreshold,
            config.CircuitBreakerOpenDurationSeconds,
            config.CircuitBreakerSuccessThreshold,
            config.DailyBudgetUsd,
            config.MonthlyBudgetUsd,
            config.FallbackChainJson,
            Source: "database",
            LastUpdatedAt: config.UpdatedAt,
            LastUpdatedByUserId: config.UpdatedByUserId);
    }
}
