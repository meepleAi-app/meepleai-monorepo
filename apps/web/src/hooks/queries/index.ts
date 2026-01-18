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

// Re-export from @tanstack/react-query for convenience
export {
  useQuery,
  useMutation,
  useQueryClient,
  useIsFetching,
  useIsMutating,
} from '@tanstack/react-query';

export type { UseQueryResult, UseMutationResult, QueryClient } from '@tanstack/react-query';
