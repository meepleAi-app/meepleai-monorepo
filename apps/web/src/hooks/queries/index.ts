/**
 * TanStack Query Hooks - Barrel Export
 *
 * Issue #1079: FE-IMP-003 — TanStack Query Data Layer
 *
 * Centralized export for all TanStack Query hooks and query keys.
 */

// User queries
export { useCurrentUser, userKeys } from './useCurrentUser';

// Games queries
export { useGames, useGame, useGameSessions, useGameDocuments, gamesKeys } from './useGames';

// Chat queries and mutations
export {
  useChats,
  useChatThread,
  useMessages,
  useCreateChat,
  useAddMessage,
  useEditMessage,
  useDeleteMessage,
  useCloseChat,
  useReopenChat,
  chatKeys,
} from './useChats';

// Admin dashboard queries (Issue #886)
export {
  useDashboardData,
  useDashboardAnalytics,
  useDashboardActivity,
  adminKeys,
} from './useDashboardData';

// User Library queries and mutations
export {
  useLibrary,
  useLibraryStats,
  useLibraryQuota,
  useGameInLibraryStatus,
  useAddGameToLibrary,
  useRemoveGameFromLibrary,
  useUpdateLibraryEntry,
  useToggleLibraryFavorite,
  // Library Sharing (Issue #2614)
  useLibraryShareLink,
  useCreateShareLink,
  useUpdateShareLink,
  useRevokeShareLink,
  useSharedLibrary,
  // Dashboard widget (Issue #2612) + Recently Added Games
  useRecentlyAddedGames,
  libraryKeys,
} from './useLibrary';

// Agent Documents queries and mutations (Issue #2399)
export {
  useAgentDocuments,
  useUpdateAgentDocuments,
  agentDocumentsKeys,
} from './useAgentDocuments';

// Shared Games Catalog queries (Issue #2518)
export {
  useSharedGames,
  useSharedGame,
  useGameCategories,
  useGameMechanics,
  sharedGamesKeys,
} from './useSharedGames';

// Agent Configuration queries and mutations (Issue #2518)
export {
  useAgentConfig,
  useUpdateAgentConfig,
  agentConfigKeys,
} from './useAgentConfig';

// Agent Typologies queries and mutations (Issue #3249)
export {
  useApprovedTypologies,
  useTypology,
  useSwitchTypology,
  agentTypologiesKeys,
  type SwitchTypologyRequest,
  type SwitchTypologyResponse,
} from './useAgentTypologies';

// AI Models Management queries and mutations (Issue #2521)
export {
  useAiModels,
  useAiModel,
  useCostTracking,
  useUpdateModelConfig,
  useSetPrimaryModel,
  useTestModel,
  aiModelsKeys,
} from './useAiModels';

// Active Sessions queries and mutations (Issue #2617)
export {
  useActiveSessions,
  useSession,
  usePauseSession,
  useResumeSession,
  useEndSession,
  sessionsKeys,
} from './useActiveSessions';

// Share Requests queries and mutations (Issue #2743)
export {
  useShareRequests,
  useShareRequest,
  useRateLimitStatus,
  useCreateShareRequest,
  shareRequestsKeys,
} from './useShareRequests';

export { useCanShareGame, type CanShareGameResult } from './useCanShareGame';

// Admin Share Requests queries and mutations (Issue #2745)
export {
  useAdminShareRequests,
  useShareRequestDetails,
  useMyReviews,
  useStartReview,
  useReleaseReview,
  useApproveRequest,
  useRejectRequest,
  useRequestChanges,
  adminShareRequestsKeys,
} from './useAdminShareRequests';

// Game Contributors queries (Issue #2746)
export { useGameContributors, gameContributorsKeys } from './useGameContributors';

// Badge & Gamification queries and mutations (Issue #2747)
export {
  useMyBadges,
  useLeaderboard,
  useToggleBadgeDisplay,
  badgeKeys,
} from './useBadges';

// Session Quota queries (Issue #3075)
export {
  useSessionQuota,
  useSessionQuotaWithStatus,
  usePrefetchSessionQuota,
  useInvalidateSessionQuota,
  sessionQuotaKeys,
} from './useSessionQuota';

// Re-export from @tanstack/react-query for convenience
export {
  useQuery,
  useMutation,
  useQueryClient,
  useIsFetching,
  useIsMutating,
} from '@tanstack/react-query';

export type { UseQueryResult, UseMutationResult, QueryClient } from '@tanstack/react-query';
