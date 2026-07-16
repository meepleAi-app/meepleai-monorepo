/**
 * @vitest-environment jsdom
 */

/**
 * WishlistPage — orchestrator smoke tests (Issue #3007, Task B6).
 *
 * Mirrors the sibling B1-B5 tests' i18n strategy: a real `IntlProvider` wired
 * to the flattened `it.json` catalog (not `renderWithQuery`'s EN catalog),
 * plus a real `QueryClientProvider` since `AddToWishlistDialog` (always
 * mounted for the header "add" CTA) calls `useLibrary`/`useAddToWishlist`/
 * `useUpdateWishlistItem` internally. All query hooks are mocked directly at
 * the module level so no network call is ever attempted.
 */

import { QueryClient, QueryClientProvider } from '@tanstack/react-query';
import { render, screen } from '@testing-library/react';
import userEvent from '@testing-library/user-event';
import { IntlProvider } from 'react-intl';
import { describe, expect, it, vi, beforeEach } from 'vitest';

import { flattenMessages } from '@/locales';
import itMessages from '@/locales/it.json';

import WishlistPage from '../page';

import type { WishlistItemDto } from '@/lib/api/schemas/wishlist.schemas';
import type { ReactElement } from 'react';

const MESSAGES = flattenMessages(itMessages as unknown as Record<string, unknown>);
const WISHLIST_I18N = itMessages.pages.library.wishlist;

// ============================================================================
// Mocks
// ============================================================================

const mockUseWishlist = vi.hoisted(() => vi.fn());
const mockUseRemoveFromWishlist = vi.hoisted(() => vi.fn());
const mockUseAddToWishlist = vi.hoisted(() => vi.fn());
const mockUseUpdateWishlistItem = vi.hoisted(() => vi.fn());
const mockUseLibrary = vi.hoisted(() => vi.fn());
const mockToastSuccess = vi.hoisted(() => vi.fn());

vi.mock('@/hooks/queries/useWishlist', () => ({
  useWishlist: mockUseWishlist,
  useRemoveFromWishlist: mockUseRemoveFromWishlist,
  useAddToWishlist: mockUseAddToWishlist,
  useUpdateWishlistItem: mockUseUpdateWishlistItem,
}));

vi.mock('@/hooks/queries/useLibrary', () => ({
  useLibrary: mockUseLibrary,
}));

vi.mock('@/components/layout/Toast', () => ({
  toast: {
    success: (...args: unknown[]) => mockToastSuccess(...args),
  },
}));

// ============================================================================
// Helpers
// ============================================================================

function renderPage(ui: ReactElement) {
  const queryClient = new QueryClient({
    defaultOptions: { queries: { retry: false }, mutations: { retry: false } },
  });
  return render(
    <QueryClientProvider client={queryClient}>
      <IntlProvider locale="it" messages={MESSAGES}>
        {ui}
      </IntlProvider>
    </QueryClientProvider>
  );
}

const LIBRARY_DATA = {
  items: [
    { gameId: 'game-1', gameTitle: 'Wingspan' },
    { gameId: 'game-2', gameTitle: 'Catan' },
  ],
  page: 1,
  pageSize: 500,
  totalCount: 2,
};

function buildItem(overrides: Partial<WishlistItemDto> = {}): WishlistItemDto {
  return {
    id: '00000000-0000-0000-0000-000000000001',
    userId: '00000000-0000-0000-0000-000000000002',
    gameId: 'game-1',
    gameName: 'Wingspan',
    priority: 'high',
    targetPrice: null,
    notes: null,
    addedAt: '2026-01-01T00:00:00Z',
    updatedAt: null,
    visibility: 'private',
    ...overrides,
  };
}

const ITEM_1 = buildItem();
const ITEM_2 = buildItem({
  id: '00000000-0000-0000-0000-000000000004',
  gameId: 'game-2',
  gameName: 'Catan',
  priority: 'medium',
});

describe('WishlistPage', () => {
  beforeEach(() => {
    vi.clearAllMocks();
    mockUseLibrary.mockReturnValue({ data: LIBRARY_DATA, isLoading: false, isError: false });
    mockUseRemoveFromWishlist.mockReturnValue({ mutate: vi.fn() });
    mockUseAddToWishlist.mockReturnValue({ mutate: vi.fn(), isPending: false });
    mockUseUpdateWishlistItem.mockReturnValue({ mutate: vi.fn(), isPending: false });
  });

  it('renders the localized page title', () => {
    mockUseWishlist.mockReturnValue({
      data: [ITEM_1, ITEM_2],
      isLoading: false,
      isError: false,
      refetch: vi.fn(),
    });

    renderPage(<WishlistPage />);

    expect(
      screen.getByRole('heading', { level: 1, name: WISHLIST_I18N.hero.title })
    ).toBeInTheDocument();
  });

  it('renders a card for each fetched wishlist item', () => {
    mockUseWishlist.mockReturnValue({
      data: [ITEM_1, ITEM_2],
      isLoading: false,
      isError: false,
      refetch: vi.fn(),
    });

    renderPage(<WishlistPage />);

    expect(screen.getByText('Wingspan')).toBeInTheDocument();
    expect(screen.getByText('Catan')).toBeInTheDocument();
    expect(screen.getAllByTestId('wishlist-card')).toHaveLength(2);
  });

  it('shows the empty state when there are no wishlist items', () => {
    mockUseWishlist.mockReturnValue({
      data: [],
      isLoading: false,
      isError: false,
      refetch: vi.fn(),
    });

    renderPage(<WishlistPage />);

    expect(screen.getByText(WISHLIST_I18N.empty.noItems.title)).toBeInTheDocument();
  });

  it('shows the filtered-empty state when an active priority filter excludes every item', async () => {
    const user = userEvent.setup();
    mockUseWishlist.mockReturnValue({
      data: [ITEM_1, ITEM_2],
      isLoading: false,
      isError: false,
      refetch: vi.fn(),
    });

    renderPage(<WishlistPage />);

    // ITEM_1/ITEM_2 are 'high'/'medium' — filtering to 'low' excludes both.
    await user.click(screen.getByRole('checkbox', { name: WISHLIST_I18N.priority.low }));

    expect(screen.getByText(WISHLIST_I18N.empty.filtered.title)).toBeInTheDocument();
    expect(screen.queryByTestId('wishlist-card')).not.toBeInTheDocument();
  });

  it('shows the error state when the wishlist query rejects', () => {
    mockUseWishlist.mockReturnValue({
      data: undefined,
      isLoading: false,
      isError: true,
      refetch: vi.fn(),
    });

    renderPage(<WishlistPage />);

    expect(screen.getByRole('alert')).toHaveTextContent(WISHLIST_I18N.error.message);
    expect(screen.getByRole('button', { name: WISHLIST_I18N.error.retry })).toBeInTheDocument();
  });
});
