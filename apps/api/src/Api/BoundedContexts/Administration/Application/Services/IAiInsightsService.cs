using Api.BoundedContexts.Administration.Application.DTOs;

namespace Api.BoundedContexts.Administration.Application.Services;

/// <summary>
/// Service for generating AI-powered insights for dashboard.
/// Issue #3916: RAG-based recommendations, backlog detection, rules reminders.
/// </summary>
internal interface IAiInsightsService
{
    /// <summary>
    /// Gets personalized AI insights for a user including:
    /// - Game recommendations (RAG-based or rule-based fallback)
    /// - Backlog alerts (games not played for 30+ days)
    /// - Rules reminders (saved chats with rules topics)
    /// </summary>
    /// <param name="userId">The user ID</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>AI insights DTO with recommendations, alerts, and reminders</returns>
    Task<AiInsightsDto> GetInsightsAsync(Guid userId, CancellationToken cancellationToken = default);
}
