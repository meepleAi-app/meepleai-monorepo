/**
 * Comprehensive render utility with all providers
 *
 * Combines QueryClient, ChatStore, IntlProvider, and other providers
 * for integration testing. Works with MSW for realistic API testing.
 *
 * Issue #2760: MSW Infrastructure Setup
 *
 * @example
 * ```typescript
 * import { renderWithProviders } from '@/__tests__/utils/renderWithProviders';
 *
 * it('renders with all providers', async () => {
 *   const { user } = renderWithProviders(<MyComponent />);
 *
 *   await user.click(screen.getByRole('button'));
 *   expect(screen.getByText('Success')).toBeInTheDocument();
 * });
 * ```
 */

import React, { ReactElement, ReactNode } from 'react';
import { render, RenderOptions, RenderResult } from '@testing-library/react';
import userEvent from '@testing-library/user-event';
import { QueryClient, QueryClientProvider } from '@tanstack/react-query';
import { ChatStoreProvider } from '@/store/chat/ChatStoreProvider';

/**
 * Create a fresh QueryClient for testing
 *
 * Configured with:
 * - No retries (fail fast in tests)
 * - No garbage collection (fresh per test)
 * - No stale time (always fetch)
 * - No automatic refetching
 */
export const createTestQueryClient = (): QueryClient =>
  new QueryClient({
    defaultOptions: {
      queries: {
        retry: false,
        gcTime: 0,
        staleTime: 0,
        refetchOnMount: false,
        refetchOnWindowFocus: false,
        refetchOnReconnect: false,
      },
      mutations: {
        retry: false,
      },
    },
  });

/**
 * Options for renderWithProviders
 */
interface RenderWithProvidersOptions extends Omit<RenderOptions, 'wrapper'> {
  /** Custom QueryClient (defaults to createTestQueryClient()) */
  queryClient?: QueryClient;
  /** Initial chat store state */
  initialChatState?: Record<string, unknown>;
  /** Locale for IntlProvider (defaults to 'en') */
  locale?: string;
  /** Whether to include ChatStoreProvider (defaults to true) */
  withChatStore?: boolean;
  /** Custom wrapper to compose with default providers */
  customWrapper?: React.ComponentType<{ children: ReactNode }>;
}

/**
 * Extended render result with additional utilities
 */
interface RenderWithProvidersResult extends RenderResult {
  /** User event instance for interactions */
  user: ReturnType<typeof userEvent.setup>;
  /** QueryClient instance for direct cache manipulation */
  queryClient: QueryClient;
  /** Re-render with same providers */
  rerender: (ui: ReactElement) => void;
}

/**
 * All Providers Wrapper
 *
 * Wraps children with: QueryClient → ChatStore → (Custom)
 */
const createAllProvidersWrapper = ({
  queryClient,
  withChatStore = true,
  customWrapper: CustomWrapper,
}: {
  queryClient: QueryClient;
  withChatStore?: boolean;
  customWrapper?: React.ComponentType<{ children: ReactNode }>;
}) => {
  const Wrapper = ({ children }: { children: ReactNode }) => {
    let content = children;

    // Wrap with custom wrapper if provided
    if (CustomWrapper) {
      content = <CustomWrapper>{content}</CustomWrapper>;
    }

    // Wrap with ChatStoreProvider if enabled
    if (withChatStore) {
      content = <ChatStoreProvider>{content}</ChatStoreProvider>;
    }

    // Always wrap with QueryClientProvider
    return <QueryClientProvider client={queryClient}>{content}</QueryClientProvider>;
  };

  Wrapper.displayName = 'TestProvidersWrapper';
  return Wrapper;
};

/**
 * Render with all providers and userEvent setup
 *
 * This is the primary render utility for integration tests.
 * It sets up:
 * - QueryClientProvider with fresh client
 * - ChatStoreProvider (optional)
 * - userEvent for realistic interactions
 *
 * @example Basic usage
 * ```typescript
 * const { user } = renderWithProviders(<MyComponent />);
 * await user.click(screen.getByRole('button'));
 * ```
 *
 * @example With custom QueryClient
 * ```typescript
 * const queryClient = createTestQueryClient();
 * queryClient.setQueryData(['user'], mockUser);
 * renderWithProviders(<Profile />, { queryClient });
 * ```
 *
 * @example Without ChatStore
 * ```typescript
 * renderWithProviders(<StaticComponent />, { withChatStore: false });
 * ```
 */
export function renderWithProviders(
  ui: ReactElement,
  options: RenderWithProvidersOptions = {}
): RenderWithProvidersResult {
  const {
    queryClient = createTestQueryClient(),
    withChatStore = true,
    customWrapper,
    ...renderOptions
  } = options;

  const Wrapper = createAllProvidersWrapper({
    queryClient,
    withChatStore,
    customWrapper,
  });

  const user = userEvent.setup();

  const renderResult = render(ui, {
    wrapper: Wrapper,
    ...renderOptions,
  });

  return {
    ...renderResult,
    user,
    queryClient,
    rerender: (newUi: ReactElement) => {
      renderResult.rerender(newUi);
    },
  };
}

/**
 * Create a wrapper function for renderHook
 *
 * @example
 * ```typescript
 * const { result } = renderHook(() => useMyHook(), {
 *   wrapper: createHookWrapper()
 * });
 * ```
 */
export function createHookWrapper(options: Omit<RenderWithProvidersOptions, 'customWrapper'> = {}) {
  const { queryClient = createTestQueryClient(), withChatStore = true } = options;

  return ({ children }: { children: ReactNode }) => {
    let content = children;

    if (withChatStore) {
      content = <ChatStoreProvider>{content}</ChatStoreProvider>;
    }

    return <QueryClientProvider client={queryClient}>{content}</QueryClientProvider>;
  };
}

/**
 * Wait for all queries to settle
 *
 * Useful for waiting on async data before assertions.
 *
 * @example
 * ```typescript
 * const { queryClient } = renderWithProviders(<DataComponent />);
 * await waitForQueries(queryClient);
 * expect(screen.getByText('Data loaded')).toBeInTheDocument();
 * ```
 */
export async function waitForQueries(queryClient: QueryClient): Promise<void> {
  const queries = queryClient.getQueryCache().getAll();
  await Promise.all(queries.map((q) => q.promise).filter(Boolean));
}

/**
 * Clear query cache
 *
 * Call in afterEach to ensure test isolation.
 *
 * @example
 * ```typescript
 * afterEach(() => {
 *   clearQueryCache(queryClient);
 * });
 * ```
 */
export function clearQueryCache(queryClient: QueryClient): void {
  queryClient.clear();
}

/**
 * Prefill query cache with mock data
 *
 * Useful for testing components that expect data to be present.
 *
 * @example
 * ```typescript
 * const queryClient = createTestQueryClient();
 * prefillQueryCache(queryClient, {
 *   user: mockUser,
 *   games: mockGames,
 * });
 * renderWithProviders(<Dashboard />, { queryClient });
 * ```
 */
export function prefillQueryCache(
  queryClient: QueryClient,
  data: Record<string, unknown>
): QueryClient {
  Object.entries(data).forEach(([key, value]) => {
    queryClient.setQueryData([key], value);
  });
  return queryClient;
}
