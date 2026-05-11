/**
 * AchievementsCarousel unit tests — Wave D.3 (Issue #756).
 */

import { render, screen } from '@testing-library/react';
import { describe, expect, it } from 'vitest';

import type { AchievementDto } from '@/lib/sessions-summary/schemas';

import { AchievementsCarousel } from '../AchievementsCarousel';
import type { AchievementsCarouselProps } from '../AchievementsCarousel';

const ACHIEVEMENTS: AchievementDto[] = [
  {
    id: 'a1',
    code: 'firstWin',
    titleKey: 'pages.sessionSummary.achievements.firstWin.title',
    descriptionKey: 'pages.sessionSummary.achievements.firstWin.description',
    iconEmoji: '🏆',
    unlockedAt: '2026-04-23T12:00:00Z',
  },
  {
    id: 'a2',
    code: 'comeback',
    titleKey: 'pages.sessionSummary.achievements.comeback.title',
    descriptionKey: 'pages.sessionSummary.achievements.comeback.description',
    iconEmoji: '🔥',
    unlockedAt: null,
  },
];

const LABELS: AchievementsCarouselProps['labels'] = {
  title: 'Achievements',
  unlockedCount: '1 / 2 sbloccati',
  emptyTitle: 'Nessun achievement questa partita',
  emptyDescription: 'Riprova per sbloccare nuovi badge!',
  lockedAriaPrefix: 'Bloccato: ',
  unlockedAtText: id => (id === 'a1' ? 'Sbloccato il 23 apr' : null),
  titleFor: id => (id === 'a1' ? 'Master birder' : 'Comeback'),
  descriptionFor: id => (id === 'a1' ? '15+ birds' : 'Servono 5 vittorie'),
};

describe('AchievementsCarousel', () => {
  it('renders data-slot="achievements-carousel"', () => {
    render(<AchievementsCarousel achievements={ACHIEVEMENTS} labels={LABELS} />);
    expect(document.querySelector('[data-slot="achievements-carousel"]')).not.toBeNull();
  });

  it('renders empty state when achievements array is empty', () => {
    render(<AchievementsCarousel achievements={[]} labels={LABELS} />);
    const root = document.querySelector('[data-slot="achievements-carousel"]')!;
    expect(root.getAttribute('data-empty')).toBe('true');
    expect(screen.getByText('Nessun achievement questa partita')).toBeTruthy();
    expect(screen.getByText('Riprova per sbloccare nuovi badge!')).toBeTruthy();
  });

  it('does NOT have data-empty when achievements present', () => {
    render(<AchievementsCarousel achievements={ACHIEVEMENTS} labels={LABELS} />);
    const root = document.querySelector('[data-slot="achievements-carousel"]')!;
    expect(root.getAttribute('data-empty')).toBeNull();
  });

  it('renders one card per achievement', () => {
    render(<AchievementsCarousel achievements={ACHIEVEMENTS} labels={LABELS} />);
    expect(document.querySelectorAll('[data-slot="achievement-card"]').length).toBe(2);
  });

  it('marks unlocked cards with data-unlocked="true"', () => {
    render(<AchievementsCarousel achievements={ACHIEVEMENTS} labels={LABELS} />);
    const cards = document.querySelectorAll('[data-slot="achievement-card"]');
    expect(cards[0].getAttribute('data-unlocked')).toBe('true');
    expect(cards[1].getAttribute('data-unlocked')).toBeNull();
  });

  it('builds aria-label from titleFor for unlocked cards', () => {
    render(<AchievementsCarousel achievements={ACHIEVEMENTS} labels={LABELS} />);
    const cards = document.querySelectorAll('[data-slot="achievement-card"]');
    expect(cards[0].getAttribute('aria-label')).toBe('Master birder');
  });

  it('prefixes "Bloccato: " on locked cards aria-label', () => {
    render(<AchievementsCarousel achievements={ACHIEVEMENTS} labels={LABELS} />);
    const cards = document.querySelectorAll('[data-slot="achievement-card"]');
    expect(cards[1].getAttribute('aria-label')).toBe('Bloccato: Comeback');
  });

  it('uses role="list" on the carousel ul', () => {
    render(<AchievementsCarousel achievements={ACHIEVEMENTS} labels={LABELS} />);
    const ul = document.querySelector('ul[role="list"]');
    expect(ul).not.toBeNull();
  });

  it('renders unlockedCount badge in header', () => {
    render(<AchievementsCarousel achievements={ACHIEVEMENTS} labels={LABELS} />);
    expect(screen.getByText('1 / 2 sbloccati')).toBeTruthy();
  });

  it('shows unlocked title text via titleFor resolver', () => {
    render(<AchievementsCarousel achievements={ACHIEVEMENTS} labels={LABELS} />);
    expect(screen.getByText('Master birder')).toBeTruthy();
  });

  it('renders 🔒 icon for locked cards', () => {
    const { container } = render(
      <AchievementsCarousel achievements={ACHIEVEMENTS} labels={LABELS} />
    );
    expect(container.textContent).toContain('🔒');
  });
});
