import { describe, expect, it, vi } from 'vitest';
import { render, screen } from '@testing-library/react';
import userEvent from '@testing-library/user-event';
import { PerGameRecapRow, type PerGameRecapGame } from './PerGameRecapRow';

const COMPETITIVE_GAME: PerGameRecapGame = {
  id: 'gs-brass-1',
  sessionId: 's-brass-may17',
  title: 'Brass: Birmingham',
  emoji: '🏭',
  cover: ['hsl(220 35% 28%)', 'hsl(28 60% 38%)'],
  order: 1,
  duration: '2h 45m',
  eventsCount: 11,
  winner: { id: 'p-davide', name: 'Davide', initials: 'DC', color: 200, score: 178 },
  topScores: [
    { id: 'p-marco', name: 'Marco', initials: 'MR', color: 262, score: 142 },
    { id: 'p-giulia', name: 'Giulia', initials: 'GM', color: 10, score: 128 },
  ],
};

const COOP_GAME: PerGameRecapGame = {
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
};

describe('PerGameRecapRow', () => {
  describe('competitive variant (default)', () => {
    it('renders game title + order badge + duration + events', () => {
      render(<PerGameRecapRow game={COMPETITIVE_GAME} />);
      expect(screen.getByText('Brass: Birmingham')).toBeInTheDocument();
      expect(screen.getByText('#1')).toBeInTheDocument();
      expect(screen.getByText(/2h 45m · 11 eventi/)).toBeInTheDocument();
    });

    it('renders winner avatar with aria-label', () => {
      render(<PerGameRecapRow game={COMPETITIVE_GAME} />);
      expect(screen.getByLabelText('Winner DC')).toBeInTheDocument();
    });

    it('renders winner name + score with "pt" suffix', () => {
      render(<PerGameRecapRow game={COMPETITIVE_GAME} />);
      expect(screen.getByText(/Davide/)).toBeInTheDocument();
      expect(screen.getByText('178pt')).toBeInTheDocument();
    });

    it('renders top scores chips when provided', () => {
      render(<PerGameRecapRow game={COMPETITIVE_GAME} />);
      expect(screen.getByLabelText('Top MR')).toBeInTheDocument();
      expect(screen.getByLabelText('Top GM')).toBeInTheDocument();
    });

    it('does NOT render co-op badge', () => {
      render(<PerGameRecapRow game={COMPETITIVE_GAME} />);
      expect(screen.queryByText(/Co-op/)).toBeNull();
      expect(screen.queryByText(/Team coop ha vinto/)).toBeNull();
    });

    it('data-coop="false"', () => {
      const { container } = render(<PerGameRecapRow game={COMPETITIVE_GAME} />);
      expect(container.firstChild).toHaveAttribute('data-coop', 'false');
    });
  });

  describe('co-op variant', () => {
    it('renders 🤝 Co-op badge', () => {
      render(<PerGameRecapRow game={COOP_GAME} />);
      expect(screen.getByText(/Co-op/)).toBeInTheDocument();
    });

    it('renders "Team coop ha vinto" instead of winner avatar', () => {
      render(<PerGameRecapRow game={COOP_GAME} />);
      expect(screen.getByText(/Team coop ha vinto/)).toBeInTheDocument();
      expect(screen.queryByLabelText(/^Winner /)).toBeNull();
    });

    it('renders coopOutcome text when provided', () => {
      render(<PerGameRecapRow game={COOP_GAME} />);
      expect(screen.getByText('round 5/6 · adversary England 1')).toBeInTheDocument();
    });

    it('data-coop="true"', () => {
      const { container } = render(<PerGameRecapRow game={COOP_GAME} />);
      expect(container.firstChild).toHaveAttribute('data-coop', 'true');
    });

    it('uses entity-toolkit border-left accent (vs entity-event for competitive)', () => {
      const { container, rerender } = render(<PerGameRecapRow game={COOP_GAME} />);
      const coopArticle = container.firstChild as HTMLElement;
      expect(coopArticle.className).toContain('border-l-entity-toolkit');

      rerender(<PerGameRecapRow game={COMPETITIVE_GAME} />);
      const competitiveArticle = container.firstChild as HTMLElement;
      expect(competitiveArticle.className).toContain('border-l-entity-event');
    });
  });

  describe('no-winner edge case', () => {
    it('renders "Nessun winner registrato" placeholder', () => {
      const noWinner: PerGameRecapGame = {
        id: 'gs-x',
        title: 'X',
        order: 1,
        duration: '60m',
        eventsCount: 0,
      };
      render(<PerGameRecapRow game={noWinner} />);
      expect(screen.getByText('Nessun winner registrato')).toBeInTheDocument();
    });
  });

  describe('singular/plural events', () => {
    it('uses "evento" for count=1', () => {
      const oneEvent: PerGameRecapGame = {
        id: 'gs-x',
        title: 'X',
        order: 1,
        duration: '60m',
        eventsCount: 1,
      };
      render(<PerGameRecapRow game={oneEvent} />);
      expect(screen.getByText(/60m · 1 evento/)).toBeInTheDocument();
    });

    it('uses "eventi" for count !== 1', () => {
      render(<PerGameRecapRow game={COMPETITIVE_GAME} />);
      expect(screen.getByText(/11 eventi/)).toBeInTheDocument();
    });
  });

  describe('Vai a session CTA', () => {
    it('renders CTA when sessionId + onJumpToSession provided', () => {
      render(<PerGameRecapRow game={COMPETITIVE_GAME} onJumpToSession={() => undefined} />);
      expect(screen.getByRole('button', { name: /Vai a session/ })).toBeInTheDocument();
    });

    it('invokes onJumpToSession with sessionId', async () => {
      const onJumpToSession = vi.fn();
      render(<PerGameRecapRow game={COMPETITIVE_GAME} onJumpToSession={onJumpToSession} />);
      await userEvent.click(screen.getByRole('button', { name: /Vai a session/ }));
      expect(onJumpToSession).toHaveBeenCalledWith('s-brass-may17');
    });

    it('omits CTA when onJumpToSession missing', () => {
      render(<PerGameRecapRow game={COMPETITIVE_GAME} />);
      expect(screen.queryByRole('button', { name: /Vai a session/ })).toBeNull();
    });

    it('omits CTA when sessionId missing', () => {
      const noSessionGame: PerGameRecapGame = { ...COMPETITIVE_GAME, sessionId: undefined };
      render(<PerGameRecapRow game={noSessionGame} onJumpToSession={() => undefined} />);
      expect(screen.queryByRole('button', { name: /Vai a session/ })).toBeNull();
    });
  });

  it('accepts custom className override', () => {
    const { container } = render(
      <PerGameRecapRow game={COMPETITIVE_GAME} className="custom-row-class" />
    );
    expect(container.firstChild).toHaveClass('custom-row-class');
  });
});
