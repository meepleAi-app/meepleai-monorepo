/**
 * Query Client Test Wrapper
 * Fix per test pre-esistenti che falliscono per missing QueryClientProvider
 */

import { ReactNode } from 'react';

import { QueryClient, QueryClientProvider } from '@tanstack/react-query';

/**
 * Create a test-specific QueryClient with sensible defaults
 */
export function createTestQueryClient() {
  return new QueryClient({
    defaultOptions: {
      queries: {
        retry: false,
        gcTime: 0,
        staleTime: 0,
      },
      mutations: {
        retry: false,
      },
    },
    logger: {
      log: () => {},
      warn: () => {},
      error: () => {},
    },
  });
}

/**
 * Wrapper component for tests that need QueryClient
 */
export function QueryClientWrapper({ children }: { children: ReactNode }) {
  const queryClient = createTestQueryClient();
  return <QueryClientProvider client={queryClient}>{children}</QueryClientProvider>;
}

/**
 * Hook to use in tests: wrapper option for renderHook
 */
export function withQueryClient() {
  return {
    wrapper: QueryClientWrapper,
  };
}
