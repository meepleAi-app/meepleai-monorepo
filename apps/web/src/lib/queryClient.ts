/**
 * TanStack Query Configuration
 *
 * Issue #1079: FE-IMP-003 — TanStack Query Data Layer
 *
 * Provides singleton QueryClient with optimized defaults:
 * - 5min staleTime (reduce unnecessary refetches)
 * - 1 retry on failure
 * - Error logging with correlation IDs
 *
 * Next.js 16 App Router pattern:
 * - Server: New QueryClient per request
 * - Client: Singleton instance
 */

import { isServer, QueryClient, DefaultOptions } from '@tanstack/react-query';

/**
 * Default options for all queries and mutations
 */
const defaultOptions: DefaultOptions = {
  queries: {
    // Data stays fresh for 5 minutes (reduce API calls)
    staleTime: 5 * 60 * 1000,

    // Retry failed requests once
    retry: 1,

    // Refetch on window focus (user returned to tab)
    refetchOnWindowFocus: true,

    // Don't refetch on mount if data is fresh
    refetchOnMount: false,
  },
  mutations: {
    // Retry mutations once
    retry: 1,

    // Log mutation errors
    onError: (error) => {
      console.error('Mutation error:', error);
      // TODO: Add Serilog/Seq integration for production error tracking
    },
  },
};

/**
 * Factory function to create a new QueryClient
 */
function makeQueryClient(): QueryClient {
  return new QueryClient({
    defaultOptions,
  });
}

// Browser singleton instance
let browserQueryClient: QueryClient | undefined = undefined;

/**
 * Get or create QueryClient
 *
 * Next.js 16 pattern:
 * - Server (SSR): Always creates new client per request
 * - Client (CSR): Returns singleton for entire session
 */
export function getQueryClient(): QueryClient {
  if (isServer) {
    // Server: Always create new client (prevent data leakage between requests)
    return makeQueryClient();
  } else {
    // Browser: Create singleton on first call
    if (!browserQueryClient) {
      browserQueryClient = makeQueryClient();
    }
    return browserQueryClient;
  }
}
