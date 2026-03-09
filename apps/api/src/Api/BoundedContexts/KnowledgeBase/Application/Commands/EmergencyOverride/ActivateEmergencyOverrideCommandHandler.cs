using Api.BoundedContexts.KnowledgeBase.Application.Services;
using Api.BoundedContexts.KnowledgeBase.Domain.Services;
using Api.Services;
using Api.SharedKernel.Application.Interfaces;

namespace Api.BoundedContexts.KnowledgeBase.Application.Commands.EmergencyOverride;

/// <summary>
/// Issue #5476: Handles emergency override activation.
/// Delegates to EmergencyOverrideService for Redis storage and audit logging.
/// Special actions (reset-circuit-breaker, flush-quota-cache) perform immediate side effects.
/// Issue #5487: Uses ICircuitBreakerRegistry directly instead of casting ILlmService.
/// </summary>
internal class ActivateEmergencyOverrideCommandHandler
    : ICommandHandler<ActivateEmergencyOverrideCommand, EmergencyOverrideResult>
{
    private readonly IEmergencyOverrideService _overrideService;
    private readonly ICircuitBreakerRegistry _circuitBreakerRegistry;
    private readonly IFreeModelQuotaTracker? _quotaTracker;
    private readonly ILogger<ActivateEmergencyOverrideCommandHandler> _logger;

    public ActivateEmergencyOverrideCommandHandler(
        IEmergencyOverrideService overrideService,
        ICircuitBreakerRegistry circuitBreakerRegistry,
        ILogger<ActivateEmergencyOverrideCommandHandler> logger,
        IFreeModelQuotaTracker? quotaTracker = null)
    {
        _overrideService = overrideService;
        _circuitBreakerRegistry = circuitBreakerRegistry;
        _logger = logger;
        _quotaTracker = quotaTracker;
    }

    public async Task<EmergencyOverrideResult> Handle(
        ActivateEmergencyOverrideCommand request,
        CancellationToken cancellationToken)
    {
        // Activate the override in Redis (with TTL for auto-revert)
        await _overrideService.ActivateOverrideAsync(
            request.Action,
            request.DurationMinutes,
            request.Reason,
            request.AdminUserId,
            request.TargetProvider,
            cancellationToken).ConfigureAwait(false);

        // Perform immediate side effects for specific actions
        switch (request.Action)
        {
            case "reset-circuit-breaker":
                _circuitBreakerRegistry.ResetCircuitBreaker(request.TargetProvider);
                _logger.LogWarning(
                    "Circuit breaker reset for provider {Provider} by admin {AdminId}",
                    request.TargetProvider ?? "ALL", request.AdminUserId);
                break;

            case "flush-quota-cache":
                if (_quotaTracker != null)
                {
                    await _quotaTracker.FlushCacheAsync(cancellationToken).ConfigureAwait(false);
                    _logger.LogWarning(
                        "Free model quota cache flushed by admin {AdminId}", request.AdminUserId);
                }
                break;
        }

        return new EmergencyOverrideResult(
            Success: true,
            Message: $"Emergency override '{request.Action}' activated for {request.DurationMinutes} minutes");
    }
}

/// <summary>
/// Issue #5476: Handles emergency override deactivation (early manual revert).
/// </summary>
internal class DeactivateEmergencyOverrideCommandHandler
    : ICommandHandler<DeactivateEmergencyOverrideCommand, EmergencyOverrideResult>
{
    private readonly IEmergencyOverrideService _overrideService;

    public DeactivateEmergencyOverrideCommandHandler(IEmergencyOverrideService overrideService)
    {
        _overrideService = overrideService;
    }

    public async Task<EmergencyOverrideResult> Handle(
        DeactivateEmergencyOverrideCommand request,
        CancellationToken cancellationToken)
    {
        await _overrideService.DeactivateOverrideAsync(
            request.Action, request.AdminUserId, cancellationToken).ConfigureAwait(false);

        return new EmergencyOverrideResult(
            Success: true,
            Message: $"Emergency override '{request.Action}' deactivated");
    }
}
