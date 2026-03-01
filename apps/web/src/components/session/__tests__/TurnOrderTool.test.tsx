/**
 * TurnOrderTool Component Tests (Issue #4975)
 *
 * Coverage:
 * - Loading state: spinner shown
 * - Error state: error message shown
 * - Uninitialised state (null): placeholder shown
 * - Main view: player list, round badge, current/next indicators
 * - Host-only buttons: Fine turno + Reset visible for hosts, hidden otherwise
 * - Advance/reset callbacks triggered correctly
 * - Accessibility: aria-current, aria-live, aria-label
 */

import { describe, it, expect, vi } from 'vitest';
import { render, screen } from '@testing-library/react';
import userEvent from '@testing-library/user-event';

import { TurnOrderTool } from '../TurnOrderTool';
import type { TurnOrderData } from '../types';

// ── fixtures ──────────────────────────────────────────────────────────────────

const THREE_PLAYERS: TurnOrderData = {
  id: 'turn-1',
  sessionId: 'session-abc',
  playerOrder: ['Alice', 'Bob', 'Charlie'],
  currentIndex: 0,
  currentPlayer: 'Alice',
  nextPlayer: 'Bob',
  roundNumber: 1,
};

const MID_ROUND: TurnOrderData = {
  ...THREE_PLAYERS,
  currentIndex: 1,
  currentPlayer: 'Bob',
  nextPlayer: 'Charlie',
  roundNumber: 2,
};

// ── tests ──────────────────────────────────────────────────────────────────────

describe('TurnOrderTool', () => {
  // ── States ──────────────────────────────────────────────────────────────────

  describe('loading state', () => {
    it('shows aria-busy container and sr-only text when isLoading is true', () => {
      render(<TurnOrderTool turnOrder={null} isLoading />);

      const busy = document.querySelector('[aria-busy="true"]');
      expect(busy).not.toBeNull();
      // sr-only span is present in DOM (screen readers announce it)
      const srOnly = document.querySelector('.sr-only');
      expect(srOnly?.textContent).toBe('Caricamento ordine di turno…');
    });

    it('does not render player list when loading', () => {
      render(<TurnOrderTool turnOrder={THREE_PLAYERS} isLoading />);
      expect(screen.queryByRole('list')).toBeNull();
    });
  });

  describe('error state', () => {
    it('shows error message when error is provided', () => {
      render(<TurnOrderTool turnOrder={null} error="Network failure" />);
      expect(screen.getByRole('alert')).toBeInTheDocument();
      expect(screen.getByText('Network failure')).toBeInTheDocument();
    });

    it('error container has assertive aria-live', () => {
      render(<TurnOrderTool turnOrder={null} error="Oops" />);
      const alert = screen.getByRole('alert');
      expect(alert).toHaveAttribute('aria-live', 'assertive');
    });
  });

  describe('uninitialised state', () => {
    it('shows placeholder when turnOrder is null and not loading', () => {
      render(<TurnOrderTool turnOrder={null} />);
      expect(screen.getByText('Ordine di turno non ancora configurato.')).toBeInTheDocument();
    });

    it('does not show player list when uninitialised', () => {
      render(<TurnOrderTool turnOrder={null} />);
      expect(screen.queryByRole('list')).toBeNull();
    });
  });

  // ── Main view ───────────────────────────────────────────────────────────────

  describe('main view', () => {
    it('renders section with aria-label', () => {
      render(<TurnOrderTool turnOrder={THREE_PLAYERS} />);
      expect(screen.getByRole('region', { name: 'Ordine di turno' })).toBeInTheDocument();
    });

    it('shows round number in badge', () => {
      render(<TurnOrderTool turnOrder={THREE_PLAYERS} />);
      expect(screen.getByLabelText('Round 1')).toBeInTheDocument();
    });

    it('shows round number for later rounds', () => {
      render(<TurnOrderTool turnOrder={MID_ROUND} />);
      expect(screen.getByLabelText('Round 2')).toBeInTheDocument();
    });

    it('renders all players in order', () => {
      render(<TurnOrderTool turnOrder={THREE_PLAYERS} />);
      const list = screen.getByRole('list');
      const items = list.querySelectorAll('li');
      expect(items).toHaveLength(3);
      expect(items[0]).toHaveTextContent('Alice');
      expect(items[1]).toHaveTextContent('Bob');
      expect(items[2]).toHaveTextContent('Charlie');
    });

    it('marks current player with aria-current="step"', () => {
      render(<TurnOrderTool turnOrder={THREE_PLAYERS} />);
      const list = screen.getByRole('list');
      const items = list.querySelectorAll('li');
      expect(items[0]).toHaveAttribute('aria-current', 'step');
      expect(items[1]).not.toHaveAttribute('aria-current');
      expect(items[2]).not.toHaveAttribute('aria-current');
    });

    it('shows "Attuale" badge on current player', () => {
      render(<TurnOrderTool turnOrder={THREE_PLAYERS} />);
      expect(screen.getByText('Attuale')).toBeInTheDocument();
    });

    it('shows "Prossimo" label on next player', () => {
      render(<TurnOrderTool turnOrder={THREE_PLAYERS} />);
      expect(screen.getByText('Prossimo')).toBeInTheDocument();
    });

    it('positions "Prossimo" on the correct player (index 1 when index 0 is current)', () => {
      render(<TurnOrderTool turnOrder={THREE_PLAYERS} />);
      const list = screen.getByRole('list');
      const items = list.querySelectorAll('li');
      // Alice (index 0) = current, Bob (index 1) = next
      expect(items[0]).toHaveTextContent('Attuale');
      expect(items[1]).toHaveTextContent('Prossimo');
      expect(items[2]).not.toHaveTextContent('Prossimo');
    });

    it('wraps "Prossimo" when current is last player', () => {
      const lastCurrent: TurnOrderData = {
        ...THREE_PLAYERS,
        currentIndex: 2,
        currentPlayer: 'Charlie',
        nextPlayer: 'Alice',
      };
      render(<TurnOrderTool turnOrder={lastCurrent} />);
      const list = screen.getByRole('list');
      const items = list.querySelectorAll('li');
      expect(items[2]).toHaveTextContent('Attuale');
      expect(items[0]).toHaveTextContent('Prossimo'); // wraps to first
    });

    it('player list has aria-live="polite"', () => {
      render(<TurnOrderTool turnOrder={THREE_PLAYERS} />);
      const list = screen.getByRole('list');
      expect(list).toHaveAttribute('aria-live', 'polite');
    });

    it('position numbers are rendered (1, 2, 3...)', () => {
      render(<TurnOrderTool turnOrder={THREE_PLAYERS} />);
      const list = screen.getByRole('list');
      expect(list).toHaveTextContent('1');
      expect(list).toHaveTextContent('2');
      expect(list).toHaveTextContent('3');
    });
  });

  // ── Host-only actions ────────────────────────────────────────────────────────

  describe('host-only actions', () => {
    it('does NOT show action buttons for non-hosts', () => {
      render(
        <TurnOrderTool
          turnOrder={THREE_PLAYERS}
          isHost={false}
          onAdvanceTurn={vi.fn()}
          onResetTurnOrder={vi.fn()}
        />
      );
      expect(screen.queryByRole('button', { name: /fine turno/i })).toBeNull();
      expect(screen.queryByRole('button', { name: /reimposta/i })).toBeNull();
    });

    it('shows Fine Turno button for hosts when onAdvanceTurn is provided', () => {
      render(
        <TurnOrderTool
          turnOrder={THREE_PLAYERS}
          isHost
          onAdvanceTurn={vi.fn()}
        />
      );
      expect(
        screen.getByRole('button', { name: /fine turno/i })
      ).toBeInTheDocument();
    });

    it('shows Reset button for hosts when onResetTurnOrder is provided', () => {
      render(
        <TurnOrderTool
          turnOrder={THREE_PLAYERS}
          isHost
          onResetTurnOrder={vi.fn()}
        />
      );
      expect(
        screen.getByRole('button', { name: /reimposta ordine di turno/i })
      ).toBeInTheDocument();
    });

    it('calls onAdvanceTurn when Fine Turno is clicked', async () => {
      const user = userEvent.setup();
      const onAdvanceTurn = vi.fn().mockResolvedValue(undefined);

      render(
        <TurnOrderTool
          turnOrder={THREE_PLAYERS}
          isHost
          onAdvanceTurn={onAdvanceTurn}
        />
      );

      await user.click(screen.getByRole('button', { name: /fine turno/i }));
      expect(onAdvanceTurn).toHaveBeenCalledOnce();
    });

    it('calls onResetTurnOrder when Reset is clicked', async () => {
      const user = userEvent.setup();
      const onResetTurnOrder = vi.fn().mockResolvedValue(undefined);

      render(
        <TurnOrderTool
          turnOrder={THREE_PLAYERS}
          isHost
          onResetTurnOrder={onResetTurnOrder}
        />
      );

      await user.click(screen.getByRole('button', { name: /reimposta ordine di turno/i }));
      expect(onResetTurnOrder).toHaveBeenCalledOnce();
    });

    it('Fine Turno button disabled while advancing', async () => {
      // Provide a promise that never resolves so button stays disabled
      const onAdvanceTurn = vi.fn(() => new Promise<void>(() => {}));

      const user = userEvent.setup();
      render(
        <TurnOrderTool
          turnOrder={THREE_PLAYERS}
          isHost
          onAdvanceTurn={onAdvanceTurn}
        />
      );

      const btn = screen.getByRole('button', { name: /fine turno/i });
      await user.click(btn);
      expect(btn).toBeDisabled();
    });

    it('Reset button disabled while resetting', async () => {
      const onResetTurnOrder = vi.fn(() => new Promise<void>(() => {}));

      const user = userEvent.setup();
      render(
        <TurnOrderTool
          turnOrder={THREE_PLAYERS}
          isHost
          onResetTurnOrder={onResetTurnOrder}
        />
      );

      const btn = screen.getByRole('button', { name: /reimposta ordine di turno/i });
      await user.click(btn);
      expect(btn).toBeDisabled();
    });
  });

  // ── className ────────────────────────────────────────────────────────────────

  it('applies custom className to section', () => {
    const { container } = render(
      <TurnOrderTool turnOrder={THREE_PLAYERS} className="custom-class" />
    );
    expect(container.querySelector('.custom-class')).not.toBeNull();
  });
});
