/**
 * ADR-075 (#2996): single source of truth (FE side) for notification deep-link paths.
 *
 * The string VALUES in `NotificationRoutes` MUST stay byte-identical to the twin BE class
 * `apps/api/src/Api/BoundedContexts/UserNotifications/Application/Constants/NotificationRoutes.cs`.
 * The cross-language drift gate `scripts/lint-cross-lang-constants.sh` hash-compares the two sets
 * and fails CI on divergence. Parameterized templates use the literal `{id}` token, substituted by
 * the builder functions below.
 *
 * The BE constructs `Notification.Link` today; these FE builders mirror the BE API so a future FE
 * surface that needs to build a deep-link uses the same canonical routes.
 */

export const NotificationRoutes = {
  // Parameterized templates ({id} substituted by the builders below)
  libraryAgentTemplate: '/library/games/{id}/agent',
  privateToolkitTemplate: '/library/private/{id}/toolkit',
  contributionRequestTemplate: '/contributions/requests/{id}',
  sharedGameTemplate: '/shared-games/{id}',
  adminSharedGameTemplate: '/admin/shared-games/{id}',
  adminApprovalQueueTemplate: '/admin/approval-queue?gameId={id}',
  documentTemplate: '/documents/{id}',
  adminShareRequestTemplate: '/admin/share-requests/{id}',
  adminMechanicAnalysisReviewTemplate: '/admin/mechanic-analyses/{id}/review',
  gameNightTemplate: '/game-nights/{id}',
  gameTemplate: '/games/{id}',
  // Static routes (no parameter)
  dashboard: '/dashboard',
  accountSuspended: '/account/suspended',
  contributions: '/contributions',
  contributionsPending: '/contributions/requests?status=pending',
  adminKnowledgeBaseQueue: '/admin/knowledge-base/queue',
  adminMechanicExtractorDashboard: '/admin/knowledge-base/mechanic-extractor/dashboard',
  userBadges: '/users/me/badges',
  settingsSubscription: '/settings/subscription',
  adminAgentsUsage: '/admin/agents/usage',
  adminAgentsStrategy: '/admin/agents/strategy',
  adminShareRequestsOldest: '/admin/share-requests?sort=oldest',
  settingsNotifications: '/settings/notifications',
  library: '/library',
  achievements: '/achievements',
  sessions: '/sessions',
} as const;

const withId = (template: string, id: string): string => template.replace('{id}', id);

export const libraryAgent = (id: string): string =>
  withId(NotificationRoutes.libraryAgentTemplate, id);
export const privateToolkit = (id: string): string =>
  withId(NotificationRoutes.privateToolkitTemplate, id);
export const contributionRequest = (id: string): string =>
  withId(NotificationRoutes.contributionRequestTemplate, id);
export const sharedGame = (id: string): string => withId(NotificationRoutes.sharedGameTemplate, id);
export const adminSharedGame = (id: string): string =>
  withId(NotificationRoutes.adminSharedGameTemplate, id);
export const adminApprovalQueue = (gameId: string): string =>
  withId(NotificationRoutes.adminApprovalQueueTemplate, gameId);
// Named documentRoute (not `document`) to avoid shadowing the DOM global.
export const documentRoute = (id: string): string =>
  withId(NotificationRoutes.documentTemplate, id);
export const adminShareRequest = (id: string): string =>
  withId(NotificationRoutes.adminShareRequestTemplate, id);
export const adminMechanicAnalysisReview = (analysisId: string): string =>
  withId(NotificationRoutes.adminMechanicAnalysisReviewTemplate, analysisId);
export const gameNight = (id: string): string => withId(NotificationRoutes.gameNightTemplate, id);
export const game = (id: string): string => withId(NotificationRoutes.gameTemplate, id);
