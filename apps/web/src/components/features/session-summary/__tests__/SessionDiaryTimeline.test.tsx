/**
 * SessionDiaryTimeline unit tests — Wave D.3 (Issue #756).
 */

import { fireEvent, render, screen } from '@testing-library/react';
import { describe, expect, it, vi } from 'vitest';

import type { DiaryEntryDto } from '@/lib/api/session-flow/types';

import { SessionDiaryTimeline } from '../SessionDiaryTimeline';
import type { DiaryTurnGroup, SessionDiaryTimelineProps } from '../SessionDiaryTimeline';

const buildEvent = (id: string, eventType: string, ts: string): DiaryEntryDto => ({
  id,
  sessionId: 's1',
  gameNightId: null,
  eventType,
  timestamp: ts,
  payload: null,
  createdBy: null,
  source: null,
});

const TURNS: DiaryTurnGroup[] = [
  {
    turn: 1,
    events: [
      buildEvent('e1', 'session_started', '2026-04-23T13:08:00Z'),
      buildEvent('e2', 'score_updated', '2026-04-23T13:09:00Z'),
    ],
  },
  {
    turn: 2,
    events: [
      buildEvent('e3', 'chat_message', '2026-04-23T13:15:00Z'),
      buildEvent('e4', 'photo_added', '2026-04-23T13:18:00Z'),
    ],
  },
];

const LABELS: SessionDiaryTimelineProps['labels'] = {
  title: 'Diario partita',
  filterAll: 'Tutti',
  filterScore: 'Score',
  filterEvent: 'Eventi',
  filterChat: 'Chat',
  filterPhoto: 'Foto',
  empty: 'Nessun evento per questo filtro',
  toggleAriaLabel: (turn, expanded) =>
    expanded ? `Comprimi turno ${turn}` : `Espandi turno ${turn}`,
  turnLabel: turn => `Turno ${turn}`,
  turnEventsCount: count => `${count} eventi`,
};

const DEFAULT_PROPS: SessionDiaryTimelineProps = {
  turns: TURNS,
  activeFilter: 'all',
  onFilterChange: vi.fn(),
  expandedTurns: new Set([1]),
  onToggleTurn: vi.fn(),
  labels: LABELS,
};

describe('SessionDiaryTimeline', () => {
  it('renders data-slot="session-diary-timeline"', () => {
    render(<SessionDiaryTimeline {...DEFAULT_PROPS} />);
    expect(document.querySelector('[data-slot="session-diary-timeline"]')).not.toBeNull();
  });

  it('renders 5 filter pills (all/score/event/chat/photo)', () => {
    render(<SessionDiaryTimeline {...DEFAULT_PROPS} />);
    expect(document.querySelectorAll('[data-slot="diary-filter-pill"]').length).toBe(5);
  });

  it('marks active filter pill with data-active="true"', () => {
    render(<SessionDiaryTimeline {...DEFAULT_PROPS} activeFilter="chat" />);
    const allPill = document.querySelector('[data-filter="all"]')!;
    const chatPill = document.querySelector('[data-filter="chat"]')!;
    expect(allPill.getAttribute('data-active')).toBeNull();
    expect(chatPill.getAttribute('data-active')).toBe('true');
  });

  it('aria-pressed mirrors active state', () => {
    render(<SessionDiaryTimeline {...DEFAULT_PROPS} activeFilter="all" />);
    const allPill = document.querySelector('[data-filter="all"]')!;
    expect(allPill.getAttribute('aria-pressed')).toBe('true');
  });

  it('fires onFilterChange when a filter pill is clicked', () => {
    const onFilterChange = vi.fn();
    render(<SessionDiaryTimeline {...DEFAULT_PROPS} onFilterChange={onFilterChange} />);
    const chatPill = document.querySelector('[data-filter="chat"]') as HTMLButtonElement;
    fireEvent.click(chatPill);
    expect(onFilterChange).toHaveBeenCalledWith('chat');
  });

  it('renders one turn item per group', () => {
    render(<SessionDiaryTimeline {...DEFAULT_PROPS} />);
    expect(document.querySelectorAll('[data-slot="diary-turn-item"]').length).toBe(2);
  });

  it('marks expanded turn with data-open="true"', () => {
    render(<SessionDiaryTimeline {...DEFAULT_PROPS} />);
    const items = document.querySelectorAll('[data-slot="diary-turn-item"]');
    const turn1 = Array.from(items).find(i => i.getAttribute('data-turn') === '1')!;
    const turn2 = Array.from(items).find(i => i.getAttribute('data-turn') === '2')!;
    expect(turn1.getAttribute('data-open')).toBe('true');
    expect(turn2.getAttribute('data-open')).toBeNull();
  });

  it('aria-expanded matches open state on toggle button', () => {
    render(<SessionDiaryTimeline {...DEFAULT_PROPS} />);
    const toggles = document.querySelectorAll('[data-slot="diary-turn-toggle"]');
    expect(toggles[0].getAttribute('aria-expanded')).toBe('true');
    expect(toggles[1].getAttribute('aria-expanded')).toBe('false');
  });

  it('fires onToggleTurn when a turn toggle is clicked', () => {
    const onToggleTurn = vi.fn();
    render(<SessionDiaryTimeline {...DEFAULT_PROPS} onToggleTurn={onToggleTurn} />);
    const turn2Toggle = document.querySelectorAll(
      '[data-slot="diary-turn-toggle"]'
    )[1] as HTMLButtonElement;
    fireEvent.click(turn2Toggle);
    expect(onToggleTurn).toHaveBeenCalledWith(2);
  });

  it('renders events list only when turn is expanded', () => {
    render(<SessionDiaryTimeline {...DEFAULT_PROPS} />);
    const eventLists = document.querySelectorAll('[data-slot="diary-events"]');
    expect(eventLists.length).toBe(1); // Only turn 1 expanded
  });

  it('shows empty state when filter excludes all events', () => {
    render(<SessionDiaryTimeline {...DEFAULT_PROPS} activeFilter="score" />);
    // Score filter only matches the score_updated event in turn 1
    // turn 2 should be filtered out (no score events)
    expect(document.querySelectorAll('[data-slot="diary-turn-item"]').length).toBe(1);
  });

  it('renders empty placeholder when no events match filter', () => {
    render(
      <SessionDiaryTimeline
        {...DEFAULT_PROPS}
        turns={[{ turn: 1, events: [buildEvent('e1', 'turn_advanced', '2026-04-23T13:00:00Z')] }]}
        activeFilter="chat"
      />
    );
    expect(screen.getByText('Nessun evento per questo filtro')).toBeTruthy();
  });

  it('uses role="tablist" on the filter pill container', () => {
    render(<SessionDiaryTimeline {...DEFAULT_PROPS} />);
    const tablist = document.querySelector('[role="tablist"]');
    expect(tablist).not.toBeNull();
  });
});
