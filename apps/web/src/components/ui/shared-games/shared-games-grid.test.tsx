import { render, screen } from '@testing-library/react';
import { describe, expect, it, vi } from 'vitest';

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
    state: 'default',
    games,
    cardLabels,
    ...overrides,
  };
}

describe('SharedGamesGrid (v2)', () => {
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
    expect(container.querySelectorAll('[data-slot="shared-games-card"]')).toHaveLength(2);
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
    expect(container.querySelectorAll('[data-slot="shared-games-card"]')).toHaveLength(0);
    rerender(<SharedGamesGrid {...build({ state: 'error' })} />);
    expect(container.querySelectorAll('[data-slot="shared-games-card"]')).toHaveLength(0);
  });
});
