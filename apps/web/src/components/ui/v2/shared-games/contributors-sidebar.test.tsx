import { render, screen } from '@testing-library/react';
import { describe, expect, it } from 'vitest';

import {
  ContributorsSidebar,
  type ContributorsSidebarLabels,
  type ContributorsSidebarItem,
} from './contributors-sidebar';

const labels: ContributorsSidebarLabels = {
  title: 'Top contributor',
  emptyTitle: 'Nessun contributor questa settimana.',
  meta: (s, w) => `${s} sessioni · ${w} vittorie`,
  toolkitsLabel: 'toolkit community',
  agentsLabel: 'agent community',
  statsHeading: 'Statistiche community',
  rankAriaLabel: rank => `Posizione ${rank}`,
};

const contributors: readonly ContributorsSidebarItem[] = [
  { userId: 'a-1', displayName: 'Alice Wonder', totalSessions: 42, totalWins: 17, hue: 280 },
  { userId: 'b-2', displayName: 'Bob', totalSessions: 30, totalWins: 9, hue: 200 },
];

describe('ContributorsSidebar (v2)', () => {
  it('renders the title from labels', () => {
    render(
      <ContributorsSidebar
        contributors={contributors}
        toolkitsTotal={12}
        agentsTotal={7}
        labels={labels}
      />
    );
    expect(screen.getByRole('heading', { level: 2, name: 'Top contributor' })).toBeInTheDocument();
  });

  it('renders one ordered list item per contributor with data-rank', () => {
    const { container } = render(
      <ContributorsSidebar
        contributors={contributors}
        toolkitsTotal={12}
        agentsTotal={7}
        labels={labels}
      />
    );
    const items = container.querySelectorAll('[data-slot="shared-games-contributor-item"]');
    expect(items).toHaveLength(2);
    expect(items[0]).toHaveAttribute('data-rank', '1');
    expect(items[1]).toHaveAttribute('data-rank', '2');
  });

  it('shows display names and meta lines', () => {
    render(
      <ContributorsSidebar
        contributors={contributors}
        toolkitsTotal={12}
        agentsTotal={7}
        labels={labels}
      />
    );
    expect(screen.getByText('Alice Wonder')).toBeInTheDocument();
    expect(screen.getByText('42 sessioni · 17 vittorie')).toBeInTheDocument();
    expect(screen.getByText('Bob')).toBeInTheDocument();
    expect(screen.getByText('30 sessioni · 9 vittorie')).toBeInTheDocument();
  });

  it('renders initials for two-word names (Alice Wonder → AW)', () => {
    render(
      <ContributorsSidebar
        contributors={[contributors[0]!]}
        toolkitsTotal={0}
        agentsTotal={0}
        labels={labels}
      />
    );
    expect(screen.getByText('AW')).toBeInTheDocument();
  });

  it('renders 2-letter slice for single-word names (Bob → BO)', () => {
    render(
      <ContributorsSidebar
        contributors={[contributors[1]!]}
        toolkitsTotal={0}
        agentsTotal={0}
        labels={labels}
      />
    );
    expect(screen.getByText('BO')).toBeInTheDocument();
  });

  it('exposes rank with aria-label for each item', () => {
    render(
      <ContributorsSidebar
        contributors={contributors}
        toolkitsTotal={12}
        agentsTotal={7}
        labels={labels}
      />
    );
    expect(screen.getByLabelText('Posizione 1')).toBeInTheDocument();
    expect(screen.getByLabelText('Posizione 2')).toBeInTheDocument();
  });

  it('renders aggregate community stats (toolkits + agents)', () => {
    render(
      <ContributorsSidebar
        contributors={contributors}
        toolkitsTotal={12}
        agentsTotal={7}
        labels={labels}
      />
    );
    expect(screen.getByText('12 toolkit community')).toBeInTheDocument();
    expect(screen.getByText('7 agent community')).toBeInTheDocument();
  });

  it('renders empty-state copy when contributors list is empty', () => {
    render(
      <ContributorsSidebar contributors={[]} toolkitsTotal={0} agentsTotal={0} labels={labels} />
    );
    expect(screen.getByText('Nessun contributor questa settimana.')).toBeInTheDocument();
    expect(screen.queryByRole('listitem')).not.toBeInTheDocument();
  });
});
