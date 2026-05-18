import { describe, expect, it, vi } from 'vitest';
import { render, screen } from '@testing-library/react';
import userEvent from '@testing-library/user-event';
import {
  CrossGameDiaryTimeline,
  type DiaryEvent,
  type DiaryGameRef,
  type DiaryPlayerRef,
} from './CrossGameDiaryTimeline';

const GAMES: ReadonlyArray<DiaryGameRef> = [
  { id: 'g1', title: 'Brass: Birmingham', emoji: '🏭' },
  { id: 'g2', title: 'Spirit Island', emoji: '🌋' },
];

const PLAYERS: ReadonlyArray<DiaryPlayerRef> = [
  { id: 'p1', initials: 'MR', color: 262 },
  { id: 'p2', initials: 'DC', color: 200 },
];

const SAMPLE_EVENTS: ReadonlyArray<DiaryEvent> = [
  {
    id: 'e1',
    time: '21:02',
    gameId: 'g1',
    kind: 'turn',
    icon: '🎯',
    actors: ['p1'],
    text: 'Marco apre il Canal Era',
  },
  {
    id: 'e2',
    time: '22:55',
    gameId: 'g1',
    kind: 'end',
    icon: '🏆',
    actors: ['p2'],
    text: '🏆 Davide vince Brass Birmingham 178–142',
  },
  {
    id: 'e3',
    time: '23:00',
    gameId: null,
    kind: 'system',
    icon: '↻',
    actors: [],
    text: 'Setup Spirit Island',
  },
  {
    id: 'e4',
    time: '23:08',
    gameId: 'g2',
    kind: 'score',
    icon: '📊',
    actors: ['p2'],
    text: 'Spirito River Surges +3 Fear',
  },
];

describe('CrossGameDiaryTimeline', () => {
  it('renders all events when filter="all"', () => {
    render(<CrossGameDiaryTimeline events={SAMPLE_EVENTS} games={GAMES} players={PLAYERS} />);
    expect(screen.getByText('Marco apre il Canal Era')).toBeInTheDocument();
    expect(screen.getByText('🏆 Davide vince Brass Birmingham 178–142')).toBeInTheDocument();
    expect(screen.getByText('Setup Spirit Island')).toBeInTheDocument();
    expect(screen.getByText('Spirito River Surges +3 Fear')).toBeInTheDocument();
  });

  it('filters events by kind when filter !== "all"', () => {
    render(
      <CrossGameDiaryTimeline
        events={SAMPLE_EVENTS}
        games={GAMES}
        players={PLAYERS}
        filter="score"
      />
    );
    expect(screen.queryByText('Marco apre il Canal Era')).toBeNull();
    expect(screen.getByText('Spirito River Surges +3 Fear')).toBeInTheDocument();
  });

  it('renders timestamps in tabular-nums', () => {
    const { container } = render(
      <CrossGameDiaryTimeline events={SAMPLE_EVENTS} games={GAMES} players={PLAYERS} />
    );
    const timestamps = container.querySelectorAll('.tabular-nums');
    expect(timestamps.length).toBeGreaterThan(0);
    expect(timestamps[0].textContent).toBe('21:02');
  });

  it('renders cross-game divider when gameId changes', () => {
    render(<CrossGameDiaryTimeline events={SAMPLE_EVENTS} games={GAMES} players={PLAYERS} />);
    // first game shows divider (no previous), second game (Spirit Island) shows another
    const spiritIsland = screen.getAllByText('Spirit Island');
    expect(spiritIsland.length).toBeGreaterThanOrEqual(1);
  });

  it('renders empty state message when filter yields zero events', () => {
    render(<CrossGameDiaryTimeline events={[]} games={GAMES} players={PLAYERS} />);
    expect(screen.getByText(/Nessun evento ancora/)).toBeInTheDocument();
  });

  it('renders filter chips only when onFilterChange provided', () => {
    const { rerender } = render(
      <CrossGameDiaryTimeline events={SAMPLE_EVENTS} games={GAMES} players={PLAYERS} />
    );
    expect(screen.queryByRole('tablist')).toBeNull();

    rerender(
      <CrossGameDiaryTimeline
        events={SAMPLE_EVENTS}
        games={GAMES}
        players={PLAYERS}
        onFilterChange={() => undefined}
      />
    );
    expect(screen.getByRole('tablist')).toBeInTheDocument();
  });

  it('calls onFilterChange with selected filter id', async () => {
    const onFilterChange = vi.fn();
    render(
      <CrossGameDiaryTimeline
        events={SAMPLE_EVENTS}
        games={GAMES}
        players={PLAYERS}
        onFilterChange={onFilterChange}
      />
    );
    await userEvent.click(screen.getByRole('tab', { name: 'Score' }));
    expect(onFilterChange).toHaveBeenCalledWith('score');
  });

  it('marks active filter tab via aria-selected', () => {
    render(
      <CrossGameDiaryTimeline
        events={SAMPLE_EVENTS}
        games={GAMES}
        players={PLAYERS}
        filter="turn"
        onFilterChange={() => undefined}
      />
    );
    const turnTab = screen.getByRole('tab', { name: 'Turn' });
    expect(turnTab).toHaveAttribute('aria-selected', 'true');
    const scoreTab = screen.getByRole('tab', { name: 'Score' });
    expect(scoreTab).toHaveAttribute('aria-selected', 'false');
  });

  it('renders player avatars when actors present', () => {
    render(<CrossGameDiaryTimeline events={SAMPLE_EVENTS} games={GAMES} players={PLAYERS} />);
    // MR appears once (e1), DC appears twice (e2 end-event + e4 score-event)
    expect(screen.getByLabelText('Player MR')).toBeInTheDocument();
    expect(screen.getAllByLabelText('Player DC')).toHaveLength(2);
  });

  it('skips unknown actor ids gracefully', () => {
    const eventWithUnknownActor: DiaryEvent = {
      id: 'eX',
      time: '00:00',
      gameId: 'g1',
      kind: 'turn',
      icon: '🎯',
      actors: ['unknown-player', 'p1'],
      text: 'Unknown actor test',
    };
    render(
      <CrossGameDiaryTimeline events={[eventWithUnknownActor]} games={GAMES} players={PLAYERS} />
    );
    expect(screen.getByLabelText('Player MR')).toBeInTheDocument();
    expect(screen.queryByLabelText('Player unknown-player')).toBeNull();
  });

  it('has role="log" with aria-live="polite" for live updates', () => {
    render(<CrossGameDiaryTimeline events={SAMPLE_EVENTS} games={GAMES} players={PLAYERS} />);
    const log = screen.getByRole('log');
    expect(log).toHaveAttribute('aria-live', 'polite');
    expect(log).toHaveAttribute('aria-label', 'Timeline diary cross-game');
  });

  it('compact mode reduces gap spacing', () => {
    const { container, rerender } = render(
      <CrossGameDiaryTimeline events={SAMPLE_EVENTS} games={GAMES} players={PLAYERS} />
    );
    const logDefault = container.querySelector('[role="log"]');
    expect(logDefault?.className).toContain('gap-2');

    rerender(
      <CrossGameDiaryTimeline events={SAMPLE_EVENTS} games={GAMES} players={PLAYERS} compact />
    );
    const logCompact = container.querySelector('[role="log"]');
    expect(logCompact?.className).toContain('gap-1.5');
  });

  it('accepts custom className override', () => {
    const { container } = render(
      <CrossGameDiaryTimeline
        events={SAMPLE_EVENTS}
        games={GAMES}
        players={PLAYERS}
        className="custom-class"
      />
    );
    expect((container.firstChild as HTMLElement).className).toContain('custom-class');
  });
});
