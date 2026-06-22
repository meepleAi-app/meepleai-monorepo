import { describe, expect, it, vi } from 'vitest';
import { render, screen, within } from '@testing-library/react';
import userEvent from '@testing-library/user-event';
import {
  NightSummaryView,
  type NightSummaryViewProps,
  type NightSummaryNight,
  type NightSummaryMVP,
  type NightSummaryPhoto,
} from './NightSummaryView';
import type { PerGameRecapGame } from './PerGameRecapRow';

const NIGHT: NightSummaryNight = {
  title: 'Sabato boardgame con i Padovani',
  dateLine: 'sabato 17 maggio 2026',
  location: 'Casa Marco · Padova',
  startedAt: '21:00',
  endedAt: '03:15',
  duration: '6h 15m',
  nightCode: '#GN-042',
};

const MVP: NightSummaryMVP = {
  id: 'p-davide',
  name: 'Davide',
  initials: 'DC',
  color: 200,
  achievements: '1 vittoria · 11 eventi · top scorer Brass',
};

const GAMES: ReadonlyArray<PerGameRecapGame> = [
  {
    id: 'gs-brass-1',
    sessionId: 's-brass-may17',
    title: 'Brass: Birmingham',
    emoji: '🏭',
    cover: ['hsl(220 35% 28%)', 'hsl(28 60% 38%)'],
    order: 1,
    duration: '2h 45m',
    eventsCount: 11,
    winner: { id: 'p-davide', name: 'Davide', initials: 'DC', color: 200, score: 178 },
  },
  {
    id: 'gs-spirit-1',
    sessionId: 's-spirit-may17',
    title: 'Spirit Island',
    emoji: '🌋',
    cover: ['hsl(210 50% 30%)', 'hsl(150 50% 38%)'],
    order: 2,
    duration: '1h 50m',
    eventsCount: 9,
    coopMode: true,
    coopOutcome: 'round 5/6 · adversary England 1',
  },
  {
    id: 'gs-wing-1',
    sessionId: 's-wing-may17',
    title: 'Wingspan',
    emoji: '🦜',
    cover: ['hsl(85 40% 45%)', 'hsl(35 60% 50%)'],
    order: 3,
    duration: '1h 25m',
    eventsCount: 8,
    winner: { id: 'p-giulia', name: 'Giulia', initials: 'GM', color: 10, score: 92 },
  },
];

const PHOTOS: ReadonlyArray<NightSummaryPhoto> = [
  { id: 'ph-1', label: 'Setup', gradient: ['hsl(220 35% 30%)', 'hsl(28 60% 40%)'] },
  { id: 'ph-2', label: 'Brindisi', gradient: ['hsl(38 80% 55%)', 'hsl(10 70% 50%)'] },
];

const baseProps: NightSummaryViewProps = {
  night: NIGHT,
  mvp: MVP,
  games: GAMES,
  eventsCount: 28,
  photos: PHOTOS,
};

describe('NightSummaryView', () => {
  describe('hero', () => {
    it('renders title + date + location', () => {
      render(<NightSummaryView {...baseProps} />);
      expect(screen.getByText('Sabato boardgame con i Padovani')).toBeInTheDocument();
      expect(screen.getByText(/sabato 17 maggio 2026/)).toBeInTheDocument();
      expect(screen.getByText(/Casa Marco · Padova/)).toBeInTheDocument();
    });

    it('renders night code and games count badge', () => {
      render(<NightSummaryView {...baseProps} />);
      expect(screen.getByText(/#GN-042 · 3 giochi/)).toBeInTheDocument();
    });

    it('renders "Serata completata" status', () => {
      render(<NightSummaryView {...baseProps} />);
      expect(screen.getByText('Serata completata')).toBeInTheDocument();
    });

    it('renders MVP banner when mvp provided', () => {
      render(<NightSummaryView {...baseProps} />);
      expect(screen.getByText('MVP della serata')).toBeInTheDocument();
      expect(screen.getByText('Davide')).toBeInTheDocument();
      expect(screen.getByLabelText('MVP avatar DC')).toBeInTheDocument();
      expect(screen.getByText('1 vittoria · 11 eventi · top scorer Brass')).toBeInTheDocument();
    });

    it('omits MVP banner when mvp=null', () => {
      render(<NightSummaryView {...baseProps} mvp={null} />);
      expect(screen.queryByText('MVP della serata')).toBeNull();
    });
  });

  describe('KPI grid', () => {
    it('renders 4 KPI cards', () => {
      render(<NightSummaryView {...baseProps} />);
      expect(screen.getByText('Sessioni totali')).toBeInTheDocument();
      expect(screen.getByText('Durata totale')).toBeInTheDocument();
      expect(screen.getByText('Eventi diary')).toBeInTheDocument();
      expect(screen.getByText('Winner per gioco')).toBeInTheDocument();
    });

    it('renders games count as Sessioni totali value', () => {
      const stats = render(<NightSummaryView {...baseProps} />);
      const sessionsCard = stats.container.querySelector('.font-display.text-\\[28px\\]');
      expect(sessionsCard?.textContent).toBe('3');
    });

    it('renders durata totale value (also appears in hero date line)', () => {
      render(<NightSummaryView {...baseProps} />);
      // "6h 15m" appears in 2 places: hero date row + KPI Durata totale card
      expect(screen.getAllByText('6h 15m').length).toBe(2);
    });

    it('renders Winner per gioco with coop split sub', () => {
      render(<NightSummaryView {...baseProps} />);
      expect(screen.getByText('3/3')).toBeInTheDocument(); // all games have winner or coop
      expect(screen.getByText('1 coop · 2 competitive')).toBeInTheDocument();
    });

    it('renders "tutti competitive" sub when no coop games', () => {
      const noCoopGames: ReadonlyArray<PerGameRecapGame> = GAMES.filter(g => !g.coopMode);
      render(<NightSummaryView {...baseProps} games={noCoopGames} />);
      expect(screen.getByText('tutti competitive')).toBeInTheDocument();
    });

    it('renders eventi diary avg / session', () => {
      render(<NightSummaryView {...baseProps} />);
      // 28 / 3 = 9.3
      expect(screen.getByText(/9\.3 avg \/ session/)).toBeInTheDocument();
    });
  });

  describe('per-game recap', () => {
    it('renders all games with title', () => {
      render(<NightSummaryView {...baseProps} />);
      const recapSection = screen.getByLabelText('Per-game recap');
      expect(within(recapSection).getByText('Brass: Birmingham')).toBeInTheDocument();
      expect(within(recapSection).getByText('Spirit Island')).toBeInTheDocument();
      expect(within(recapSection).getByText('Wingspan')).toBeInTheDocument();
    });

    it('renders "3 giochi completati · in ordine cronologico" when multiple', () => {
      render(<NightSummaryView {...baseProps} />);
      expect(screen.getByText('3 giochi completati · in ordine cronologico')).toBeInTheDocument();
    });

    it('renders "1 gioco completato" when single', () => {
      render(<NightSummaryView {...baseProps} games={[GAMES[0]]} />);
      expect(screen.getByText('1 gioco completato')).toBeInTheDocument();
    });

    it('forwards onJumpToSession to PerGameRecapRow', async () => {
      const onJumpToSession = vi.fn();
      render(<NightSummaryView {...baseProps} onJumpToSession={onJumpToSession} />);
      const btns = screen.getAllByRole('button', { name: /Vai a session/ });
      await userEvent.click(btns[0]);
      expect(onJumpToSession).toHaveBeenCalledWith('s-brass-may17');
    });
  });

  describe('photo gallery', () => {
    it('renders "N foto caricate" with N>0', () => {
      render(<NightSummaryView {...baseProps} />);
      expect(screen.getByText('2 foto caricate')).toBeInTheDocument();
    });

    it('renders + Aggiungi foto button when photos exist + onAddPhoto provided', () => {
      render(<NightSummaryView {...baseProps} onAddPhoto={() => undefined} />);
      expect(screen.getByRole('button', { name: /\+ Aggiungi foto/ })).toBeInTheDocument();
    });

    it('renders "Gallery vuota" + add-first-photo button when no photos', () => {
      render(<NightSummaryView {...baseProps} photos={[]} onAddPhoto={() => undefined} />);
      expect(screen.getByText('Gallery vuota')).toBeInTheDocument();
      expect(screen.getByRole('button', { name: /\+ Aggiungi prima foto/ })).toBeInTheDocument();
    });

    it('omits add-first-photo button when onAddPhoto missing', () => {
      render(<NightSummaryView {...baseProps} photos={[]} />);
      expect(screen.getByText('Gallery vuota')).toBeInTheDocument();
      expect(screen.queryByRole('button', { name: /\+ Aggiungi prima foto/ })).toBeNull();
    });
  });

  describe('archived banner', () => {
    it('does not render banner when archived=false', () => {
      render(<NightSummaryView {...baseProps} />);
      expect(screen.queryByText('Serata archiviata')).toBeNull();
    });

    it('renders ArchivedBanner when archived=true', () => {
      render(<NightSummaryView {...baseProps} archived />);
      expect(screen.getByText('Serata archiviata')).toBeInTheDocument();
    });

    it('renders "Torna alla lista" CTA when archived + onGoToList', () => {
      const onGoToList = vi.fn();
      render(<NightSummaryView {...baseProps} archived onGoToList={onGoToList} />);
      const btn = screen.getByRole('button', { name: /Torna alla lista/ });
      expect(btn).toBeInTheDocument();
    });

    it('switches Archivia↔Disarchivia footer CTA based on archived state', () => {
      const { rerender } = render(<NightSummaryView {...baseProps} />);
      expect(screen.getByRole('button', { name: /Archivia/ })).toBeInTheDocument();
      expect(screen.queryByRole('button', { name: /Disarchivia/ })).toBeNull();

      rerender(<NightSummaryView {...baseProps} archived />);
      expect(screen.getByRole('button', { name: /Disarchivia/ })).toBeInTheDocument();
    });

    it('top-level data-archived reflects state', () => {
      const { container, rerender } = render(<NightSummaryView {...baseProps} />);
      expect(container.firstChild).toHaveAttribute('data-archived', 'false');
      rerender(<NightSummaryView {...baseProps} archived />);
      expect(container.firstChild).toHaveAttribute('data-archived', 'true');
    });
  });

  describe('footer CTAs', () => {
    it('invokes onShare on "Condividi riepilogo" click', async () => {
      const onShare = vi.fn();
      render(<NightSummaryView {...baseProps} onShare={onShare} />);
      await userEvent.click(screen.getByRole('button', { name: /Condividi riepilogo/ }));
      expect(onShare).toHaveBeenCalledTimes(1);
    });

    it('invokes onArchive when not-archived + click Archivia', async () => {
      const onArchive = vi.fn();
      render(<NightSummaryView {...baseProps} onArchive={onArchive} />);
      await userEvent.click(screen.getByRole('button', { name: /^Archivia$/ }));
      expect(onArchive).toHaveBeenCalledTimes(1);
    });

    it('invokes onUnarchive when archived + click Disarchivia', async () => {
      const onUnarchive = vi.fn();
      render(<NightSummaryView {...baseProps} archived onUnarchive={onUnarchive} />);
      await userEvent.click(screen.getByRole('button', { name: /Disarchivia/ }));
      expect(onUnarchive).toHaveBeenCalledTimes(1);
    });
  });

  describe('share success toast', () => {
    it('does not render toast when shareSuccess.visible=false', () => {
      render(<NightSummaryView {...baseProps} shareSuccess={{ visible: false }} />);
      expect(screen.queryByText('Link copiato')).toBeNull();
    });

    it('renders toast with custom subline when shareSuccess.visible=true', () => {
      render(
        <NightSummaryView
          {...baseProps}
          shareSuccess={{ visible: true, subline: 'meepleai.app/s/gn-042' }}
        />
      );
      expect(screen.getByText('Link copiato')).toBeInTheDocument();
      expect(screen.getByText('meepleai.app/s/gn-042')).toBeInTheDocument();
    });
  });

  it('accepts custom className override', () => {
    const { container } = render(
      <NightSummaryView {...baseProps} className="custom-summary-class" />
    );
    expect(container.firstChild).toHaveClass('custom-summary-class');
  });

  it('data-mobile reflects mobile prop', () => {
    const { container, rerender } = render(<NightSummaryView {...baseProps} />);
    expect(container.firstChild).toHaveAttribute('data-mobile', 'false');
    rerender(<NightSummaryView {...baseProps} mobile />);
    expect(container.firstChild).toHaveAttribute('data-mobile', 'true');
  });
});
