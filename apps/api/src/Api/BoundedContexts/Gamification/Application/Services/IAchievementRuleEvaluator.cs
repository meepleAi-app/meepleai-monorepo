namespace Api.BoundedContexts.Gamification.Application.Services;

/// <summary>
/// Evaluates achievement rules for a user and returns progress (0-100).
/// Issue #3922: Achievement System and Badge Engine.
/// </summary>
internal interface IAchievementRuleEvaluator
{
    /// <summary>
    /// Evaluates the progress for a specific achievement code and user.
    /// Returns 0-100 representing percentage completion.
    /// </summary>
    Task<int> EvaluateProgressAsync(
        string achievementCode,
        int threshold,
        Guid userId,
        CancellationToken cancellationToken = default);
}
