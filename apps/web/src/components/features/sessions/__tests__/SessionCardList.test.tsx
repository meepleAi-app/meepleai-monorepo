/**
 * SessionCardList unit tests — Wave D.1 (Issue #735).
 *
 * 8 tests:
 * 1. Renders data-slot="session-card-list"
 * 2. Sets data-item-id from item.id
 * 3. Shows gameName as title
 * 4. Shows OutcomeBadge for completed sessions
 * 5. Shows turn label for in-progress sessions
 * 6. Fires onClick with the item on click
 * 7. aria-label uses openSessionAriaTemplate with gameName substitution
 * 8. Abandoned session dims decorative cover (not body text) — WCAG AA fix #1221
 */

import { render, screen, fireEvent } from '@testing-library/react';
import { describe, expect, it, vi } from 'vitest';

import { SessionCardList } from '../SessionCardList';
import type { SessionCardListProps, SessionCardListLabels } from '../SessionCardList';
import type { SessionListItem } from '@/lib/sessions/sessions-filters';

const LABELS: SessionCardListLabels = {
  outcomeWon: '🏆 Vinta',
  outcomeLost: '△ Persa',
  outcomeTie: '= Pareggio',
  statusLive: 'Live',
  statusPaused: 'In pausa',
  statusAbandoned: '⊘ Abbandonata',
  playerCountTemplate: '{count} giocatori',
  chatCountTemplate: '{count} chat',
  turnTemplate: 'Turno {turn}',
  winnerLabel: 'Punteggi',
  openSessionAriaTemplate: 'Apri sessione {gameName}',
};

const COMPLETED_ITEM: SessionListItem = {
  id: 'session-1',
  gameName: 'Wingspan',
  date: '23 apr 2026',
  when: '2 giorni fa',
  duration: '1h 24m',
  status: 'completed',
  outcome: 'won',
  playerCount: 4,
  scores: [
    { name: 'Marco', score: 89, winner: true },
    { name: 'Anna', score: 76 },
  ],
  hasChat: true,
  chatCount: 3,
};

const INPROGRESS_ITEM: SessionListItem = {
  id: 'session-2',
  gameName: 'Brass',
  date: 'oggi · live',
  when: 'in corso',
  duration: '1h 12m',
  status: 'inprogress',
  outcome: null,
  playerCount: 4,
  scores: [{ name: 'Marco', score: 0 }],
  hasChat: false,
  turn: '12/18',
};

const ABANDONED_ITEM: SessionListItem = {
  id: 'session-3',
  gameName: 'Catan',
  date: '5 apr 2026',
  when: '3 sett fa',
  duration: '28m',
  status: 'abandoned',
  outcome: null,
  playerCount: 2,
  scores: [],
  hasChat: false,
};

describe('SessionCardList', () => {
  it('renders data-slot="session-card-list"', () => {
    render(<SessionCardList item={COMPLETED_ITEM} onClick={vi.fn()} labels={LABELS} />);
    expect(document.querySelector('[data-slot="session-card-list"]')).not.toBeNull();
  });

  it('sets data-item-id from item.id', () => {
    render(<SessionCardList item={COMPLETED_ITEM} onClick={vi.fn()} labels={LABELS} />);
    const el = document.querySelector('[data-slot="session-card-list"]');
    expect(el!.getAttribute('data-item-id')).toBe('session-1');
  });

  it('shows gameName as title text', () => {
    render(<SessionCardList item={COMPLETED_ITEM} onClick={vi.fn()} labels={LABELS} />);
    // gameName appears in both h3 title and chip footer — query the h3 specifically
    const heading = document.querySelector('h3');
    expect(heading?.textContent?.trim()).toBe('Wingspan');
  });

  it('shows OutcomeBadge for completed sessions', () => {
    render(<SessionCardList item={COMPLETED_ITEM} onClick={vi.fn()} labels={LABELS} />);
    expect(screen.getByText('🏆 Vinta')).toBeTruthy();
  });

  it('shows turn label for in-progress sessions', () => {
    render(<SessionCardList item={INPROGRESS_ITEM} onClick={vi.fn()} labels={LABELS} />);
    expect(screen.getByText('Turno 12/18')).toBeTruthy();
  });

  it('fires onClick with the item when clicked', () => {
    const onClick = vi.fn();
    render(<SessionCardList item={COMPLETED_ITEM} onClick={onClick} labels={LABELS} />);
    const card = document.querySelector('[data-slot="session-card-list"]') as HTMLButtonElement;
    fireEvent.click(card);
    expect(onClick).toHaveBeenCalledWith(COMPLETED_ITEM);
  });

  it('aria-label uses openSessionAriaTemplate with gameName substitution', () => {
    render(<SessionCardList item={COMPLETED_ITEM} onClick={vi.fn()} labels={LABELS} />);
    const el = document.querySelector('[data-slot="session-card-list"]');
    expect(el!.getAttribute('aria-label')).toBe('Apri sessione Wingspan');
  });

  it('abandoned session dims decorative cover only — body text stays full opacity (#1221 Real-C-A)', () => {
    render(<SessionCardList item={ABANDONED_ITEM} onClick={vi.fn()} labels={LABELS} />);
    const root = document.querySelector('[data-slot="session-card-list"]');
    expect(root).not.toBeNull();
    // Root MUST NOT carry opacity-70 (would dim text and fail WCAG AA 4.5:1)
    expect(root!.classList.contains('opacity-70')).toBe(false);
    // Cover placeholder (first aria-hidden child) MUST carry opacity-60 grayscale dim
    const cover = root!.querySelector('[aria-hidden="true"]');
    expect(cover).not.toBeNull();
    expect(cover!.className).toMatch(/opacity-60/);
    expect(cover!.className).toMatch(/grayscale/);
    // Left border MUST soften via /60 alpha when abandoned
    expect(root!.className).toMatch(/border-l-entity-session\/60/);
  });
});
