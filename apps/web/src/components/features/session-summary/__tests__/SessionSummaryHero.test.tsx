/**
 * SessionSummaryHero unit tests — Wave D.3 (Issue #756).
 */

import { render, screen } from '@testing-library/react';
import { describe, expect, it } from 'vitest';

import type { RankedParticipant } from '@/lib/sessions-summary/tie-groups';

import { SessionSummaryHero } from '../SessionSummaryHero';
import type { SessionSummaryHeroProps } from '../SessionSummaryHero';

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
    joinOrder: 1,
    finalRank: 1,
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
    joinOrder: 2,
    finalRank: 2,
    rank: 2,
    isTied: false,
    tiedPlayerIds: ['p2'],
  },
  {
    ...PARTICIPANT_BASE,
    id: 'p3',
    userId: 'u3',
    displayName: 'Luca',
    totalScore: 64,
    joinOrder: 3,
    finalRank: 3,
    rank: 3,
    isTied: false,
    tiedPlayerIds: ['p3'],
  },
];

const RANKED_TIED: RankedParticipant[] = [
  {
    ...PARTICIPANT_BASE,
    id: 'p1',
    userId: 'u1',
    displayName: 'Marco',
    totalScore: 100,
    joinOrder: 1,
    finalRank: 1,
    rank: 1,
    isTied: true,
    tiedPlayerIds: ['p1', 'p2'],
  },
  {
    ...PARTICIPANT_BASE,
    id: 'p2',
    userId: 'u2',
    displayName: 'Anna',
    totalScore: 100,
    joinOrder: 2,
    finalRank: 1,
    rank: 1,
    isTied: true,
    tiedPlayerIds: ['p1', 'p2'],
  },
  {
    ...PARTICIPANT_BASE,
    id: 'p3',
    userId: 'u3',
    displayName: 'Luca',
    totalScore: 50,
    joinOrder: 3,
    finalRank: 3,
    rank: 3,
    isTied: false,
    tiedPlayerIds: ['p3'],
  },
];

const LABELS: SessionSummaryHeroProps['labels'] = {
  title: 'Riepilogo partita',
  tiedBanner: '🤝 Pareggio tra Marco e Anna',
  confettiAriaLabel: 'Animazione festeggiamento',
  confettiSkippedLabel: 'Medaglia vittoria',
  podiumPlaceAriaLabel: (place, name, score) => `Posizione ${place}: ${name} con ${score} punti`,
};

const DEFAULT_PROPS: SessionSummaryHeroProps = {
  rankedParticipants: RANKED_DEFAULT,
  showConfetti: true,
  labels: LABELS,
};

describe('SessionSummaryHero', () => {
  it('renders data-slot="session-summary-hero"', () => {
    render(<SessionSummaryHero {...DEFAULT_PROPS} />);
    expect(document.querySelector('[data-slot="session-summary-hero"]')).not.toBeNull();
  });

  it('renders title text from labels', () => {
    render(<SessionSummaryHero {...DEFAULT_PROPS} />);
    expect(screen.getByText('Riepilogo partita')).toBeTruthy();
  });

  it('renders 3 podium places for default 3-participant scenario', () => {
    render(<SessionSummaryHero {...DEFAULT_PROPS} />);
    expect(document.querySelectorAll('[data-slot="podium-place"]').length).toBe(3);
  });

  it('does NOT render tied banner when no ties', () => {
    render(<SessionSummaryHero {...DEFAULT_PROPS} />);
    expect(document.querySelector('[data-slot="hero-tied-banner"]')).toBeNull();
  });

  it('renders tied banner when 1st place is tied', () => {
    render(<SessionSummaryHero {...DEFAULT_PROPS} rankedParticipants={RANKED_TIED} />);
    expect(document.querySelector('[data-slot="hero-tied-banner"]')).not.toBeNull();
    expect(screen.getByText(/Pareggio/)).toBeTruthy();
  });

  it('marks data-tied="true" on hero when tied', () => {
    render(<SessionSummaryHero {...DEFAULT_PROPS} rankedParticipants={RANKED_TIED} />);
    const hero = document.querySelector('[data-slot="session-summary-hero"]') as HTMLElement;
    expect(hero.getAttribute('data-tied')).toBe('true');
  });

  it('renders T tied-badge on each tied 1st-place podium place', () => {
    render(<SessionSummaryHero {...DEFAULT_PROPS} rankedParticipants={RANKED_TIED} />);
    expect(document.querySelectorAll('[data-slot="podium-tied-badge"]').length).toBe(2);
  });

  it('renders confetti when showConfetti=true and not reduced motion', () => {
    render(<SessionSummaryHero {...DEFAULT_PROPS} showConfetti />);
    expect(document.querySelector('[data-slot="confetti"]')).not.toBeNull();
  });

  it('does NOT render confetti when showConfetti=false', () => {
    render(<SessionSummaryHero {...DEFAULT_PROPS} showConfetti={false} />);
    expect(document.querySelector('[data-slot="confetti"]')).toBeNull();
  });

  it('exposes confetti skipped fallback for reduced motion users', () => {
    render(<SessionSummaryHero {...DEFAULT_PROPS} showConfetti prefersReducedMotion />);
    const fallback = document.querySelector('[data-slot="confetti-skipped-fallback"]');
    expect(fallback).not.toBeNull();
    expect(fallback!.getAttribute('aria-label')).toBe('Medaglia vittoria');
  });

  it('uses region role with labelled heading', () => {
    render(<SessionSummaryHero {...DEFAULT_PROPS} />);
    const region = screen.getByRole('region');
    expect(region).not.toBeNull();
  });

  it('podium places carry data-place attributes 1/2/3', () => {
    render(<SessionSummaryHero {...DEFAULT_PROPS} />);
    const places = document.querySelectorAll('[data-slot="podium-place"]');
    const dataPlaces = Array.from(places).map(p => p.getAttribute('data-place'));
    // Layout order in DOM: [2nd, 1st, 3rd]
    expect(dataPlaces).toEqual(['2', '1', '3']);
  });

  it('renders solo variant for single participant', () => {
    render(<SessionSummaryHero {...DEFAULT_PROPS} rankedParticipants={[RANKED_DEFAULT[0]]} />);
    expect(document.querySelector('[data-slot="podium-solo"]')).not.toBeNull();
    expect(document.querySelector('[data-slot="podium-row"]')).toBeNull();
  });
});
