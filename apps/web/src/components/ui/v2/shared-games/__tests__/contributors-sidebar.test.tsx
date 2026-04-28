/**
 * ContributorsSidebar — leaderboard rendering tests.
 *
 * Wave A.3b — V2 Migration `/shared-games` (Issue #596).
 *
 * Covered:
 *   - Loading state shows skeleton placeholders + aria-busy.
 *   - Error / empty state collapses to a discreet help line.
 *   - Populated list renders rank + display name + sessions/wins counts.
 *   - Avatar fallback uses initials when `avatarUrl` is null.
 */
import { render, screen } from '@testing-library/react';
import { describe, expect, it } from 'vitest';

import type { TopContributor } from '@/lib/api';

import { ContributorsSidebar } from '../contributors-sidebar';

function makeContributor(overrides: Partial<TopContributor> = {}): TopContributor {
  return {
    userId: '00000000-0000-0000-0000-000000000001',
    displayName: 'Mario Rossi',
    avatarUrl: null,
    totalSessions: 12,
    totalWins: 5,
    score: 22,
    ...overrides,
  };
}

describe('ContributorsSidebar', () => {
  it('renders skeleton placeholders while loading', () => {
    render(
      <ContributorsSidebar
        contributors={undefined}
        isLoading
        isError={false}
        title="Top contributor"
      />
    );

    const aside = screen.getByTestId('shared-games-contributors');
    const list = aside.querySelector('ul[aria-busy="true"]');
    expect(list).not.toBeNull();
    expect(list?.children).toHaveLength(5);
  });

  it('shows the empty label when error and no data', () => {
    render(
      <ContributorsSidebar
        contributors={undefined}
        isLoading={false}
        isError
        title="Top contributor"
        emptyLabel="Nessun contributor"
      />
    );

    expect(screen.getByText('Nessun contributor')).toBeInTheDocument();
    expect(screen.queryByTestId('shared-games-contributors-list')).not.toBeInTheDocument();
  });

  it('shows the empty label when the list is empty', () => {
    render(
      <ContributorsSidebar contributors={[]} isLoading={false} isError={false} emptyLabel="Vuoto" />
    );

    expect(screen.getByText('Vuoto')).toBeInTheDocument();
  });

  it('renders contributor entries with rank, name and counts', () => {
    const contributors: TopContributor[] = [
      makeContributor({ displayName: 'Alice', totalSessions: 10, totalWins: 3 }),
      makeContributor({
        userId: '00000000-0000-0000-0000-000000000002',
        displayName: 'Bob',
        totalSessions: 7,
        totalWins: 4,
      }),
    ];

    render(<ContributorsSidebar contributors={contributors} isLoading={false} isError={false} />);

    const list = screen.getByTestId('shared-games-contributors-list');
    expect(list.children).toHaveLength(2);

    expect(screen.getByText('Alice')).toBeInTheDocument();
    expect(screen.getByText('Bob')).toBeInTheDocument();
    expect(screen.getByText(/10 sessioni/)).toBeInTheDocument();
    expect(screen.getByText(/4 vittorie/)).toBeInTheDocument();
  });

  it('uses initials avatar when avatarUrl is null', () => {
    render(
      <ContributorsSidebar
        contributors={[makeContributor({ displayName: 'Mario Rossi', avatarUrl: null })]}
        isLoading={false}
        isError={false}
      />
    );

    expect(screen.getByText('MR')).toBeInTheDocument();
  });

  it('renders an <img> avatar when avatarUrl is provided', () => {
    render(
      <ContributorsSidebar
        contributors={[
          makeContributor({
            displayName: 'Image User',
            avatarUrl: 'https://example.test/a.png',
          }),
        ]}
        isLoading={false}
        isError={false}
      />
    );

    const img = document.querySelector(
      'img[src="https://example.test/a.png"]'
    ) as HTMLImageElement | null;
    expect(img).not.toBeNull();
    expect(img?.alt).toBe('');
  });
});
