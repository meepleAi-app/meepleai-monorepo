namespace Api.BoundedContexts.UserNotifications.Application.Constants;

/// <summary>
/// ADR-075 (#2996): single source of truth for notification deep-link paths stored in
/// <c>Notification.Link</c>. Event handlers and jobs must use these constants/builders instead of
/// constructing paths inline, so a route rename is a one-place change (and the compiler flags every
/// usage).
/// <para>
/// The <c>*Template</c> string constants are the canonical values. They MUST stay byte-identical to
/// the twin FE module <c>apps/web/src/lib/constants/notification-routes.ts</c> — the cross-language
/// drift gate <c>scripts/lint-cross-lang-constants.sh</c> (ADR-075) hash-compares the two sets and
/// fails CI on divergence. Parameterized templates use the literal <c>{id}</c> token, substituted by
/// the builder methods (which produce output byte-identical to the previous inline interpolations,
/// since <see cref="Guid.ToString()"/> is the default format used before).
/// </para>
/// </summary>
internal static class NotificationRoutes
{
    // ── Parameterized route templates ({id} token substituted by the builders below) ──
    public const string LibraryAgentTemplate = "/library/games/{id}/agent";
    public const string PrivateToolkitTemplate = "/library/private/{id}/toolkit";
    public const string ContributionRequestTemplate = "/contributions/requests/{id}";
    public const string SharedGameTemplate = "/shared-games/{id}";
    public const string AdminSharedGameTemplate = "/admin/shared-games/{id}";
    public const string AdminApprovalQueueTemplate = "/admin/approval-queue?gameId={id}";
    public const string DocumentTemplate = "/documents/{id}";
    public const string AdminShareRequestTemplate = "/admin/share-requests/{id}";
    public const string AdminMechanicAnalysisReviewTemplate = "/admin/mechanic-analyses/{id}/review";
    public const string GameNightTemplate = "/game-nights/{id}";
    public const string GameTemplate = "/games/{id}";

    // ── Static routes (no parameter) ──
    public const string Dashboard = "/dashboard";
    public const string AccountSuspended = "/account/suspended";
    public const string Contributions = "/contributions";
    public const string ContributionsPending = "/contributions/requests?status=pending";
    public const string AdminKnowledgeBaseQueue = "/admin/knowledge-base/queue";
    public const string AdminMechanicExtractorDashboard = "/admin/knowledge-base/mechanic-extractor/dashboard";
    public const string UserBadges = "/users/me/badges";
    public const string SettingsSubscription = "/settings/subscription";
    public const string AdminAgentsUsage = "/admin/agents/usage";
    public const string AdminAgentsStrategy = "/admin/agents/strategy";
    public const string AdminShareRequestsOldest = "/admin/share-requests?sort=oldest";
    public const string SettingsNotifications = "/settings/notifications";
    public const string Library = "/library";
    public const string Achievements = "/achievements";
    public const string Sessions = "/sessions";

    // ── Builders for parameterized routes ──
    public static string LibraryAgent(Guid id) => LibraryAgentTemplate.Replace("{id}", id.ToString());
    public static string PrivateToolkit(Guid id) => PrivateToolkitTemplate.Replace("{id}", id.ToString());
    public static string ContributionRequest(Guid id) => ContributionRequestTemplate.Replace("{id}", id.ToString());
    public static string SharedGame(Guid id) => SharedGameTemplate.Replace("{id}", id.ToString());
    public static string AdminSharedGame(Guid id) => AdminSharedGameTemplate.Replace("{id}", id.ToString());
    public static string AdminApprovalQueue(Guid gameId) => AdminApprovalQueueTemplate.Replace("{id}", gameId.ToString());
    public static string Document(Guid id) => DocumentTemplate.Replace("{id}", id.ToString());
    public static string AdminShareRequest(Guid id) => AdminShareRequestTemplate.Replace("{id}", id.ToString());
    public static string AdminMechanicAnalysisReview(Guid analysisId) => AdminMechanicAnalysisReviewTemplate.Replace("{id}", analysisId.ToString());
    public static string GameNight(Guid id) => GameNightTemplate.Replace("{id}", id.ToString());
    public static string Game(Guid id) => GameTemplate.Replace("{id}", id.ToString());
}
