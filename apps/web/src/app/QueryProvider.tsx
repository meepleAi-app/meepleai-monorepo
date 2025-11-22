/**
 * QueryProvider - TanStack Query setup for Next.js 16 App Router
 *
 * Issue #1079: FE-IMP-003 — TanStack Query Data Layer
 *
 * Wraps app with QueryClientProvider and React Query DevTools.
 * Must be a Client Component ("use client") for React Query.
 */

'use client';

import { QueryClientProvider } from '@tanstack/react-query';
import { ReactQueryDevtools } from '@tanstack/react-query-devtools';
import { getQueryClient } from '@/lib/queryClient';
import { ReactNode } from 'react';

interface QueryProviderProps {
  children: ReactNode;
}

/**
 * Provider component for TanStack Query
 *
 * Usage: Wrap app tree in _app.tsx or layout.tsx
 *
 * Features:
 * - Provides QueryClient to all child components
 * - Includes DevTools in development mode
 * - Singleton client on browser, new client per request on server
 */
export function QueryProvider({ children }: QueryProviderProps) {
  // Get singleton QueryClient (browser) or new client (server)
  const queryClient = getQueryClient();

  return (
    <QueryClientProvider client={queryClient}>
      {children}
      {/* DevTools only in development */}
      {process.env.NODE_ENV === 'development' && (
        <ReactQueryDevtools initialIsOpen={false} position="bottom" />
      )}
    </QueryClientProvider>
  );
}
