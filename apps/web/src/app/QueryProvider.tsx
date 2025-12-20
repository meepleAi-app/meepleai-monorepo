/**
 * QueryProvider - TanStack Query setup for Next.js 16 App Router
 *
 * Issue #1079: FE-IMP-003 — TanStack Query Data Layer
 * Issue #994: Frontend Build Optimization (BGAI-054)
 *
 * Wraps app with QueryClientProvider and React Query DevTools.
 * Must be a Client Component ("use client") for React Query.
 */

'use client';

import { ReactNode, lazy, Suspense } from 'react';

import { QueryClientProvider } from '@tanstack/react-query';

import { getQueryClient } from '@/lib/queryClient';

// Lazy load DevTools to exclude from production bundle (~820KB savings)
// Issue #994: Dynamic import ensures tree-shaking in production builds
const ReactQueryDevtools = lazy(() =>
  import('@tanstack/react-query-devtools').then(mod => ({
    default: mod.ReactQueryDevtools,
  }))
);

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
 * - Includes DevTools in development mode (lazy loaded)
 * - Singleton client on browser, new client per request on server
 */
export function QueryProvider({ children }: QueryProviderProps) {
  // Get singleton QueryClient (browser) or new client (server)
  const queryClient = getQueryClient();

  return (
    <QueryClientProvider client={queryClient}>
      {children}
      {/* DevTools lazy loaded only in development */}
      {process.env.NODE_ENV === 'development' && (
        <Suspense fallback={null}>
          <ReactQueryDevtools initialIsOpen={false} position="bottom" />
        </Suspense>
      )}
    </QueryClientProvider>
  );
}
