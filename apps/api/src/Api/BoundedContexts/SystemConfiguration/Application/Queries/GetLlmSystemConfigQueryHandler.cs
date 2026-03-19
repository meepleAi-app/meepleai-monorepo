using Api.BoundedContexts.SystemConfiguration.Application.Queries;
using Api.BoundedContexts.SystemConfiguration.Domain.Repositories;
using Api.Configuration;
using Api.SharedKernel.Application.Interfaces;
using Microsoft.Extensions.Options;

namespace Api.BoundedContexts.SystemConfiguration.Application.Queries;

/// <summary>
/// Issue #5495: Handler for GetLlmSystemConfigQuery.
/// Returns DB config if present, otherwise appsettings defaults with "appsettings" source marker.
/// </summary>
internal sealed class GetLlmSystemConfigQueryHandler : IQueryHandler<GetLlmSystemConfigQuery, LlmSystemConfigDto>
{
    private readonly ILlmSystemConfigRepository _repository;
    private readonly IOptions<AiProviderSettings> _aiSettings;

    public GetLlmSystemConfigQueryHandler(
        ILlmSystemConfigRepository repository,
        IOptions<AiProviderSettings> aiSettings)
    {
        _repository = repository;
        _aiSettings = aiSettings;
    }

    public async Task<LlmSystemConfigDto> Handle(GetLlmSystemConfigQuery request, CancellationToken cancellationToken)
    {
        var config = await _repository.GetCurrentAsync(cancellationToken).ConfigureAwait(false);

        if (config != null)
        {
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

        // No DB config — return appsettings defaults
        var cb = _aiSettings.Value.CircuitBreaker;
        return new LlmSystemConfigDto(
            cb.FailureThreshold,
            cb.OpenDurationSeconds,
            cb.SuccessThreshold,
            DailyBudgetUsd: 10.00m,
            MonthlyBudgetUsd: 100.00m,
            FallbackChainJson: "[]",
            Source: "appsettings",
            LastUpdatedAt: null,
            LastUpdatedByUserId: null);
    }
}
