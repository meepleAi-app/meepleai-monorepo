using Api.BoundedContexts.KnowledgeBase.Application.DTOs;
using Api.BoundedContexts.KnowledgeBase.Application.Queries;
using Api.BoundedContexts.KnowledgeBase.Domain.Repositories;
using MediatR;

namespace Api.BoundedContexts.KnowledgeBase.Application.Queries;

/// <summary>
/// Returns today's usage for each free OpenRouter model against a hardcoded daily limit.
/// OpenRouter free models are typically capped at 50 requests per day per account.
/// Issue #5082: Admin usage page — free tier quota indicator.
/// </summary>
internal sealed class GetUsageFreeQuotaQueryHandler
    : IRequestHandler<GetUsageFreeQuotaQuery, FreeQuotaDto>
{
    /// <summary>
    /// Default daily request limit for free OpenRouter models.
    /// Issue #5087: OpenRouter free models with $10+ credits have 1000 RPD limit.
    /// </summary>
    private const int DefaultDailyLimit = 1000;

    private readonly ILlmRequestLogRepository _repository;

    public GetUsageFreeQuotaQueryHandler(ILlmRequestLogRepository repository)
    {
        _repository = repository ?? throw new ArgumentNullException(nameof(repository));
    }

    public async Task<FreeQuotaDto> Handle(
        GetUsageFreeQuotaQuery request,
        CancellationToken cancellationToken)
    {
        var today = DateOnly.FromDateTime(DateTime.UtcNow);
        var freeModelUsage = await _repository
            .GetFreeModelUsageAsync(today, cancellationToken)
            .ConfigureAwait(false);

        // Next UTC midnight = reset time for all free model quotas
        var nextResetUtc = DateTime.UtcNow.Date.AddDays(1);

        var models = freeModelUsage
            .Select(x =>
            {
                var percent = DefaultDailyLimit > 0
                    ? Math.Min((double)x.RequestsToday / DefaultDailyLimit, 1.0)
                    : 0.0;

                return new FreeModelUsageDto(
                    ModelId: x.ModelId,
                    RequestsToday: x.RequestsToday,
                    DailyLimit: DefaultDailyLimit,
                    PercentUsed: percent,
                    IsExhausted: x.RequestsToday >= DefaultDailyLimit,
                    NextResetUtc: nextResetUtc
                );
            })
            .OrderByDescending(m => m.PercentUsed)
            .ToList();

        return new FreeQuotaDto(
            Models: models,
            TotalFreeRequestsToday: freeModelUsage.Sum(x => x.RequestsToday),
            GeneratedAt: DateTime.UtcNow
        );
    }
}
