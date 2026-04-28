/**
 * MeepleCardGame — anchor wrapper + field rendering tests.
 *
 * Wave A.3b — V2 Migration `/shared-games` (Issue #596).
 *
 * Covered:
 *   - Renders the title and a real `<a>` whose default href is
 *     `/shared-games/<id>`.
 *   - `aria-label` includes the rating formatted as "valutazione X.X su 10".
 *   - `data-testid="shared-games-card"` and `data-game-id` are wired so
 *     E2E / visual tests can locate cards reliably.
 *   - TOP / NEW cover labels surface only when the corresponding flags are
 *     set on the SharedGame.
 *   - Custom `href` overrides the default.
 *   - The schema has no `publisher` field — there is nothing to assert here
 *     beyond making sure the component compiles against `SharedGame` and
 *     does not reference such a field. The fixture below documents the
 *     contract.
 */
import { render, screen } from '@testing-library/react';
import { describe, expect, it, vi } from 'vitest';

import type { SharedGame } from '@/lib/api';

import { MeepleCardGame } from '../meeple-card-game';

// Capture the props handed down to MeepleCard so we can assert that fields
// not yet rendered visually by MeepleCard (cover labels TOP / NEW) are still
// forwarded correctly. The mock keeps the children-less surface minimal — we
// only need title + a marker to locate the body in the DOM.
const meepleCardSpy = vi.fn();
vi.mock('@/components/ui/data-display/meeple-card', () => ({
  MeepleCard: (props: Record<string, unknown>) => {
    meepleCardSpy(props);
    return (
      <div data-testid="mock-meeple-card">
        <span>{String(props.title ?? '')}</span>
      </div>
    );
  },
}));

function makeGame(overrides: Partial<SharedGame> = {}): SharedGame {
  return {
    id: '11111111-1111-1111-1111-111111111111',
    bggId: 1,
    title: 'Catan',
    yearPublished: 1995,
    description: '',
    minPlayers: 3,
    maxPlayers: 4,
    playingTimeMinutes: 90,
    minAge: 10,
    complexityRating: null,
    averageRating: 7.2,
    imageUrl: '',
    thumbnailUrl: '',
    status: 'Active',
    isRagPublic: false,
    hasKnowledgeBase: false,
    createdAt: '2026-01-01T00:00:00Z',
    modifiedAt: null,
    toolkitsCount: 0,
    agentsCount: 0,
    kbsCount: 0,
    newThisWeekCount: 0,
    contributorsCount: 0,
    isTopRated: false,
    isNew: false,
    ...overrides,
  };
}

describe('MeepleCardGame', () => {
  it('renders the title and a default href to /shared-games/<id>', () => {
    render(<MeepleCardGame game={makeGame()} />);

    const card = screen.getByTestId('shared-games-card');
    expect(card.tagName.toLowerCase()).toBe('a');
    expect(card).toHaveAttribute('href', '/shared-games/11111111-1111-1111-1111-111111111111');
    expect(card).toHaveAttribute('data-game-id', '11111111-1111-1111-1111-111111111111');
    expect(screen.getByText('Catan')).toBeInTheDocument();
  });

  it('honors the optional `href` override', () => {
    render(<MeepleCardGame game={makeGame()} href="/custom/path" />);

    expect(screen.getByTestId('shared-games-card')).toHaveAttribute('href', '/custom/path');
  });

  it('builds an aria-label that includes the rating formatted to one decimal', () => {
    render(<MeepleCardGame game={makeGame({ averageRating: 8.456 })} />);

    expect(screen.getByTestId('shared-games-card')).toHaveAttribute(
      'aria-label',
      'Catan, valutazione 8.5 su 10'
    );
  });

  it('omits the rating fragment in aria-label when averageRating is null', () => {
    render(<MeepleCardGame game={makeGame({ averageRating: null })} />);

    expect(screen.getByTestId('shared-games-card')).toHaveAttribute('aria-label', 'Catan');
  });

  it('forwards TOP and NEW cover labels to MeepleCard when the flags are set', () => {
    meepleCardSpy.mockClear();
    render(
      <MeepleCardGame game={makeGame({ isTopRated: true, isNew: true, title: 'Hot Game' })} />
    );

    expect(meepleCardSpy).toHaveBeenCalledTimes(1);
    const props = meepleCardSpy.mock.calls[0][0] as {
      coverLabels?: ReadonlyArray<{ text: string }>;
    };
    expect(props.coverLabels).toEqual([{ text: 'TOP' }, { text: 'NEW' }]);
  });

  it('does not pass coverLabels when neither flag is set', () => {
    meepleCardSpy.mockClear();
    render(<MeepleCardGame game={makeGame()} />);

    expect(meepleCardSpy).toHaveBeenCalledTimes(1);
    const props = meepleCardSpy.mock.calls[0][0] as {
      coverLabels?: ReadonlyArray<{ text: string }>;
    };
    expect(props.coverLabels).toBeUndefined();
  });

  it('forwards playingTimeMinutes as a "{n} min" Durata metadata entry', () => {
    meepleCardSpy.mockClear();
    render(<MeepleCardGame game={makeGame({ playingTimeMinutes: 45 })} />);

    const props = meepleCardSpy.mock.calls[0][0] as {
      metadata?: ReadonlyArray<{ label: string; value?: string }>;
    };
    const durata = props.metadata?.find(m => m.label === 'Durata');
    expect(durata?.value).toBe('45 min');
  });
});
