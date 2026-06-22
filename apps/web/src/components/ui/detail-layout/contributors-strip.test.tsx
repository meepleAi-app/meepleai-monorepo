/**
 * Wave A.4 (Issue #603) — ContributorsStrip avatar overlap + overflow tests.
 *
 * Verifies the avatar stacking contract from spec §3.6:
 *  - Visible avatars truncated at maxVisible (default 8)
 *  - Surplus rendered as "+N" overflow circle when totalCount > visible.length
 *  - Initials extraction (first letter of first 2 tokens, uppercased)
 *  - Avatar fallback uses initials when avatarUrl is null/undefined
 *  - Image rendered when avatarUrl provided
 */

import { render, screen } from '@testing-library/react';
import { describe, expect, it } from 'vitest';

import { ContributorsStrip, type ContributorAvatar } from './contributors-strip';

const labels = {
  title: 'Top contributors',
  playersLabel: (n: number) => `${n} players`,
  overflowAriaLabel: (n: number) => `+${n} more contributors`,
};

const makeContributors = (n: number): ContributorAvatar[] =>
  Array.from({ length: n }, (_, i) => ({
    id: `c-${i}`,
    name: `Player ${i}`,
    avatarUrl: null,
  }));

describe('ContributorsStrip (Wave A.4)', () => {
  it('renders title and total count label', () => {
    render(<ContributorsStrip contributors={makeContributors(3)} totalCount={3} labels={labels} />);
    expect(screen.getByText('Top contributors')).toBeInTheDocument();
    expect(screen.getByText('3 players')).toBeInTheDocument();
  });

  it('shows initials fallback for contributors without avatarUrl', () => {
    render(
      <ContributorsStrip
        contributors={[{ id: '1', name: 'Mario Rossi', avatarUrl: null }]}
        totalCount={1}
        labels={labels}
      />
    );
    // initials: 'MR'
    expect(screen.getByText('MR')).toBeInTheDocument();
  });

  it('extracts single-token initials correctly', () => {
    render(
      <ContributorsStrip
        contributors={[{ id: '1', name: 'Alice', avatarUrl: null }]}
        totalCount={1}
        labels={labels}
      />
    );
    expect(screen.getByText('A')).toBeInTheDocument();
  });

  it('renders <img> when avatarUrl is provided', () => {
    render(
      <ContributorsStrip
        contributors={[{ id: '1', name: 'Alice', avatarUrl: 'https://example.com/a.png' }]}
        totalCount={1}
        labels={labels}
      />
    );
    const img = screen.getByRole('img', { name: 'Alice' });
    expect(img).toHaveAttribute('src', 'https://example.com/a.png');
  });

  it('truncates to maxVisible and renders overflow "+N"', () => {
    render(
      <ContributorsStrip
        contributors={makeContributors(8)}
        totalCount={20}
        maxVisible={8}
        labels={labels}
      />
    );
    // overflow = 20 - 8 = 12
    expect(screen.getByText('+12')).toBeInTheDocument();
    expect(screen.getByLabelText('+12 more contributors')).toBeInTheDocument();
  });

  it('does not render overflow circle when totalCount equals visible', () => {
    const { container } = render(
      <ContributorsStrip
        contributors={makeContributors(5)}
        totalCount={5}
        maxVisible={8}
        labels={labels}
      />
    );
    expect(container.textContent).not.toContain('+0');
    // No element matching the overflow aria-label
    expect(container.querySelector('[aria-label*="more contributors"]')).toBeNull();
  });

  it('respects custom maxVisible', () => {
    render(
      <ContributorsStrip
        contributors={makeContributors(10)}
        totalCount={10}
        maxVisible={3}
        labels={labels}
      />
    );
    // 3 visible + 1 overflow circle = 4 li elements
    // overflow = 10 - 3 = 7
    expect(screen.getByText('+7')).toBeInTheDocument();
  });

  it('does not render the avatar list when contributors is empty', () => {
    const { container } = render(
      <ContributorsStrip contributors={[]} totalCount={0} labels={labels} />
    );
    expect(container.querySelector('ul')).toBeNull();
  });
});
