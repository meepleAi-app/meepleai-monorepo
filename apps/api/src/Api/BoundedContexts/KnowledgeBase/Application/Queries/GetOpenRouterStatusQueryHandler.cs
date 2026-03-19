using Api.BoundedContexts.KnowledgeBase.Application.DTOs;
using Api.BoundedContexts.KnowledgeBase.Application.Queries;
using Api.BoundedContexts.KnowledgeBase.Domain.Repositories;
using Api.BoundedContexts.KnowledgeBase.Domain.Services;
using MediatR;
using Microsoft.Extensions.Logging;

namespace Api.BoundedContexts.KnowledgeBase.Application.Handlers;

/// <summary>
/// Aggregates OpenRouter account status, rate-limit utilization, and daily request count
/// into a single DTO for the admin usage dashboard.
/// Issue #5077: Admin usage page — KPI cards data source.
/// </summary>
internal sealed class GetOpenRouterStatusQueryHandler
    : IRequestHandler<GetOpenRouterStatusQuery, OpenRouterStatusDto>
{
    private const string OpenRouterProvider = "openrouter";

    private readonly IOpenRouterUsageService _usageService;
    private readonly IOpenRouterRateLimitTracker _rateLimitTracker;
    private readonly ILlmRequestLogRepository _logRepository;
    private readonly ILogger<GetOpenRouterStatusQueryHandler> _logger;

    public GetOpenRouterStatusQueryHandler(
        IOpenRouterUsageService usageService,
        IOpenRouterRateLimitTracker rateLimitTracker,
        ILlmRequestLogRepository logRepository,
        ILogger<GetOpenRouterStatusQueryHandler> logger)
    {
        _usageService = usageService ?? throw new ArgumentNullException(nameof(usageService));
        _rateLimitTracker = rateLimitTracker ?? throw new ArgumentNullException(nameof(rateLimitTracker));
        _logRepository = logRepository ?? throw new ArgumentNullException(nameof(logRepository));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    public async Task<OpenRouterStatusDto> Handle(
        GetOpenRouterStatusQuery request,
        CancellationToken cancellationToken)
    {
        var today = DateOnly.FromDateTime(DateTime.UtcNow);

        // Fan out to three independent data sources in parallel
        var accountStatusTask = _usageService.GetAccountStatusAsync(cancellationToken);
        var dailySpendTask = _usageService.GetDailySpendAsync(cancellationToken);
        var rateLimitTask = _rateLimitTracker.GetCurrentStatusAsync(OpenRouterProvider, cancellationToken);
        var todayCountTask = _logRepository.GetTodayCountAsync(today, cancellationToken);

        await Task.WhenAll(accountStatusTask, dailySpendTask, rateLimitTask, todayCountTask)
            .ConfigureAwait(false);

        var account = await accountStatusTask.ConfigureAwait(false);
        var dailySpend = await dailySpendTask.ConfigureAwait(false);
        var rateLimit = await rateLimitTask.ConfigureAwait(false);
        var todayCount = await todayCountTask.ConfigureAwait(false);

        _logger.LogDebug(
            "OpenRouter status: balance=${Balance}, spend=${Spend}, rpm={Rpm}/{Limit}, today={Count}",
            account?.BalanceUsd, dailySpend, rateLimit.CurrentRpm, rateLimit.LimitRpm, todayCount);

        return new OpenRouterStatusDto(
            BalanceUsd: account?.BalanceUsd ?? 0m,
            DailySpendUsd: dailySpend,
            TodayRequestCount: todayCount,
            CurrentRpm: rateLimit.CurrentRpm,
            LimitRpm: rateLimit.LimitRpm,
            UtilizationPercent: rateLimit.UtilizationPercent,
            IsThrottled: rateLimit.IsThrottled,
            IsFreeTier: account?.IsFreeTier ?? false,
            RateLimitInterval: account?.RateLimitInterval ?? string.Empty,
            LastUpdated: account?.LastUpdated
        );
    }
}
