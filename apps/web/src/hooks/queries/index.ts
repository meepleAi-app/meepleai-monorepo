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
  useGameInLibraryStatus,
  useAddGameToLibrary,
  useRemoveGameFromLibrary,
  useUpdateLibraryEntry,
  useToggleLibraryFavorite,
  libraryKeys,
} from './useLibrary';

// Re-export from @tanstack/react-query for convenience
export {
  useQuery,
  useMutation,
  useQueryClient,
  useIsFetching,
  useIsMutating,
} from '@tanstack/react-query';

export type { UseQueryResult, UseMutationResult, QueryClient } from '@tanstack/react-query';
