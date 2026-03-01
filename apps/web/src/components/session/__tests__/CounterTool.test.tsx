/**
 * CounterTool Component Tests (Issue #4979)
 *
 * Coverage:
 * - Loading state: spinner shown when counterState is null
 * - Shared mode: single counter row, +/- buttons work
 * - Per-player mode: one row per participant
 * - Min/max clamping: buttons disabled at boundaries
 * - Reset button: calls onApplyChange with correct delta
 * - Range indicator shown
 * - Error displayed when error prop is set
 * - Accessibility: aria-live, aria-label, aria-busy
 */

import React from 'react';

import { describe, it, expect, vi, beforeEach } from 'vitest';
import { render, screen, waitFor } from '@testing-library/react';
import userEvent from '@testing-library/user-event';

import { CounterTool } from '../CounterTool';
import type { CounterToolConfig, CounterState, Participant } from '../types';

// ── Fixtures ──────────────────────────────────────────────────────────────────

const SHARED_CONFIG: CounterToolConfig = {
  name: 'Punti Vittoria',
  minValue: 0,
  maxValue: 100,
  defaultValue: 50,
  isPerPlayer: false,
  icon: null,
  color: null,
};

const PER_PLAYER_CONFIG: CounterToolConfig = {
  ...SHARED_CONFIG,
  isPerPlayer: true,
};

const SHARED_STATE: CounterState = {
  minValue: 0,
  maxValue: 100,
  defaultValue: 50,
  isPerPlayer: false,
  currentValue: 50,
  playerValues: {},
};

const PLAYERS: Participant[] = [
  {
    id: 'player-1',
    displayName: 'Alice',
    isOwner: true,
    isCurrentUser: true,
    avatarColor: '#f00',
    totalScore: 0,
  },
  {
    id: 'player-2',
    displayName: 'Bob',
    isOwner: false,
    isCurrentUser: false,
    avatarColor: '#0f0',
    totalScore: 0,
  },
];

const PER_PLAYER_STATE: CounterState = {
  ...SHARED_STATE,
  isPerPlayer: true,
  currentValue: 50,
  playerValues: {
    'player-1': 30,
    'player-2': 45,
  },
};

// ── Tests ─────────────────────────────────────────────────────────────────────

describe('CounterTool', () => {
  let onApplyChange: ReturnType<typeof vi.fn>;

  beforeEach(() => {
    onApplyChange = vi.fn().mockResolvedValue(undefined);
  });

  // ── Loading state ────────────────────────────────────────────────────────────

  describe('loading state', () => {
    it('shows aria-busy container when counterState is null', () => {
      render(
        <CounterTool
          config={SHARED_CONFIG}
          counterState={null}
          participants={PLAYERS}
          currentUserId="player-1"
          onApplyChange={onApplyChange}
        />
      );

      expect(document.querySelector('[aria-busy="true"]')).not.toBeNull();
    });

    it('shows sr-only loading text when counterState is null', () => {
      render(
        <CounterTool
          config={SHARED_CONFIG}
          counterState={null}
          participants={PLAYERS}
          currentUserId="player-1"
          onApplyChange={onApplyChange}
        />
      );

      expect(document.querySelector('.sr-only')?.textContent).toBe('Caricamento contatore…');
    });
  });

  // ── Section accessibility ────────────────────────────────────────────────────

  describe('accessibility', () => {
    it('renders section with correct aria-label', () => {
      render(
        <CounterTool
          config={SHARED_CONFIG}
          counterState={SHARED_STATE}
          participants={PLAYERS}
          currentUserId="player-1"
          onApplyChange={onApplyChange}
        />
      );

      expect(
        screen.getByRole('region', { name: /Contatore: Punti Vittoria/i })
      ).toBeInTheDocument();
    });

    it('value display has aria-live="polite"', () => {
      render(
        <CounterTool
          config={SHARED_CONFIG}
          counterState={SHARED_STATE}
          participants={PLAYERS}
          currentUserId="player-1"
          onApplyChange={onApplyChange}
        />
      );

      const live = document.querySelector('[aria-live="polite"]');
      expect(live).not.toBeNull();
    });
  });

  // ── Shared mode ──────────────────────────────────────────────────────────────

  describe('shared mode', () => {
    it('renders the config name in header', () => {
      render(
        <CounterTool
          config={SHARED_CONFIG}
          counterState={SHARED_STATE}
          participants={PLAYERS}
          currentUserId="player-1"
          onApplyChange={onApplyChange}
        />
      );

      expect(screen.getAllByText('Punti Vittoria').length).toBeGreaterThanOrEqual(1);
    });

    it('shows "Condiviso" mode badge', () => {
      render(
        <CounterTool
          config={SHARED_CONFIG}
          counterState={SHARED_STATE}
          participants={PLAYERS}
          currentUserId="player-1"
          onApplyChange={onApplyChange}
        />
      );

      expect(screen.getByText('Condiviso')).toBeInTheDocument();
    });

    it('displays current value', () => {
      render(
        <CounterTool
          config={SHARED_CONFIG}
          counterState={SHARED_STATE}
          participants={PLAYERS}
          currentUserId="player-1"
          onApplyChange={onApplyChange}
        />
      );

      expect(screen.getByLabelText(/Punti Vittoria: 50/)).toBeInTheDocument();
    });

    it('calls onApplyChange with +1 on increment click', async () => {
      const user = userEvent.setup();
      render(
        <CounterTool
          config={SHARED_CONFIG}
          counterState={SHARED_STATE}
          participants={PLAYERS}
          currentUserId="player-1"
          onApplyChange={onApplyChange}
        />
      );

      await user.click(screen.getByRole('button', { name: /Increment.*by 1/i }));
      expect(onApplyChange).toHaveBeenCalledWith('player-1', 1);
    });

    it('calls onApplyChange with -1 on decrement click', async () => {
      const user = userEvent.setup();
      render(
        <CounterTool
          config={SHARED_CONFIG}
          counterState={SHARED_STATE}
          participants={PLAYERS}
          currentUserId="player-1"
          onApplyChange={onApplyChange}
        />
      );

      await user.click(screen.getByRole('button', { name: /Decrement.*by 1/i }));
      expect(onApplyChange).toHaveBeenCalledWith('player-1', -1);
    });

    it('increment button disabled when value is at maxValue', () => {
      const atMax: CounterState = { ...SHARED_STATE, currentValue: 100 };
      render(
        <CounterTool
          config={SHARED_CONFIG}
          counterState={atMax}
          participants={PLAYERS}
          currentUserId="player-1"
          onApplyChange={onApplyChange}
        />
      );

      expect(screen.getByRole('button', { name: /Increment/i })).toBeDisabled();
    });

    it('decrement button disabled when value is at minValue', () => {
      const atMin: CounterState = { ...SHARED_STATE, currentValue: 0 };
      render(
        <CounterTool
          config={SHARED_CONFIG}
          counterState={atMin}
          participants={PLAYERS}
          currentUserId="player-1"
          onApplyChange={onApplyChange}
        />
      );

      expect(screen.getByRole('button', { name: /Decrement/i })).toBeDisabled();
    });
  });

  // ── Per-player mode ──────────────────────────────────────────────────────────

  describe('per-player mode', () => {
    it('shows "Per player" mode badge', () => {
      render(
        <CounterTool
          config={PER_PLAYER_CONFIG}
          counterState={PER_PLAYER_STATE}
          participants={PLAYERS}
          currentUserId="player-1"
          onApplyChange={onApplyChange}
        />
      );

      expect(screen.getByText('Per player')).toBeInTheDocument();
    });

    it('renders one row per participant', () => {
      render(
        <CounterTool
          config={PER_PLAYER_CONFIG}
          counterState={PER_PLAYER_STATE}
          participants={PLAYERS}
          currentUserId="player-1"
          onApplyChange={onApplyChange}
        />
      );

      expect(screen.getByText('Alice')).toBeInTheDocument();
      expect(screen.getByText('Bob')).toBeInTheDocument();
    });

    it('displays per-player values correctly', () => {
      render(
        <CounterTool
          config={PER_PLAYER_CONFIG}
          counterState={PER_PLAYER_STATE}
          participants={PLAYERS}
          currentUserId="player-1"
          onApplyChange={onApplyChange}
        />
      );

      expect(screen.getByLabelText('Alice: 30')).toBeInTheDocument();
      expect(screen.getByLabelText('Bob: 45')).toBeInTheDocument();
    });

    it('falls back to defaultValue when player has no entry', () => {
      const stateNoPlayer2: CounterState = {
        ...PER_PLAYER_STATE,
        playerValues: { 'player-1': 30 },
      };
      render(
        <CounterTool
          config={PER_PLAYER_CONFIG}
          counterState={stateNoPlayer2}
          participants={PLAYERS}
          currentUserId="player-1"
          onApplyChange={onApplyChange}
        />
      );

      // Bob falls back to defaultValue=50
      expect(screen.getByLabelText('Bob: 50')).toBeInTheDocument();
    });

    it('calls onApplyChange with correct player ID when incrementing', async () => {
      const user = userEvent.setup();
      render(
        <CounterTool
          config={PER_PLAYER_CONFIG}
          counterState={PER_PLAYER_STATE}
          participants={PLAYERS}
          currentUserId="player-1"
          onApplyChange={onApplyChange}
        />
      );

      // Click increment for Bob (second row, second increment button)
      const incrementBtns = screen.getAllByRole('button', { name: /Increment/i });
      await user.click(incrementBtns[1]); // Bob's increment
      expect(onApplyChange).toHaveBeenCalledWith('player-2', 1);
    });
  });

  // ── Reset button ─────────────────────────────────────────────────────────────

  describe('reset button', () => {
    it('renders reset button with correct aria-label', () => {
      render(
        <CounterTool
          config={SHARED_CONFIG}
          counterState={SHARED_STATE}
          participants={PLAYERS}
          currentUserId="player-1"
          onApplyChange={onApplyChange}
        />
      );

      expect(
        screen.getByRole('button', { name: /Reset Punti Vittoria to 50/i })
      ).toBeInTheDocument();
    });

    it('calls onApplyChange with delta to reach defaultValue on reset', async () => {
      const user = userEvent.setup();
      // currentValue=50, defaultValue=50, so delta=0 → no call
      // Use a value != default
      const stateOff: CounterState = { ...SHARED_STATE, currentValue: 70 };
      render(
        <CounterTool
          config={SHARED_CONFIG}
          counterState={stateOff}
          participants={PLAYERS}
          currentUserId="player-1"
          onApplyChange={onApplyChange}
        />
      );

      await user.click(screen.getByRole('button', { name: /Reset/i }));
      await waitFor(() =>
        expect(onApplyChange).toHaveBeenCalledWith('player-1', -20)
      );
    });
  });

  // ── Range indicator ──────────────────────────────────────────────────────────

  it('shows range indicator with min and max values', () => {
    render(
      <CounterTool
        config={SHARED_CONFIG}
        counterState={SHARED_STATE}
        participants={PLAYERS}
        currentUserId="player-1"
        onApplyChange={onApplyChange}
      />
    );

    expect(screen.getByText(/Range: 0 – 100/)).toBeInTheDocument();
  });

  // ── Error state ──────────────────────────────────────────────────────────────

  it('shows error message when error prop is set', () => {
    render(
      <CounterTool
        config={SHARED_CONFIG}
        counterState={SHARED_STATE}
        participants={PLAYERS}
        currentUserId="player-1"
        onApplyChange={onApplyChange}
        error="Network failure"
      />
    );

    expect(screen.getByRole('alert')).toBeInTheDocument();
    expect(screen.getByText('Network failure')).toBeInTheDocument();
  });

  // ── isPending ────────────────────────────────────────────────────────────────

  it('disables buttons when isPending', () => {
    render(
      <CounterTool
        config={SHARED_CONFIG}
        counterState={SHARED_STATE}
        participants={PLAYERS}
        currentUserId="player-1"
        onApplyChange={onApplyChange}
        isPending
      />
    );

    screen.getAllByRole('button', { name: /Increment|Decrement/i }).forEach(btn => {
      expect(btn).toBeDisabled();
    });
  });

  // ── Custom className ─────────────────────────────────────────────────────────

  it('applies custom className to section', () => {
    const { container } = render(
      <CounterTool
        config={SHARED_CONFIG}
        counterState={SHARED_STATE}
        participants={PLAYERS}
        currentUserId="player-1"
        onApplyChange={onApplyChange}
        className="custom-counter"
      />
    );

    expect(container.querySelector('.custom-counter')).not.toBeNull();
  });
});
