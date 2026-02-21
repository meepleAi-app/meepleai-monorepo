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

// Admin dashboard queries (Issue #886)
export {
  useDashboardData,
  useDashboardAnalytics,
  useDashboardActivity,
  useAppUsageStats,
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

// Tier Strategy Configuration queries and mutations (Issue #3440)
export {
  useTierStrategyMatrix,
  useStrategyModelMappings,
  useUpdateTierStrategyAccess,
  useUpdateStrategyModelMapping,
  useResetTierStrategyConfig,
  tierStrategyKeys,
} from './useTierStrategy';

// Game Carousel queries (Issue #3586, #3587 - Epic #3585)
export {
  useCarouselGames,
  useFeaturedGames,
  useTrendingGames,
  useCategoryGames,
  useUserLibraryGames,
  carouselGamesKeys,
  type CarouselSource,
  type UseCarouselGamesOptions,
  type CarouselGamesResult,
} from './useCarouselGames';

// Game Carousel sort types (Issue #3587)
export type {
  CarouselSortValue,
  CarouselSortOption,
} from '@/components/ui/data-display/game-carousel';
export { CAROUSEL_SORT_OPTIONS } from '@/components/ui/data-display/game-carousel';

// Agent Slots & Creation Flow (Issue #4771, #4772, #4773)
export {
  useAgentSlots,
  useHasAvailableSlots,
  useInvalidateAgentSlots,
  agentSlotsKeys,
  type AgentSlot,
  type AgentSlotsData,
} from './useAgentSlots';

export {
  useCreateAgentFlow,
  type CreateAgentFlowInput,
  type CreateAgentFlowResult,
} from './useCreateAgentFlow';

// PDF Processing Status (Issue #4946)
export { usePdfProcessingStatus, pdfStatusKeys } from './usePdfProcessingStatus';

// Re-export from @tanstack/react-query for convenience
export {
  useQuery,
  useMutation,
  useQueryClient,
  useIsFetching,
  useIsMutating,
} from '@tanstack/react-query';

export type { UseQueryResult, UseMutationResult, QueryClient } from '@tanstack/react-query';
