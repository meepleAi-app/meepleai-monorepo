import { describe, expect, it, vi } from 'vitest';
import { render, screen, within } from '@testing-library/react';
import userEvent from '@testing-library/user-event';
import {
  NightLiveHub,
  type NightLiveHubProps,
  type NightLiveHubNight,
  type NightLiveHubCurrentGame,
} from './NightLiveHub';
import type { PlannedGame } from './PlannedGamesPane';
import type { DiaryEvent, DiaryGameRef, DiaryPlayerRef } from './CrossGameDiaryTimeline';

const NIGHT: NightLiveHubNight = {
  title: 'Sabato boardgame con i Padovani',
  shortTitle: 'Padovani · 17 mag',
  nightCode: '#GN-042',
};

const CURRENT_GAME: NightLiveHubCurrentGame = {
  id: 'gs-spirit-1',
  sessionId: 's-spirit-may17',
  title: 'Spirit Island',
  emoji: '🌋',
  cover: ['hsl(210 50% 30%)', 'hsl(150 50% 38%)'],
  score: 'round 2',
};

const PLANNED_GAMES: ReadonlyArray<PlannedGame> = [
  {
    id: 'gs-brass-1',
    title: 'Brass: Birmingham',
    emoji: '🏭',
    cover: ['hsl(220 35% 28%)', 'hsl(28 60% 38%)'],
    status: 'completed',
    order: 1,
    actual: '113m',
    winner: { name: 'Davide', initials: 'DC', color: 200 },
  },
  {
    id: 'gs-spirit-1',
    title: 'Spirit Island',
    emoji: '🌋',
    cover: ['hsl(210 50% 30%)', 'hsl(150 50% 38%)'],
    status: 'inprogress',
    order: 2,
    actual: '35m',
  },
  {
    id: 'gs-wing-1',
    title: 'Wingspan',
    emoji: '🦜',
    cover: ['hsl(85 40% 45%)', 'hsl(35 60% 50%)'],
    status: 'upcoming',
    order: 3,
    estimated: '60m',
  },
];

const DIARY_GAMES: ReadonlyArray<DiaryGameRef> = [
  { id: 'gs-brass-1', title: 'Brass', emoji: '🏭' },
];

const DIARY_PLAYERS: ReadonlyArray<DiaryPlayerRef> = [{ id: 'p1', initials: 'MR', color: 262 }];

const DIARY_EVENTS: ReadonlyArray<DiaryEvent> = [
  {
    id: 'd01',
    time: '21:02',
    gameId: 'gs-brass-1',
    kind: 'turn',
    icon: '🎯',
    actors: ['p1'],
    text: 'Marco apre il Canal Era',
  },
];

const baseProps: NightLiveHubProps = {
  night: NIGHT,
  current: 2,
  total: 3,
  elapsed: '23m',
  plannedGames: PLANNED_GAMES,
  currentGame: CURRENT_GAME,
  diaryEvents: DIARY_EVENTS,
  diaryGames: DIARY_GAMES,
  diaryPlayers: DIARY_PLAYERS,
};

describe('NightLiveHub', () => {
  describe('desktop layout (default)', () => {
    it('renders night title in top bar', () => {
      render(<NightLiveHub {...baseProps} />);
      expect(screen.getByText('Sabato boardgame con i Padovani')).toBeInTheDocument();
    });

    it('renders nightCode badge when provided', () => {
      render(<NightLiveHub {...baseProps} />);
      expect(screen.getByText('#GN-042')).toBeInTheDocument();
    });

    it('renders 3-pane layout (planned + current + diary)', () => {
      render(<NightLiveHub {...baseProps} />);
      expect(screen.getByLabelText('Planned games')).toBeInTheDocument();
      expect(screen.getByLabelText('Current game')).toBeInTheDocument();
      expect(screen.getByLabelText('Cross-game diary')).toBeInTheDocument();
    });

    it('renders desktop counter Game X/Y · elapsed', () => {
      render(<NightLiveHub {...baseProps} />);
      expect(screen.getByText('2')).toBeInTheDocument();
      expect(screen.getByText('/3')).toBeInTheDocument();
      expect(screen.getByText('23m')).toBeInTheDocument();
    });

    it('renders player counter when both props provided', () => {
      render(<NightLiveHub {...baseProps} confirmedPlayers={5} totalPlayers={6} />);
      expect(screen.getByText('5/6')).toBeInTheDocument();
      // "Player" label appears in both desktop counter and mobile compact strip
      expect(screen.getAllByText('Player').length).toBeGreaterThanOrEqual(1);
    });

    it('omits player counter when totalPlayers missing', () => {
      render(<NightLiveHub {...baseProps} confirmedPlayers={5} />);
      expect(screen.queryByText('5/6')).toBeNull();
    });

    it('does NOT render mobile bottom tabs', () => {
      render(<NightLiveHub {...baseProps} />);
      expect(screen.queryByRole('tablist', { name: 'Hub mobile tabs' })).toBeNull();
    });
  });

  describe('mobile layout', () => {
    it('renders bottom tab navigation (3 tabs)', () => {
      render(<NightLiveHub {...baseProps} mobile />);
      const tablist = screen.getByRole('tablist', { name: 'Hub mobile tabs' });
      expect(tablist).toBeInTheDocument();
      expect(screen.getByRole('tab', { name: /Current/ })).toBeInTheDocument();
      expect(screen.getByRole('tab', { name: /Planned/ })).toBeInTheDocument();
      expect(screen.getByRole('tab', { name: /Diary/ })).toBeInTheDocument();
    });

    it('starts with current tab active by default', () => {
      render(<NightLiveHub {...baseProps} mobile />);
      expect(screen.getByRole('tab', { name: /Current/ })).toHaveAttribute('aria-selected', 'true');
      // Current game card visible
      expect(screen.getByLabelText('Current game')).toBeInTheDocument();
    });

    it('respects initialMobileTab prop', () => {
      render(<NightLiveHub {...baseProps} mobile initialMobileTab="planned" />);
      expect(screen.getByRole('tab', { name: /Planned/ })).toHaveAttribute('aria-selected', 'true');
      expect(screen.getByLabelText('Planned games')).toBeInTheDocument();
    });

    it('switches tabs on click', async () => {
      render(<NightLiveHub {...baseProps} mobile />);
      // initially "Current" is active
      expect(screen.getByLabelText('Current game')).toBeInTheDocument();

      await userEvent.click(screen.getByRole('tab', { name: /Diary/ }));
      // diary text visible
      expect(screen.getByText('Marco apre il Canal Era')).toBeInTheDocument();
      // current game card no longer rendered
      expect(screen.queryByLabelText('Current game')).toBeNull();
    });

    it('renders short title in compact mode', () => {
      render(<NightLiveHub {...baseProps} mobile />);
      expect(screen.getByText('Padovani · 17 mag')).toBeInTheDocument();
    });

    it('falls back to full title in compact when shortTitle missing', () => {
      const nightNoShort: NightLiveHubNight = { title: 'Full title only' };
      render(<NightLiveHub {...baseProps} night={nightNoShort} mobile />);
      expect(screen.getByText('Full title only')).toBeInTheDocument();
    });
  });

  describe('status variants', () => {
    it('shows "Live" badge by default with pulsing dot', () => {
      render(<NightLiveHub {...baseProps} />);
      expect(screen.getByLabelText('Status: Live')).toBeInTheDocument();
    });

    it('shows "In pausa" badge with ⏸ icon when status=paused', () => {
      render(<NightLiveHub {...baseProps} status="paused" />);
      expect(screen.getByLabelText('Status: In pausa')).toBeInTheDocument();
      // pause icon span ⏸ is decorative
      expect(screen.getByText('⏸')).toBeInTheDocument();
    });

    it('shows "Transition" badge when status=transition', () => {
      render(<NightLiveHub {...baseProps} status="transition" />);
      expect(screen.getByLabelText('Status: Transition')).toBeInTheDocument();
    });

    it('toolbar button shows "Pausa" when status=live', () => {
      render(<NightLiveHub {...baseProps} />);
      expect(screen.getByRole('button', { name: /Pausa/ })).toBeInTheDocument();
    });

    it('toolbar button shows "Riprendi" when status=paused', () => {
      render(<NightLiveHub {...baseProps} status="paused" />);
      expect(screen.getByRole('button', { name: /Riprendi/ })).toBeInTheDocument();
    });

    it('top-level data-night-status reflects current status', () => {
      const { container, rerender } = render(<NightLiveHub {...baseProps} />);
      expect(container.firstChild).toHaveAttribute('data-night-status', 'live');
      rerender(<NightLiveHub {...baseProps} status="paused" />);
      expect(container.firstChild).toHaveAttribute('data-night-status', 'paused');
    });
  });

  describe('toolbar callbacks', () => {
    it('invokes onPauseToggle on Pausa click', async () => {
      const onPauseToggle = vi.fn();
      render(<NightLiveHub {...baseProps} onPauseToggle={onPauseToggle} />);
      await userEvent.click(screen.getByRole('button', { name: /Pausa/ }));
      expect(onPauseToggle).toHaveBeenCalledTimes(1);
    });

    it('invokes onTransition on Transition click', async () => {
      const onTransition = vi.fn();
      render(<NightLiveHub {...baseProps} onTransition={onTransition} />);
      await userEvent.click(screen.getByRole('button', { name: /Transition/ }));
      expect(onTransition).toHaveBeenCalledTimes(1);
    });

    it('invokes onEnd on End click', async () => {
      const onEnd = vi.fn();
      render(<NightLiveHub {...baseProps} onEnd={onEnd} />);
      await userEvent.click(screen.getByRole('button', { name: /^End$/ }));
      expect(onEnd).toHaveBeenCalledTimes(1);
    });

    it('renders back button only when onBack provided', () => {
      const { rerender } = render(<NightLiveHub {...baseProps} />);
      expect(screen.queryByRole('button', { name: 'Indietro' })).toBeNull();

      rerender(<NightLiveHub {...baseProps} onBack={() => undefined} />);
      expect(screen.getByRole('button', { name: 'Indietro' })).toBeInTheDocument();
    });
  });

  describe('CurrentGameCard', () => {
    it('renders current game title + sessionId', () => {
      render(<NightLiveHub {...baseProps} />);
      const currentGameSection = screen.getByLabelText('Current game');
      // Spirit Island title and sessionId rendered inside CurrentGameCard region
      // (Spirit Island also appears in PlannedGamesPane row, so we scope to the region)
      expect(within(currentGameSection).getByText('Spirit Island')).toBeInTheDocument();
      expect(within(currentGameSection).getByText('s-spirit-may17')).toBeInTheDocument();
    });

    it('renders score pill when score provided', () => {
      render(<NightLiveHub {...baseProps} />);
      expect(screen.getByText('round 2')).toBeInTheDocument();
    });

    it('invokes onJumpToSession with sessionId', async () => {
      const onJumpToSession = vi.fn();
      render(<NightLiveHub {...baseProps} onJumpToSession={onJumpToSession} />);
      await userEvent.click(screen.getByRole('button', { name: /Apri sessione live/ }));
      expect(onJumpToSession).toHaveBeenCalledWith('s-spirit-may17');
    });

    it('renders empty state when currentGame=null', () => {
      render(<NightLiveHub {...baseProps} currentGame={null} />);
      expect(screen.getByText('Nessun gioco corrente · in attesa transition')).toBeInTheDocument();
    });
  });

  describe('AutoSaveToast integration', () => {
    it('renders toast when autoSaveToast.visible=true', () => {
      render(
        <NightLiveHub {...baseProps} autoSaveToast={{ visible: true, timestamp: '23:35:42' }} />
      );
      expect(screen.getByText('Auto-salvato')).toBeInTheDocument();
      expect(screen.getByText('23:35:42 · prossimo tra 60s')).toBeInTheDocument();
    });

    it('does not render toast when autoSaveToast.visible=false', () => {
      render(
        <NightLiveHub {...baseProps} autoSaveToast={{ visible: false, timestamp: '23:35:42' }} />
      );
      expect(screen.queryByText('Auto-salvato')).toBeNull();
    });

    it('does not render toast when autoSaveToast prop missing', () => {
      render(<NightLiveHub {...baseProps} />);
      expect(screen.queryByText('Auto-salvato')).toBeNull();
    });
  });

  it('accepts custom className override', () => {
    const { container } = render(<NightLiveHub {...baseProps} className="custom-hub-class" />);
    expect(container.firstChild).toHaveClass('custom-hub-class');
  });

  it('forwards onAddGame to PlannedGamesPane', async () => {
    const onAddGame = vi.fn();
    render(<NightLiveHub {...baseProps} onAddGame={onAddGame} />);
    await userEvent.click(screen.getByRole('button', { name: /Aggiungi gioco/ }));
    expect(onAddGame).toHaveBeenCalledTimes(1);
  });
});
