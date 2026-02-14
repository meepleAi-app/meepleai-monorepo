using Api.BoundedContexts.Administration.Application.DTOs;
using Api.BoundedContexts.Administration.Domain.Services;
using Api.BoundedContexts.Administration.Domain.ValueObjects;
using DomainInsightType = Api.BoundedContexts.Administration.Domain.ValueObjects.InsightType;
using DtoInsightType = Api.BoundedContexts.Administration.Application.DTOs.InsightType;

namespace Api.BoundedContexts.Administration.Application.Services;

/// <summary>
/// Service for generating AI-powered insights for dashboard.
/// Issue #4308: RAG-based recommendations with Qdrant, backlog detection, rules reminders, streak nudges.
/// Upgraded from rule-based (Issue #3916) to full RAG integration with domain analyzers.
/// </summary>
internal class AiInsightsService : IAiInsightsService
{
    private readonly IUserInsightsService _userInsightsService;
    private readonly ILogger<AiInsightsService> _logger;

    public AiInsightsService(
        IUserInsightsService userInsightsService,
        ILogger<AiInsightsService> logger)
    {
        _userInsightsService = userInsightsService ?? throw new ArgumentNullException(nameof(userInsightsService));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    public async Task<AiInsightsDto> GetInsightsAsync(Guid userId, CancellationToken cancellationToken = default)
    {
        try
        {
            // Delegate to domain service (runs 4 analyzers in parallel)
            var domainInsights = await _userInsightsService.GenerateInsightsAsync(userId, cancellationToken)
                .ConfigureAwait(false);

            // Map domain AIInsight to application DashboardInsightDto
            var dtoInsights = domainInsights.Select(MapToDto).ToList();

            _logger.LogInformation(
                "Generated {Count} AI insights for user {UserId}",
                dtoInsights.Count,
                userId);

            return new AiInsightsDto(
                dtoInsights.AsReadOnly(),
                DateTime.UtcNow,
                DateTime.UtcNow.AddMinutes(60)); // Cache for 1 hour
        }
        catch (Exception ex)
        {
            _logger.LogError(
                ex,
                "Error generating AI insights for user {UserId}",
                userId);

            // Return empty insights on error (graceful degradation)
            return new AiInsightsDto(
                Array.Empty<DashboardInsightDto>(),
                DateTime.UtcNow,
                DateTime.UtcNow.AddMinutes(60));
        }
    }

    /// <summary>
    /// Maps domain AIInsight to application DashboardInsightDto.
    /// </summary>
    private static DashboardInsightDto MapToDto(AIInsight domainInsight)
    {
        // Map InsightType enum from domain to application
        var dtoType = domainInsight.Type switch
        {
            DomainInsightType.BacklogAlert => DtoInsightType.Backlog,
            DomainInsightType.RulesReminder => DtoInsightType.RulesReminder,
            DomainInsightType.Recommendation => DtoInsightType.Recommendation,
            DomainInsightType.StreakNudge => DtoInsightType.Streak,
            _ => DtoInsightType.Recommendation
        };

        // Map icon based on type
        var icon = domainInsight.Type switch
        {
            DomainInsightType.BacklogAlert => "⏰",
            DomainInsightType.RulesReminder => "📖",
            DomainInsightType.Recommendation => "🎲",
            DomainInsightType.StreakNudge => "🔥",
            _ => "💡"
        };

        return new DashboardInsightDto(
            Id: domainInsight.Id,
            Type: dtoType,
            Icon: icon,
            Title: domainInsight.Title,
            Description: domainInsight.Description,
            ActionUrl: domainInsight.ActionUrl,
            ActionLabel: domainInsight.ActionLabel,
            Priority: domainInsight.Priority,
            Metadata: new Dictionary<string, object>(StringComparer.Ordinal)
            {
                ["createdAt"] = domainInsight.CreatedAt.ToString("O"),
                ["source"] = "rag-enhanced" // Indicates RAG vs rule-based
            }
        );
    }

}

/// <summary>
/// DTO for AI insights response (alias for DashboardInsightsResponseDto).
/// </summary>
internal record AiInsightsDto(
    IReadOnlyList<DashboardInsightDto> Insights,
    DateTime GeneratedAt,
    DateTime NextRefresh);
