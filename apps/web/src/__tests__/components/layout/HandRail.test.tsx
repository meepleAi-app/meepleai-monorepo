import { render, screen } from '@testing-library/react';
import { vi, describe, it, expect, beforeEach } from 'vitest';
import type { HandCard } from '@/lib/stores/card-hand-store';

// Mock token imports first (before HandRail imports)
vi.mock('@/components/ui/data-display/meeple-card/tokens', () => ({
  entityHsl: () => `hsl(100, 50%, 50%)`,
  entityIcon: {
    game: '🎲',
    player: '👤',
    collection: '📚',
    event: '📅',
  },
}));

// next/link stub
vi.mock('next/link', () => ({
  default: ({
    href,
    children,
    ...props
  }: {
    href: string;
    children: React.ReactNode;
    [k: string]: unknown;
  }) => (
    <a href={href} {...props}>
      {children}
    </a>
  ),
}));

// Mock next/navigation with mutable state
let currentPathname = '/dashboard';
vi.mock('next/navigation', () => ({
  usePathname: () => currentPathname,
}));

// Mock the card-hand-store to avoid Zustand subscription issues
const mockCards: HandCard[] = [];
vi.mock('@/lib/stores/card-hand-store', async () => {
  const actual = await vi.importActual<typeof import('@/lib/stores/card-hand-store')>(
    '@/lib/stores/card-hand-store'
  );
  return {
    ...actual,
    useCardHand: (selector?: (state: any) => any) => {
      const state = { cards: mockCards };
      return selector ? selector(state) : state.cards;
    },
  };
});

// Import after all mocks are set up
import { HandRail } from '@/components/layout/HandRail';

beforeEach(() => {
  mockCards.length = 0;
  currentPathname = '/dashboard';
});

describe('HandRail', () => {
  it('non renderizza nulla con la mano vuota', () => {
    const { container } = render(<HandRail />);
    expect(container.firstChild).toBeNull();
  });

  it('renderizza dopo drawCard', () => {
    mockCards.push({
      id: 'game:abc',
      entityType: 'game',
      entityId: 'abc',
      label: 'Catan',
      href: '/games/abc',
      pinned: false,
      addedAt: Date.now(),
    });
    render(<HandRail />);
    expect(screen.getByTestId('hand-rail')).toBeInTheDocument();
    expect(screen.getByTestId('rail-card-game:abc')).toBeInTheDocument();
  });

  it('imposta aria-current="page" sulla card attiva', () => {
    currentPathname = '/games/abc';

    mockCards.push({
      id: 'game:abc',
      entityType: 'game',
      entityId: 'abc',
      label: 'Catan',
      href: '/games/abc',
      pinned: false,
      addedAt: Date.now(),
    });
    render(<HandRail />);
    const link = screen.getByTestId('rail-card-game:abc');
    expect(link).toHaveAttribute('aria-current', 'page');
  });

  it('separa pinned da recent con sezione labels', () => {
    mockCards.push(
      {
        id: 'game:1',
        entityType: 'game',
        entityId: '1',
        label: 'Pinned',
        href: '/games/1',
        pinned: true,
        addedAt: Date.now() - 1000,
      },
      {
        id: 'game:2',
        entityType: 'game',
        entityId: '2',
        label: 'Recent',
        href: '/games/2',
        pinned: false,
        addedAt: Date.now(),
      }
    );
    render(<HandRail />);
    expect(screen.getByText(/Fissate/i)).toBeInTheDocument();
    expect(screen.getByText(/Recenti/i)).toBeInTheDocument();
  });
});
