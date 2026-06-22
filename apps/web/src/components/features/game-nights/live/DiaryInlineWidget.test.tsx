import { describe, expect, it, vi } from 'vitest';
import { render, screen } from '@testing-library/react';
import userEvent from '@testing-library/user-event';
import { DiaryInlineWidget } from './DiaryInlineWidget';
import type { DiaryEvent, DiaryGameRef, DiaryPlayerRef } from './CrossGameDiaryTimeline';

const GAMES: ReadonlyArray<DiaryGameRef> = [
  { id: 'g1', title: 'Brass', emoji: '🏭' },
  { id: 'g2', title: 'Spirit Island', emoji: '🌋' },
];
const PLAYERS: ReadonlyArray<DiaryPlayerRef> = [{ id: 'p1', initials: 'MR', color: 262 }];

const makeEvents = (count: number, gameId: string | null = 'g1'): DiaryEvent[] =>
  Array.from({ length: count }, (_, i) => ({
    id: `e${i + 1}`,
    time: `${20 + i}:00`,
    gameId,
    kind: 'turn' as const,
    icon: '🎯',
    actors: ['p1'],
    text: `Evento ${i + 1}`,
  }));

describe('DiaryInlineWidget', () => {
  it('renders header with default "Diary cross-game" kicker', () => {
    render(<DiaryInlineWidget events={makeEvents(3)} games={GAMES} players={PLAYERS} />);
    expect(screen.getByText('Diary cross-game')).toBeInTheDocument();
  });

  it('renders event count + sessions count in subheader', () => {
    const mixedGameEvents = [...makeEvents(3, 'g1'), ...makeEvents(2, 'g2')];
    render(<DiaryInlineWidget events={mixedGameEvents} games={GAMES} players={PLAYERS} />);
    expect(screen.getByText('5 eventi · 2 session')).toBeInTheDocument();
  });

  it('uses singular "evento" when count is 1', () => {
    render(<DiaryInlineWidget events={makeEvents(1)} games={GAMES} players={PLAYERS} />);
    expect(screen.getByText(/1 evento ·/)).toBeInTheDocument();
  });

  it('overrides sessionsCount when prop provided', () => {
    render(
      <DiaryInlineWidget
        events={makeEvents(3)}
        games={GAMES}
        players={PLAYERS}
        sessionsCount={42}
      />
    );
    expect(screen.getByText('3 eventi · 42 session')).toBeInTheDocument();
  });

  it('slices to last maxEvents (default 7)', () => {
    render(<DiaryInlineWidget events={makeEvents(10)} games={GAMES} players={PLAYERS} />);
    // Events 1-3 hidden (only last 7 shown)
    expect(screen.queryByText('Evento 1')).toBeNull();
    expect(screen.queryByText('Evento 3')).toBeNull();
    expect(screen.getByText('Evento 4')).toBeInTheDocument();
    expect(screen.getByText('Evento 10')).toBeInTheDocument();
  });

  it('respects custom maxEvents', () => {
    render(
      <DiaryInlineWidget events={makeEvents(10)} games={GAMES} players={PLAYERS} maxEvents={3} />
    );
    expect(screen.queryByText('Evento 7')).toBeNull();
    expect(screen.getByText('Evento 8')).toBeInTheDocument();
    expect(screen.getByText('Evento 10')).toBeInTheDocument();
  });

  it('maxEvents=0 disables slicing (shows all)', () => {
    render(
      <DiaryInlineWidget events={makeEvents(10)} games={GAMES} players={PLAYERS} maxEvents={0} />
    );
    expect(screen.getByText('Evento 1')).toBeInTheDocument();
    expect(screen.getByText('Evento 10')).toBeInTheDocument();
  });

  it('renders Live badge with pulsing dot when isLive', () => {
    render(<DiaryInlineWidget events={makeEvents(1)} games={GAMES} players={PLAYERS} isLive />);
    expect(screen.getByLabelText('Live')).toBeInTheDocument();
  });

  it('omits Live badge when isLive=false (default)', () => {
    render(<DiaryInlineWidget events={makeEvents(1)} games={GAMES} players={PLAYERS} />);
    expect(screen.queryByLabelText('Live')).toBeNull();
  });

  it('renders Annota button when onAddNote provided + invokes callback', async () => {
    const onAddNote = vi.fn();
    render(
      <DiaryInlineWidget
        events={makeEvents(1)}
        games={GAMES}
        players={PLAYERS}
        onAddNote={onAddNote}
      />
    );
    await userEvent.click(screen.getByRole('button', { name: /\+ Annota/ }));
    expect(onAddNote).toHaveBeenCalledTimes(1);
  });

  it('renders Apri timeline as button when onOpenFullTimeline provided', async () => {
    const onOpen = vi.fn();
    render(
      <DiaryInlineWidget
        events={makeEvents(1)}
        games={GAMES}
        players={PLAYERS}
        onOpenFullTimeline={onOpen}
      />
    );
    await userEvent.click(screen.getByRole('button', { name: /Apri timeline/ }));
    expect(onOpen).toHaveBeenCalledTimes(1);
  });

  it('renders Apri timeline as static span when no callback (read-only)', () => {
    render(<DiaryInlineWidget events={makeEvents(1)} games={GAMES} players={PLAYERS} />);
    expect(screen.queryByRole('button', { name: /Apri timeline/ })).toBeNull();
    expect(screen.getByText(/Apri timeline/)).toBeInTheDocument();
  });

  it('embedded=true applies fixed size + shadow', () => {
    const { container } = render(
      <DiaryInlineWidget
        events={makeEvents(1)}
        games={GAMES}
        players={PLAYERS}
        embedded
        width={320}
        height={400}
      />
    );
    const widget = container.firstChild as HTMLElement;
    expect(widget.className).toContain('shadow-md');
    expect(widget.style.width).toBe('320px');
    expect(widget.style.height).toBe('400px');
  });

  it('embedded=false (default) uses 100% size, no shadow', () => {
    const { container } = render(
      <DiaryInlineWidget events={makeEvents(1)} games={GAMES} players={PLAYERS} />
    );
    const widget = container.firstChild as HTMLElement;
    expect(widget.className).not.toContain('shadow-md');
    expect(widget.style.width).toBe('100%');
  });

  it('accepts custom className override', () => {
    const { container } = render(
      <DiaryInlineWidget
        events={makeEvents(1)}
        games={GAMES}
        players={PLAYERS}
        className="custom-class"
      />
    );
    expect((container.firstChild as HTMLElement).className).toContain('custom-class');
  });

  it('does not show Annota button when onAddNote missing', () => {
    render(<DiaryInlineWidget events={makeEvents(1)} games={GAMES} players={PLAYERS} />);
    expect(screen.queryByRole('button', { name: /\+ Annota/ })).toBeNull();
  });
});
