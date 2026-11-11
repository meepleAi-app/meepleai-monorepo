/**
 * #4990: the Catalogo tab of /games was a "coming soon" placeholder; the shared catalog was
 * reachable only from Library → Aggiungi gioco.
 */
import { act, fireEvent, render, screen } from '@testing-library/react';
import { beforeEach, describe, expect, it, vi } from 'vitest';

import { CatalogTabContent } from '../CatalogTabContent';

const replace = vi.fn();
let search = new URLSearchParams('tab=catalogo');

vi.mock('next/navigation', () => ({
  useRouter: () => ({ replace, push: vi.fn() }),
  usePathname: () => '/games',
  useSearchParams: () => search,
}));

vi.mock('@/hooks/useTranslation', () => ({
  useTranslation: () => ({
    t: (key: string, values?: Record<string, unknown>) =>
      values ? `${key}:${JSON.stringify(values)}` : key,
  }),
}));

vi.mock('@/components/ui/data-display/meeple-card/MeepleCard', () => ({
  MeepleCard: ({ title, href, imageUrl }: { title: string; href?: string; imageUrl?: string }) => (
    <a href={href} data-testid="catalog-card" data-image={imageUrl ?? ''}>
      {title}
    </a>
  ),
}));

// The title hook resolves the user's locale; covered by its own tests.
vi.mock('@/lib/i18n/use-game-title', () => ({
  useGameTitle: (game: { title: string }) => ({
    value: game.title,
    source: 'canonical',
    locale: 'it',
  }),
}));

const useSharedGames = vi.fn();
vi.mock('@/hooks/queries', () => ({
  useSharedGames: (...args: unknown[]) => useSharedGames(...args),
}));

function catalog(
  items: Array<{ id: string; title: string; coverUrl?: string | null }>,
  total = items.length
) {
  useSharedGames.mockReturnValue({
    data: { items, total, page: 1, pageSize: 24 },
    isLoading: false,
    isError: false,
    refetch: vi.fn(),
  });
}

describe('CatalogTabContent (#4990)', () => {
  beforeEach(() => {
    replace.mockReset();
    useSharedGames.mockReset();
    search = new URLSearchParams('tab=catalogo');
  });

  it('shows a card per catalog game, linking to its page, with its cover', () => {
    catalog([
      { id: 'g1', title: 'Jaipur', coverUrl: 'https://covers.example/jaipur.jpg' },
      { id: 'g2', title: 'Root', coverUrl: null },
    ]);
    render(<CatalogTabContent />);

    const cards = screen.getAllByTestId('catalog-card');
    expect(cards.map(c => c.textContent)).toEqual(['Jaipur', 'Root']);
    expect(cards[0]).toHaveAttribute('href', '/games/g1');
    expect(cards[0]).toHaveAttribute('data-image', 'https://covers.example/jaipur.jpg');
    expect(cards[1]).toHaveAttribute('data-image', '');
  });

  it('searches the catalog with the q of the URL, from the first page', () => {
    search = new URLSearchParams('tab=catalogo&q=jaipur');
    catalog([]);
    render(<CatalogTabContent />);

    expect(useSharedGames).toHaveBeenLastCalledWith(
      expect.objectContaining({ searchTerm: 'jaipur', page: 1 }),
      true,
      true
    );
  });

  it('writes what the user types into the URL, keeping the tab', () => {
    vi.useFakeTimers();
    try {
      catalog([]);
      render(<CatalogTabContent />);

      fireEvent.change(screen.getByRole('searchbox'), { target: { value: 'root' } });
      act(() => {
        vi.advanceTimersByTime(400);
      });

      expect(replace).toHaveBeenCalledWith('/games?tab=catalogo&q=root', { scroll: false });
    } finally {
      vi.useRealTimers();
    }
  });

  it('says nothing matched the search, naming it', () => {
    search = new URLSearchParams('tab=catalogo&q=zzz');
    catalog([]);
    render(<CatalogTabContent />);

    expect(screen.getByText('pages.games.catalog.noResults:{"query":"zzz"}')).toBeInTheDocument();
  });

  it('pages through the catalog', () => {
    catalog([{ id: 'g1', title: 'Jaipur' }], 50);
    render(<CatalogTabContent />);

    fireEvent.click(
      screen.getByRole('button', { name: /pages\.library\.catalogPagination\.nextPage/ })
    );

    expect(useSharedGames).toHaveBeenLastCalledWith(
      expect.objectContaining({ page: 2 }),
      true,
      true
    );
  });
});
