/**
 * ScoringBreakdownTable unit tests — Wave D.3 (Issue #756).
 */

import { render, screen } from '@testing-library/react';
import { describe, expect, it } from 'vitest';

import type { RankedParticipant } from '@/lib/sessions-summary/tie-groups';

import { ScoringBreakdownTable } from '../ScoringBreakdownTable';
import type { ScoringBreakdownTableProps } from '../ScoringBreakdownTable';

const PARTICIPANT_BASE = {
  isOwner: false,
  joinOrder: 1,
  finalRank: 1,
  userId: null as string | null,
} satisfies Partial<RankedParticipant>;

const RANKED_DEFAULT: RankedParticipant[] = [
  {
    ...PARTICIPANT_BASE,
    id: 'p1',
    userId: 'u1',
    displayName: 'Marco',
    totalScore: 89,
    rank: 1,
    isTied: false,
    tiedPlayerIds: ['p1'],
  },
  {
    ...PARTICIPANT_BASE,
    id: 'p2',
    userId: 'u2',
    displayName: 'Anna',
    totalScore: 79,
    rank: 2,
    isTied: false,
    tiedPlayerIds: ['p2'],
  },
];

const RANKED_TIED: RankedParticipant[] = [
  {
    ...PARTICIPANT_BASE,
    id: 'p1',
    displayName: 'Marco',
    totalScore: 100,
    rank: 1,
    isTied: true,
    tiedPlayerIds: ['p1', 'p2'],
  },
  {
    ...PARTICIPANT_BASE,
    id: 'p2',
    displayName: 'Anna',
    totalScore: 100,
    rank: 1,
    isTied: true,
    tiedPlayerIds: ['p1', 'p2'],
  },
];

const LABELS: ScoringBreakdownTableProps['labels'] = {
  title: 'Punteggio dettagliato',
  headerName: 'Giocatore',
  headerScore: 'Punteggio',
  headerRank: 'Posizione',
  tied: 'Pari merito',
};

describe('ScoringBreakdownTable', () => {
  it('renders data-slot="scoring-breakdown-table"', () => {
    render(<ScoringBreakdownTable rankedParticipants={RANKED_DEFAULT} labels={LABELS} />);
    expect(document.querySelector('[data-slot="scoring-breakdown-table"]')).not.toBeNull();
  });

  it('renders semantic <table> with caption + thead + tbody', () => {
    render(<ScoringBreakdownTable rankedParticipants={RANKED_DEFAULT} labels={LABELS} />);
    expect(document.querySelector('table')).not.toBeNull();
    expect(document.querySelector('caption')).not.toBeNull();
    expect(document.querySelector('thead')).not.toBeNull();
    expect(document.querySelector('tbody')).not.toBeNull();
  });

  it('caption text matches labels.title', () => {
    render(<ScoringBreakdownTable rankedParticipants={RANKED_DEFAULT} labels={LABELS} />);
    expect(screen.getByText('Punteggio dettagliato')).toBeTruthy();
  });

  it('renders one row per participant', () => {
    render(<ScoringBreakdownTable rankedParticipants={RANKED_DEFAULT} labels={LABELS} />);
    expect(document.querySelectorAll('[data-slot="scoring-row"]').length).toBe(2);
  });

  it('renders rank, name, and score per row', () => {
    render(<ScoringBreakdownTable rankedParticipants={RANKED_DEFAULT} labels={LABELS} />);
    expect(screen.getByText('Marco')).toBeTruthy();
    expect(screen.getByText('Anna')).toBeTruthy();
    expect(screen.getByText('89')).toBeTruthy();
    expect(screen.getByText('79')).toBeTruthy();
  });

  it('marks tied rows with data-tied="true"', () => {
    render(<ScoringBreakdownTable rankedParticipants={RANKED_TIED} labels={LABELS} />);
    const rows = document.querySelectorAll('[data-slot="scoring-row"]');
    expect(rows[0].getAttribute('data-tied')).toBe('true');
    expect(rows[1].getAttribute('data-tied')).toBe('true');
  });

  it('renders = tied indicator with aria-label inside tied rows', () => {
    render(<ScoringBreakdownTable rankedParticipants={RANKED_TIED} labels={LABELS} />);
    const indicators = document.querySelectorAll('[data-slot="scoring-tied-indicator"]');
    expect(indicators.length).toBe(2);
    expect(indicators[0].getAttribute('aria-label')).toBe('Pari merito');
  });

  it('uses scope="col" on header cells', () => {
    render(<ScoringBreakdownTable rankedParticipants={RANKED_DEFAULT} labels={LABELS} />);
    const headers = document.querySelectorAll('thead th');
    headers.forEach(h => {
      expect(h.getAttribute('scope')).toBe('col');
    });
  });

  it('uses scope="row" on the participant name th', () => {
    render(<ScoringBreakdownTable rankedParticipants={RANKED_DEFAULT} labels={LABELS} />);
    const rowHeaders = document.querySelectorAll('tbody th[scope="row"]');
    expect(rowHeaders.length).toBe(2);
  });

  it('marks the score column with aria-sort="descending"', () => {
    render(<ScoringBreakdownTable rankedParticipants={RANKED_DEFAULT} labels={LABELS} />);
    const sortHeader = document.querySelector('thead th[aria-sort="descending"]');
    expect(sortHeader).not.toBeNull();
  });

  it('handles empty participants list', () => {
    render(<ScoringBreakdownTable rankedParticipants={[]} labels={LABELS} />);
    expect(document.querySelectorAll('[data-slot="scoring-row"]').length).toBe(0);
  });

  it('marks data-rank attribute per row', () => {
    render(<ScoringBreakdownTable rankedParticipants={RANKED_DEFAULT} labels={LABELS} />);
    const rows = document.querySelectorAll('[data-slot="scoring-row"]');
    expect(rows[0].getAttribute('data-rank')).toBe('1');
    expect(rows[1].getAttribute('data-rank')).toBe('2');
  });
});
