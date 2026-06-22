import { describe, expect, it, vi } from 'vitest';
import { render, screen } from '@testing-library/react';
import userEvent from '@testing-library/user-event';
import { PlannedGamesPane, type PlannedGame } from './PlannedGamesPane';

const SAMPLE_GAMES: ReadonlyArray<PlannedGame> = [
  {
    id: 'gs-brass-1',
    title: 'Brass: Birmingham',
    publisher: 'Roxley',
    emoji: '🏭',
    cover: ['hsl(220 35% 28%)', 'hsl(28 60% 38%)'],
    status: 'completed',
    order: 1,
    actual: '113m',
    estimated: '120m',
    score: '178–142',
    winner: { name: 'Davide', initials: 'DC', color: 200 },
  },
  {
    id: 'gs-spirit-1',
    title: 'Spirit Island',
    publisher: 'GMT',
    emoji: '🌋',
    cover: ['hsl(210 50% 30%)', 'hsl(150 50% 38%)'],
    status: 'inprogress',
    order: 2,
    actual: '35m elapsed',
    estimated: '90m',
    score: 'round 2',
  },
  {
    id: 'gs-wing-1',
    title: 'Wingspan',
    publisher: 'Stonemaier',
    emoji: '🦜',
    cover: ['hsl(85 40% 45%)', 'hsl(35 60% 50%)'],
    status: 'upcoming',
    order: 3,
    estimated: '60m',
  },
];

describe('PlannedGamesPane', () => {
  it('renders default "Planned (N)" title', () => {
    render(<PlannedGamesPane games={SAMPLE_GAMES} />);
    expect(screen.getByText(/Planned \(3\)/)).toBeInTheDocument();
  });

  it('renders custom title override', () => {
    render(<PlannedGamesPane games={SAMPLE_GAMES} title="Giochi" />);
    expect(screen.getByText(/Giochi \(3\)/)).toBeInTheDocument();
  });

  it('renders all game titles', () => {
    render(<PlannedGamesPane games={SAMPLE_GAMES} />);
    expect(screen.getByText('Brass: Birmingham')).toBeInTheDocument();
    expect(screen.getByText('Spirit Island')).toBeInTheDocument();
    expect(screen.getByText('Wingspan')).toBeInTheDocument();
  });

  it('renders empty state when no games', () => {
    render(<PlannedGamesPane games={[]} />);
    expect(screen.getByText('Nessun gioco pianificato')).toBeInTheDocument();
  });

  it('shows PlayOrder lock badge by default', () => {
    render(<PlannedGamesPane games={SAMPLE_GAMES} />);
    expect(screen.getByText(/PlayOrder/)).toBeInTheDocument();
  });

  it('omits PlayOrder lock when playOrderLocked=false', () => {
    render(<PlannedGamesPane games={SAMPLE_GAMES} playOrderLocked={false} />);
    expect(screen.queryByText(/PlayOrder/)).toBeNull();
  });

  it('renders status badges per row (completed/inprogress/upcoming)', () => {
    render(<PlannedGamesPane games={SAMPLE_GAMES} />);
    expect(screen.getByLabelText('Status: completed')).toBeInTheDocument();
    expect(screen.getByLabelText('Status: inprogress')).toBeInTheDocument();
    expect(screen.getByLabelText('Status: upcoming')).toBeInTheDocument();
  });

  it('renders FINITO + actual time for completed games', () => {
    render(<PlannedGamesPane games={SAMPLE_GAMES} />);
    expect(screen.getByText('FINITO')).toBeInTheDocument();
    expect(screen.getByText('113m')).toBeInTheDocument();
  });

  it('renders LIVE + actual for inprogress games', () => {
    render(<PlannedGamesPane games={SAMPLE_GAMES} />);
    expect(screen.getByText('LIVE')).toBeInTheDocument();
    expect(screen.getByText('35m elapsed')).toBeInTheDocument();
  });

  it('renders UPCOMING + estimated for upcoming games', () => {
    render(<PlannedGamesPane games={SAMPLE_GAMES} />);
    expect(screen.getByText('UPCOMING')).toBeInTheDocument();
    expect(screen.getByText('est. 60m')).toBeInTheDocument();
  });

  it('renders winner banner for completed game when winner present', () => {
    render(<PlannedGamesPane games={SAMPLE_GAMES} />);
    expect(screen.getByText('🏆 Davide ha vinto')).toBeInTheDocument();
    expect(screen.getByLabelText('Winner DC')).toBeInTheDocument();
    expect(screen.getByText('178–142')).toBeInTheDocument();
  });

  it('omits winner banner when winner missing', () => {
    const noWinnerGame: PlannedGame = {
      id: 'g-x',
      title: 'X',
      status: 'completed',
      order: 1,
      actual: '60m',
    };
    render(<PlannedGamesPane games={[noWinnerGame]} />);
    expect(screen.queryByText(/ha vinto/)).toBeNull();
  });

  it('compact=true removes right border + uses full width', () => {
    const { container, rerender } = render(<PlannedGamesPane games={SAMPLE_GAMES} />);
    const aside = container.querySelector('aside');
    expect(aside?.className).toContain('w-[280px]');
    expect(aside?.className).toContain('border-r');

    rerender(<PlannedGamesPane games={SAMPLE_GAMES} compact />);
    const asideCompact = container.querySelector('aside');
    expect(asideCompact?.className).toContain('w-full');
    expect(asideCompact?.className).not.toContain('border-r');
  });

  it('add game button disabled when onAddGame missing', () => {
    render(<PlannedGamesPane games={SAMPLE_GAMES} />);
    const btn = screen.getByRole('button', { name: /Aggiungi gioco/ });
    expect(btn).toBeDisabled();
  });

  it('add game button enabled + invokes callback when onAddGame provided', async () => {
    const onAddGame = vi.fn();
    render(<PlannedGamesPane games={SAMPLE_GAMES} onAddGame={onAddGame} />);
    const btn = screen.getByRole('button', { name: /Aggiungi gioco/ });
    expect(btn).not.toBeDisabled();
    await userEvent.click(btn);
    expect(onAddGame).toHaveBeenCalledTimes(1);
  });

  it('renders #order · publisher in subline', () => {
    render(<PlannedGamesPane games={SAMPLE_GAMES} />);
    expect(screen.getByText('#1 · Roxley')).toBeInTheDocument();
    expect(screen.getByText('#2 · GMT')).toBeInTheDocument();
  });

  it('falls back to #order only when publisher missing', () => {
    const noPub: PlannedGame = {
      id: 'g-x',
      title: 'X',
      status: 'upcoming',
      order: 5,
      estimated: '30m',
    };
    render(<PlannedGamesPane games={[noPub]} />);
    expect(screen.getByText('#5')).toBeInTheDocument();
  });

  it('accepts custom className override', () => {
    const { container } = render(
      <PlannedGamesPane games={SAMPLE_GAMES} className="custom-class" />
    );
    expect(container.querySelector('aside')?.className).toContain('custom-class');
  });

  it('inprogress row has session-tinted bg + border', () => {
    const { container } = render(<PlannedGamesPane games={SAMPLE_GAMES} />);
    const inprogressRow = container.querySelector('[data-status="inprogress"]');
    expect(inprogressRow?.className).toContain('bg-entity-session');
    expect(inprogressRow?.className).toContain('border-entity-session');
  });

  it('completed row has reduced opacity (.80)', () => {
    const { container } = render(<PlannedGamesPane games={SAMPLE_GAMES} />);
    const completedRow = container.querySelector('[data-status="completed"]');
    expect(completedRow?.className).toContain('opacity-80');
  });
});
