namespace Api.BoundedContexts.KbQuality.Application.Behaviors;

/// <summary>
/// Keys for surfacing per-request KbQuality eval metadata through
/// <c>HttpContext.Items</c>. The pipeline behaviors compute these values
/// (cost cap, sliding rate limit) as side-effects of their pre/post checks;
/// the endpoint then reads them to populate the <c>EvaluationStartedResult</c>
/// fields and the standard HTTP quota headers
/// (<c>X-RateLimit-Remaining</c>, <c>X-RateLimit-Reset</c>, <c>X-Cost-Cap-Remaining</c>).
///
/// <para>This is the same project-standard pattern used by <c>KbQualityCurrentUser</c>
/// (reading <c>SessionStatusDto</c> from <c>HttpContext.Items</c>) — keeps the
/// behavior contract loose without introducing a scoped DI carrier object.</para>
/// </summary>
internal static class KbQualityHttpItemKeys
{
    public const string RateLimitRemaining = "KbQuality.RateLimitRemaining";
    public const string RateLimitReset = "KbQuality.RateLimitReset";
    public const string CostCapRemaining = "KbQuality.CostCapRemaining";
    public const string CostCapEstimate = "KbQuality.CostCapEstimate";

    /// <summary>
    /// Set by <c>StartEvaluationCommandHandler</c> AFTER MarkCompleted/MarkFailed with the
    /// run's final <c>CostUsd</c>. Read by <c>EvalCostCapBehavior</c> at the end of the
    /// pipeline so the budget charge reflects actual spend instead of the pessimistic
    /// upfront estimate. Falls back to the estimate when unset (e.g., exception escaped
    /// the handler before MarkFailed could record a cost).
    /// </summary>
    public const string ActualCostUsd = "KbQuality.ActualCostUsd";
}
