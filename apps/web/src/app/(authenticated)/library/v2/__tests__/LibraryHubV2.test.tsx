import { render, screen } from '@testing-library/react';
import { describe, it, expect, vi, beforeEach } from 'vitest';

vi.mock('next/navigation', () => ({
  usePathname: () => '/library',
  useRouter: () => ({ push: vi.fn() }),
  useSearchParams: () => ({ get: () => null }),
}));

vi.mock('@/stores/use-card-hand', () => {
  const state = {
    cards: [],
    pinnedIds: new Set(),
    drawCard: vi.fn(),
    pinCard: vi.fn(),
    unpinCard: vi.fn(),
    expandedStack: false,
    toggleExpandStack: vi.fn(),
  };
  return {
    useCardHand: (selector?: (s: typeof state) => unknown) => (selector ? selector(state) : state),
  };
});

vi.mock('@/lib/stores/mini-nav-config-store', () => {
  const state = {
    config: null,
    setConfig: vi.fn(),
    clear: vi.fn(),
  };
  return {
    useMiniNavConfigStore: (selector?: (s: typeof state) => unknown) =>
      selector ? selector(state) : state,
  };
});

import { LibraryHubV2 } from '../LibraryHubV2';

describe('LibraryHubV2', () => {
  beforeEach(() => {
    vi.clearAllMocks();
  });

  it('renders the library header', () => {
    render(<LibraryHubV2 />);
    expect(screen.getByText(/La tua libreria/i)).toBeInTheDocument();
  });

  it('renders the filter bar', () => {
    render(<LibraryHubV2 />);
    expect(screen.getByText('Tutti')).toBeInTheDocument();
    expect(screen.getByText('Strategici')).toBeInTheDocument();
  });

  it('renders the personal library section with add card (always present)', () => {
    render(<LibraryHubV2 />);
    expect(screen.getByRole('button', { name: /aggiungi gioco/i })).toBeInTheDocument();
  });
});
