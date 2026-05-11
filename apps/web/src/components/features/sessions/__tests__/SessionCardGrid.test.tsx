/**
 * SessionCardGrid unit tests — Wave D.1 (Issue #735).
 *
 * 7 tests:
 * 1. Renders data-slot="session-card-grid"
 * 2. Sets data-item-id from item.id
 * 3. Shows gameName as title
 * 4. Shows OutcomeBadge for completed sessions
 * 5. Shows ScoringInline (compact) in the body
 * 6. Fires onClick with item on click
 * 7. aria-label uses openSessionAriaTemplate with gameName substitution
 */

import { render, screen, fireEvent } from '@testing-library/react';
import { describe, expect, it, vi } from 'vitest';

import { SessionCardGrid } from '../SessionCardGrid';
import type { SessionCardGridProps, SessionCardGridLabels } from '../SessionCardGrid';
import type { SessionListItem } from '@/lib/sessions/sessions-filters';

const LABELS: SessionCardGridLabels = {
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
  scoreOverflowTemplate: '+{count} altri',
};

const ITEM: SessionListItem = {
  id: 'session-grid-1',
  gameName: 'Azul',
  date: '21 apr 2026',
  when: '4 giorni fa',
  duration: '42m',
  status: 'completed',
  outcome: 'lost',
  playerCount: 3,
  scores: [
    { name: 'Sara', score: 81, winner: true },
    { name: 'Marco', score: 72 },
    { name: 'Luca', score: 65 },
  ],
  hasChat: false,
};

describe('SessionCardGrid', () => {
  it('renders data-slot="session-card-grid"', () => {
    render(<SessionCardGrid item={ITEM} onClick={vi.fn()} labels={LABELS} />);
    expect(document.querySelector('[data-slot="session-card-grid"]')).not.toBeNull();
  });

  it('sets data-item-id from item.id', () => {
    render(<SessionCardGrid item={ITEM} onClick={vi.fn()} labels={LABELS} />);
    const el = document.querySelector('[data-slot="session-card-grid"]');
    expect(el!.getAttribute('data-item-id')).toBe('session-grid-1');
  });

  it('shows gameName as title text', () => {
    render(<SessionCardGrid item={ITEM} onClick={vi.fn()} labels={LABELS} />);
    // gameName appears in both h3 title and chip footer — query the h3 specifically
    const heading = document.querySelector('h3');
    expect(heading?.textContent?.trim()).toBe('Azul');
  });

  it('shows OutcomeBadge for completed sessions', () => {
    render(<SessionCardGrid item={ITEM} onClick={vi.fn()} labels={LABELS} />);
    expect(screen.getByText('△ Persa')).toBeTruthy();
  });

  it('shows ScoringInline (compact) in the card body', () => {
    render(<SessionCardGrid item={ITEM} onClick={vi.fn()} labels={LABELS} />);
    const scoring = document.querySelector('[data-slot="scoring-inline"]');
    expect(scoring).not.toBeNull();
    // Sara is winner, her name should appear
    expect(screen.getByText('Sara')).toBeTruthy();
  });

  it('fires onClick with item when clicked', () => {
    const onClick = vi.fn();
    render(<SessionCardGrid item={ITEM} onClick={onClick} labels={LABELS} />);
    const card = document.querySelector('[data-slot="session-card-grid"]') as HTMLButtonElement;
    fireEvent.click(card);
    expect(onClick).toHaveBeenCalledWith(ITEM);
  });

  it('aria-label uses openSessionAriaTemplate with gameName substitution', () => {
    render(<SessionCardGrid item={ITEM} onClick={vi.fn()} labels={LABELS} />);
    const el = document.querySelector('[data-slot="session-card-grid"]');
    expect(el!.getAttribute('aria-label')).toBe('Apri sessione Azul');
  });
});
