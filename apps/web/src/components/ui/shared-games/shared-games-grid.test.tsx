import { render, screen } from '@testing-library/react';
import { axe } from 'jest-axe';
import { beforeEach, describe, expect, it, vi } from 'vitest';

import { EmptyState } from './empty-state';
import { ErrorState } from './error-state';
import { type MeepleCardGameLabels } from './meeple-card-game';
import {
  SharedGamesGrid,
  type SharedGamesGridGame,
  type SharedGamesGridProps,
} from './shared-games-grid';

vi.mock('next/link', () => ({
  default: ({
    href,
    children,
    prefetch: _prefetch,
    ...rest
  }: {
    href: string;
    children: React.ReactNode;
    prefetch?: boolean;
  } & React.AnchorHTMLAttributes<HTMLAnchorElement>) => (
    <a href={href} {...rest}>
      {children}
    </a>
  ),
}));

vi.mock('@/hooks/useAdminRole', () => ({ useAdminRole: vi.fn() }));
vi.mock('@/components/features/cover-editor', () => ({
  AdminCoverEditAffordance: ({
    gameId,
    needsAttention,
  }: {
    gameId: string;
    needsAttention?: boolean;
  }) => (
    <button
      type="button"
      data-testid={`cover-edit-${gameId}`}
      data-needs-attention={needsAttention ? 'true' : 'false'}
    >
      edit
    </button>
  ),
}));

import { useAdminRole } from '@/hooks/useAdminRole';

const mockUseAdminRole = useAdminRole as unknown as ReturnType<typeof vi.fn>;

function setRole(isEditorOrAbove: boolean) {
  mockUseAdminRole.mockReturnValue({
    user: null,
    isSuperAdmin: false,
    isAdminOrAbove: isEditorOrAbove,
    isEditorOrAbove,
    hasRole: () => false,
    isLoading: false,
  });
}

const cardLabels: MeepleCardGameLabels = {
  ratingAriaLabel: 'Voto',
  toolkitLabel: 'tk',
  agentLabel: 'ag',
  newWeekAriaLabel: count => `${count} nuovi`,
};

const games: readonly SharedGamesGridGame[] = [
  {
    id: 'f47ac10b-58cc-4372-a567-0e02b2c3d479',
    title: 'Catan',
    rating: 4,
    year: 1995,
    toolkitsCount: 0,
    agentsCount: 0,
    kbsCount: 0,
    newThisWeekCount: 0,
  },
  {
    id: 'f47ac10b-58cc-4372-a567-0e02b2c3d480',
    title: 'Wingspan',
    rating: 5,
    year: 2019,
    toolkitsCount: 0,
    agentsCount: 0,
    kbsCount: 0,
    newThisWeekCount: 0,
  },
];

function build(overrides: Partial<SharedGamesGridProps> = {}): SharedGamesGridProps {
  return {
    heading: 'Giochi del catalogo',
    state: 'default',
    games,
    cardLabels,
    ...overrides,
  };
}

describe('SharedGamesGrid (v2)', () => {
  beforeEach(() => {
    vi.clearAllMocks();
    setRole(false);
  });

  it('does not render cover-edit affordances for a non-admin', () => {
    setRole(false);
    render(<SharedGamesGrid {...build()} />);
    expect(screen.queryAllByTestId(/^cover-edit-/)).toHaveLength(0);
  });

  it('renders a role-gated cover-edit affordance per card for an editor-or-above', () => {
    setRole(true);
    render(<SharedGamesGrid {...build()} />);
    expect(screen.getByTestId(`cover-edit-${games[0].id}`)).toBeInTheDocument();
    expect(screen.getByTestId(`cover-edit-${games[1].id}`)).toBeInTheDocument();
  });

  it('segnala come da sistemare le card senza cover (#3611)', () => {
    setRole(true);
    render(
      <SharedGamesGrid
        {...build({
          games: [
            { ...games[0], coverUrl: 'https://r2.example/c.webp' },
            { ...games[1], coverUrl: null },
          ],
        })}
      />
    );

    expect(screen.getByTestId(`cover-edit-${games[0].id}`)).toHaveAttribute(
      'data-needs-attention',
      'false'
    );
    expect(screen.getByTestId(`cover-edit-${games[1].id}`)).toHaveAttribute(
      'data-needs-attention',
      'true'
    );
  });

  it('inoltra il punto focale del crop fino alla cover renderizzata (#3611)', () => {
    const { container } = render(
      <SharedGamesGrid
        {...build({
          games: [
            {
              ...games[0],
              coverUrl: 'https://r2.example/c.webp',
              coverFocalX: 0.3,
              coverFocalY: 0.7,
            },
          ],
        })}
      />
    );

    expect(container.querySelector('img')).toHaveStyle({ objectPosition: '30% 70%' });
  });

  it('emits data-state matching the state prop', () => {
    const { container, rerender } = render(<SharedGamesGrid {...build({ state: 'default' })} />);
    expect(container.querySelector('[data-slot="shared-games-grid"]')).toHaveAttribute(
      'data-state',
      'default'
    );
    rerender(<SharedGamesGrid {...build({ state: 'loading' })} />);
    expect(container.querySelector('[data-slot="shared-games-grid"]')).toHaveAttribute(
      'data-state',
      'loading'
    );
  });

  it('renders 9 skeleton cards when state=loading', () => {
    const { container } = render(<SharedGamesGrid {...build({ state: 'loading' })} />);
    expect(container.querySelectorAll('[data-slot="shared-games-skeleton-card"]')).toHaveLength(9);
  });

  it('renders one MeepleCardGame per game when state=default', () => {
    const { container } = render(<SharedGamesGrid {...build()} />);
    expect(container.querySelectorAll('[data-testid="shared-games-card"]')).toHaveLength(2);
    expect(screen.getByText('Catan')).toBeInTheDocument();
    expect(screen.getByText('Wingspan')).toBeInTheDocument();
  });

  it('renders the errorNode slot when state=error', () => {
    render(
      <SharedGamesGrid {...build({ state: 'error', errorNode: <p data-testid="err">boom</p> })} />
    );
    expect(screen.getByTestId('err')).toBeInTheDocument();
  });

  it('renders the emptyNode slot when state=empty-search', () => {
    render(
      <SharedGamesGrid
        {...build({ state: 'empty-search', emptyNode: <p data-testid="empty">nothing</p> })}
      />
    );
    expect(screen.getByTestId('empty')).toBeInTheDocument();
  });

  it('renders the emptyNode slot when state=filtered-empty', () => {
    render(
      <SharedGamesGrid
        {...build({
          state: 'filtered-empty',
          emptyNode: <p data-testid="filt-empty">nothing matches</p>,
        })}
      />
    );
    expect(screen.getByTestId('filt-empty')).toBeInTheDocument();
  });

  it('does not render game cards when state is empty/filtered/error/loading', () => {
    const { container, rerender } = render(
      <SharedGamesGrid {...build({ state: 'empty-search' })} />
    );
    expect(container.querySelectorAll('[data-testid="shared-games-card"]')).toHaveLength(0);
    rerender(<SharedGamesGrid {...build({ state: 'error' })} />);
    expect(container.querySelectorAll('[data-testid="shared-games-card"]')).toHaveLength(0);
  });
});

// #5024: on /shared-games the page h1 (SharedGamesHero) was followed by an h3, the card titles or the
// title of the empty/error state, and axe/Lighthouse `heading-order` failed. Only that rule runs here:
// it is an axe best-practice rule, outside the WCAG A/AA set of the E2E gate.
describe('SharedGamesGrid — heading order after the page h1 (#5024)', () => {
  beforeEach(() => {
    vi.clearAllMocks();
    setRole(false);
  });

  const states: ReadonlyArray<[string, Partial<SharedGamesGridProps>]> = [
    ['the game cards', {}],
    [
      'the empty state',
      {
        state: 'empty-search',
        emptyNode: (
          <EmptyState
            kind="empty-search"
            title="Nessun gioco"
            resetLabel="Reset"
            onReset={() => {}}
          />
        ),
      },
    ],
    [
      'the error state',
      {
        state: 'error',
        errorNode: (
          <ErrorState
            title="Errore"
            description="Riprova"
            retryLabel="Riprova"
            onRetry={() => {}}
          />
        ),
      },
    ],
  ];

  it.each(states)('keeps the heading levels in order before %s', async (_, overrides) => {
    const { container } = render(
      <main>
        <h1>Catalogo community</h1>
        <SharedGamesGrid {...build(overrides)} />
      </main>
    );

    expect(container.querySelector('h3')).not.toBeNull();
    const results = await axe(container, {
      runOnly: { type: 'rule', values: ['heading-order'] },
    });
    expect(results.violations).toEqual([]);
  });

  it('names the results region with its heading', () => {
    render(<SharedGamesGrid {...build()} />);
    expect(screen.getByRole('region', { name: 'Giochi del catalogo' })).toBeInTheDocument();
    expect(
      screen.getByRole('heading', { level: 2, name: 'Giochi del catalogo' })
    ).toBeInTheDocument();
  });
});
