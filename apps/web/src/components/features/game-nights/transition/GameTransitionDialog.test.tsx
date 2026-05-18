import { describe, expect, it, vi } from 'vitest';
import { render, screen, within } from '@testing-library/react';
import userEvent from '@testing-library/user-event';
import {
  GameTransitionDialog,
  type GameTransitionDialogProps,
  type TransitionLastGame,
  type TransitionNextGame,
} from './GameTransitionDialog';

const LAST_GAME: TransitionLastGame = {
  id: 'gs-brass-1',
  title: 'Brass: Birmingham',
  publisher: 'Roxley',
  emoji: '🏭',
  cover: ['hsl(220 35% 28%)', 'hsl(28 60% 38%)'],
  duration: '2h 45m',
  endedAt: '22:55',
  winner: { id: 'p-davide', name: 'Davide', initials: 'DC', color: 200, score: 178 },
  topThree: [
    {
      id: 'p-davide',
      name: 'Davide',
      initials: 'DC',
      color: 200,
      score: 178,
      delta: '+50 industria',
    },
    { id: 'p-marco', name: 'Marco', initials: 'MR', color: 262, score: 142, delta: '+12 network' },
    { id: 'p-giulia', name: 'Giulia', initials: 'GM', color: 10, score: 128, delta: '+8 carbone' },
  ],
};

const NEXT_GAME: TransitionNextGame = {
  id: 'gs-spirit-1',
  title: 'Spirit Island',
  publisher: 'GMT · co-op',
  emoji: '🌋',
  cover: ['hsl(210 50% 30%)', 'hsl(150 50% 38%)'],
  estimated: '90m',
  weight: 4.08,
  rules: [
    { icon: '🔁', text: 'Phase order · Growth → Fast → Slow', src: '§ 4.2' },
    { icon: '😱', text: 'Fear deck · 9 carte', src: '§ 5.1' },
  ],
  setup: [
    { icon: '🗺️', text: 'Tabellone', done: true },
    { icon: '🃏', text: 'Invader deck', done: false },
  ],
};

const baseProps: GameTransitionDialogProps = {
  open: true,
  lastGame: LAST_GAME,
  nextGame: NEXT_GAME,
};

describe('GameTransitionDialog', () => {
  describe('open prop', () => {
    it('returns null when open=false', () => {
      const { container } = render(<GameTransitionDialog {...baseProps} open={false} />);
      expect(container.firstChild).toBeNull();
    });

    it('renders dialog when open=true', () => {
      render(<GameTransitionDialog {...baseProps} />);
      expect(screen.getByRole('dialog')).toBeInTheDocument();
    });
  });

  describe('ARIA + accessibility', () => {
    it('has aria-modal="true" + aria-labelledby pointing to existing id', () => {
      render(<GameTransitionDialog {...baseProps} />);
      const dialog = screen.getByRole('dialog');
      expect(dialog).toHaveAttribute('aria-modal', 'true');
      const titleId = dialog.getAttribute('aria-labelledby');
      expect(titleId).toBeTruthy();
      // The element with that id must exist (avoid screen-reader naming bug like PR #1250 review)
      expect(document.getElementById(titleId!)).not.toBeNull();
    });

    it('renders Ultimo gioco + Prossimo gioco sections with aria-label', () => {
      render(<GameTransitionDialog {...baseProps} />);
      expect(screen.getByLabelText('Ultimo gioco')).toBeInTheDocument();
      expect(screen.getByLabelText('Prossimo gioco')).toBeInTheDocument();
    });

    it('renders close button only when onClose provided', () => {
      const { rerender } = render(<GameTransitionDialog {...baseProps} />);
      expect(screen.queryByRole('button', { name: 'Chiudi' })).toBeNull();

      rerender(<GameTransitionDialog {...baseProps} onClose={() => undefined} />);
      expect(screen.getByRole('button', { name: 'Chiudi' })).toBeInTheDocument();
    });
  });

  describe('RecapPanel (left col)', () => {
    it('renders last game title + publisher + endedAt', () => {
      render(<GameTransitionDialog {...baseProps} />);
      const recap = screen.getByLabelText('Ultimo gioco');
      expect(within(recap).getByText('Brass: Birmingham')).toBeInTheDocument();
      expect(within(recap).getByText(/Roxley/)).toBeInTheDocument();
      expect(within(recap).getByText(/22:55/)).toBeInTheDocument();
    });

    it('renders winner banner with score + duration kicker + winner avatar', () => {
      render(<GameTransitionDialog {...baseProps} />);
      const recap = screen.getByLabelText('Ultimo gioco');
      // "178pt" is unique to the winner banner (top-3 list shows just "178" without "pt")
      expect(within(recap).getByText('178pt')).toBeInTheDocument();
      expect(within(recap).getByText(/Winner · 2h 45m/)).toBeInTheDocument();
      expect(within(recap).getByLabelText('Winner avatar DC')).toBeInTheDocument();
    });

    it('renders top-3 list with rank + name + score', () => {
      render(<GameTransitionDialog {...baseProps} />);
      const recap = screen.getByLabelText('Ultimo gioco');
      expect(within(recap).getByLabelText('Rank 1')).toBeInTheDocument();
      expect(within(recap).getByLabelText('Rank 2')).toBeInTheDocument();
      expect(within(recap).getByLabelText('Rank 3')).toBeInTheDocument();
      expect(within(recap).getByText('178')).toBeInTheDocument();
      expect(within(recap).getByText('142')).toBeInTheDocument();
      expect(within(recap).getByText('128')).toBeInTheDocument();
    });

    it('renders delta info when provided', () => {
      render(<GameTransitionDialog {...baseProps} />);
      expect(screen.getByText('+50 industria')).toBeInTheDocument();
      expect(screen.getByText('+12 network')).toBeInTheDocument();
    });

    it('truncates to top-3 even when more players provided', () => {
      const moreThanThree: TransitionLastGame = {
        ...LAST_GAME,
        topThree: [
          ...LAST_GAME.topThree,
          { id: 'p-extra', name: 'Extra', initials: 'EX', color: 50, score: 100 },
        ],
      };
      render(<GameTransitionDialog {...baseProps} lastGame={moreThanThree} />);
      expect(screen.queryByText('Extra')).toBeNull();
    });

    it('omits top-3 list when empty', () => {
      const noTop: TransitionLastGame = { ...LAST_GAME, topThree: [] };
      render(<GameTransitionDialog {...baseProps} lastGame={noTop} />);
      expect(screen.queryByText('Classifica · Top 3')).toBeNull();
    });
  });

  describe('PreviewPanel (right col)', () => {
    it('renders next game title + publisher + weight', () => {
      render(<GameTransitionDialog {...baseProps} />);
      const preview = screen.getByLabelText('Prossimo gioco');
      expect(within(preview).getByText('Spirit Island')).toBeInTheDocument();
      expect(within(preview).getByText(/GMT · co-op/)).toBeInTheDocument();
      expect(within(preview).getByText(/weight 4\.08/)).toBeInTheDocument();
    });

    it('renders duration pill', () => {
      render(<GameTransitionDialog {...baseProps} />);
      expect(screen.getByText(/~90m/)).toBeInTheDocument();
    });

    it('forwards rulesStatus to KBRulesQuickGlance', () => {
      const { rerender } = render(<GameTransitionDialog {...baseProps} />);
      // ok status: rules text visible
      expect(screen.getByText('Phase order · Growth → Fast → Slow')).toBeInTheDocument();

      rerender(<GameTransitionDialog {...baseProps} rulesStatus="error" />);
      expect(screen.getByRole('alert')).toBeInTheDocument();
    });

    it('invokes onRetryRules when Riprova clicked (error state)', async () => {
      const onRetryRules = vi.fn();
      render(
        <GameTransitionDialog {...baseProps} rulesStatus="error" onRetryRules={onRetryRules} />
      );
      await userEvent.click(screen.getByRole('button', { name: /Riprova/ }));
      expect(onRetryRules).toHaveBeenCalledTimes(1);
    });

    it('renders SetupChecklist with done/total counter', () => {
      render(<GameTransitionDialog {...baseProps} />);
      expect(screen.getByText(/Setup checklist · 1\/2/)).toBeInTheDocument();
    });
  });

  describe('Footer actions', () => {
    it('invokes onPlayNext on "Avvia prossima session" click', async () => {
      const onPlayNext = vi.fn();
      render(<GameTransitionDialog {...baseProps} onPlayNext={onPlayNext} />);
      await userEvent.click(screen.getByRole('button', { name: /Avvia prossima session/ }));
      expect(onPlayNext).toHaveBeenCalledTimes(1);
    });

    it('invokes onOpenSubmodal("skip") on "Salta gioco" click', async () => {
      const onOpenSubmodal = vi.fn();
      render(<GameTransitionDialog {...baseProps} onOpenSubmodal={onOpenSubmodal} />);
      await userEvent.click(screen.getByRole('button', { name: /Salta gioco/ }));
      expect(onOpenSubmodal).toHaveBeenCalledWith('skip');
    });

    it('invokes onOpenSubmodal("end") on "Termina serata qui" click', async () => {
      const onOpenSubmodal = vi.fn();
      render(<GameTransitionDialog {...baseProps} onOpenSubmodal={onOpenSubmodal} />);
      await userEvent.click(screen.getByRole('button', { name: /Termina serata qui/ }));
      expect(onOpenSubmodal).toHaveBeenCalledWith('end');
    });
  });

  describe('ConfirmSubmodal', () => {
    it('does not render when submodal=null', () => {
      render(<GameTransitionDialog {...baseProps} submodal={null} />);
      expect(screen.queryByRole('alertdialog')).toBeNull();
    });

    it('renders skip submodal with warning tone when submodal="skip"', () => {
      render(<GameTransitionDialog {...baseProps} submodal="skip" />);
      const alertdialog = screen.getByRole('alertdialog');
      expect(alertdialog).toBeInTheDocument();
      expect(within(alertdialog).getByText('Saltare Spirit Island?')).toBeInTheDocument();
      expect(
        within(alertdialog).getByRole('button', { name: /Salta e prossimo/ })
      ).toBeInTheDocument();
    });

    it('renders end submodal with danger tone when submodal="end"', () => {
      render(<GameTransitionDialog {...baseProps} submodal="end" />);
      const alertdialog = screen.getByRole('alertdialog');
      expect(within(alertdialog).getByText('Terminare la serata qui?')).toBeInTheDocument();
      expect(
        within(alertdialog).getByRole('button', { name: /Termina · vai a Summary/ })
      ).toBeInTheDocument();
    });

    it('invokes onConfirmSubmodal on primary button click', async () => {
      const onConfirmSubmodal = vi.fn();
      render(
        <GameTransitionDialog
          {...baseProps}
          submodal="skip"
          onConfirmSubmodal={onConfirmSubmodal}
        />
      );
      await userEvent.click(screen.getByRole('button', { name: /Salta e prossimo/ }));
      expect(onConfirmSubmodal).toHaveBeenCalledTimes(1);
    });

    it('invokes onCancelSubmodal on Annulla click', async () => {
      const onCancelSubmodal = vi.fn();
      render(
        <GameTransitionDialog {...baseProps} submodal="end" onCancelSubmodal={onCancelSubmodal} />
      );
      await userEvent.click(screen.getByRole('button', { name: 'Annulla' }));
      expect(onCancelSubmodal).toHaveBeenCalledTimes(1);
    });
  });

  describe('mobile layout', () => {
    it('renders fullscreen instead of fixed size when mobile=true', () => {
      const { container } = render(<GameTransitionDialog {...baseProps} mobile />);
      const dialog = container.firstChild as HTMLElement;
      expect(dialog.className).toContain('w-full');
      expect(dialog.className).toContain('h-full');
      expect(dialog.className).not.toContain('rounded-xl');
    });

    it('uses flex column body when mobile=true (instead of 2-col grid)', () => {
      const { container } = render(<GameTransitionDialog {...baseProps} mobile />);
      const dialog = container.firstChild as HTMLElement;
      expect(dialog.getAttribute('data-mobile')).toBe('true');
    });
  });

  it('accepts custom nightCode in header', () => {
    render(<GameTransitionDialog {...baseProps} nightCode="#GN-042" />);
    expect(screen.getByText(/Game transition · #GN-042/)).toBeInTheDocument();
  });

  it('falls back to "Game transition" label when nightCode missing', () => {
    render(<GameTransitionDialog {...baseProps} />);
    expect(screen.getByText('Game transition')).toBeInTheDocument();
  });

  it('accepts custom className override', () => {
    const { container } = render(
      <GameTransitionDialog {...baseProps} className="custom-dialog-class" />
    );
    expect(container.firstChild).toHaveClass('custom-dialog-class');
  });
});
