/**
 * TanStack Query Test Utilities
 *
 * Issue #1079: FE-IMP-003 — TanStack Query Data Layer
 *
 * Provides test wrappers and utilities for testing components that use
 * TanStack Query hooks.
 */

import { render, RenderOptions } from '@testing-library/react';
import { QueryClient, QueryClientProvider } from '@tanstack/react-query';
import { IntlProvider } from 'react-intl';
import { ReactElement, ReactNode } from 'react';

import { flattenMessages, getMessages, DEFAULT_LOCALE } from '@/locales';

// Pre-flatten Italian messages once for all tests in this wrapper
export const TEST_MESSAGES = flattenMessages(getMessages(DEFAULT_LOCALE) as Record<string, unknown>);

/**
 * Translation helper for use in tests.
 *
 * Resolves a flat message key against the active locale messages and
 * performs simple `{variable}` substitution.
 *
 * ⚠️ ICU plural syntax (e.g. `{count, plural, =1 {...} other {...}}`) is NOT
 * evaluated — the raw pattern is returned with `{count}` replaced literally.
 * For plural keys, assert against the rendered DOM using a regex pattern
 * (e.g. `/3 giochi/`) so the real IntlProvider inside renderWithQuery resolves them.
 *
 * Usage:
 * ```ts
 * import { t } from '@/__tests__/utils/query-test-utils';
 *
 * expect(screen.getByText(t('privateGames.title'))).toBeInTheDocument();
 * expect(screen.getByText(t('privateGames.pageOf', { page: 1, totalPages: 3 }))).toBeInTheDocument();
 * ```
 */
export function t(key: string, params?: Record<string, string | number>): string {
  let msg = (TEST_MESSAGES[key] as string) ?? key;
  if (params) {
    Object.entries(params).forEach(([k, v]) => {
      msg = msg.replace(new RegExp(`\\{${k}\\}`, 'g'), String(v));
    });
  }
  return msg;
}

/**
 * Create a new QueryClient for testing
 *
 * Configured with:
 * - No retries (fail fast in tests)
 * - Silent logger (no console spam)
 * - Fresh cache for each test
 */
export function createTestQueryClient(): QueryClient {
  return new QueryClient({
    defaultOptions: {
      queries: {
        // Disable retries in tests (fail fast)
        retry: false,
        // Don't cache data between tests
        staleTime: 0,
        // Disable automatic refetching
        refetchOnMount: false,
        refetchOnWindowFocus: false,
        refetchOnReconnect: false,
      },
      mutations: {
        // Disable retries for mutations
        retry: false,
      },
    },
  });
}

/**
 * Wrapper component that provides QueryClient to children
 */
interface QueryWrapperProps {
  children: ReactNode;
  queryClient?: QueryClient;
}

function QueryWrapper({ children, queryClient }: QueryWrapperProps) {
  const client = queryClient || createTestQueryClient();

  return (
    <IntlProvider locale={DEFAULT_LOCALE} messages={TEST_MESSAGES}>
      <QueryClientProvider client={client}>{children}</QueryClientProvider>
    </IntlProvider>
  );
}

/**
 * Custom render function that wraps components with QueryClientProvider
 *
 * Usage:
 * ```tsx
 * const { result } = renderWithQuery(<MyComponent />);
 * ```
 *
 * With custom QueryClient:
 * ```tsx
 * const queryClient = createTestQueryClient();
 * const { result } = renderWithQuery(<MyComponent />, { queryClient });
 * ```
 *
 * @param ui Component to render
 * @param options Render options with optional queryClient
 * @returns Render result
 */
export function renderWithQuery(
  ui: ReactElement,
  options?: Omit<RenderOptions, 'wrapper'> & { queryClient?: QueryClient }
) {
  const { queryClient, ...renderOptions } = options || {};

  return render(ui, {
    wrapper: ({ children }) => <QueryWrapper queryClient={queryClient}>{children}</QueryWrapper>,
    ...renderOptions,
  });
}

/**
 * Wait for queries to settle (useful for async assertions)
 *
 * Usage:
 * ```tsx
 * const queryClient = createTestQueryClient();
 * renderWithQuery(<MyComponent />, { queryClient });
 * await waitForQueries(queryClient);
 * expect(screen.getByText('Data loaded')).toBeInTheDocument();
 * ```
 */
export async function waitForQueries(queryClient: QueryClient): Promise<void> {
  // Wait for all active queries to settle
  const queries = queryClient.getQueryCache().getAll();
  await Promise.all(queries.map(q => q.promise).filter(Boolean));
}

/**
 * Clear all query cache (useful in beforeEach/afterEach)
 *
 * Usage:
 * ```tsx
 * afterEach(() => {
 *   clearQueryCache(queryClient);
 * });
 * ```
 */
export function clearQueryCache(queryClient: QueryClient): void {
  queryClient.clear();
}
