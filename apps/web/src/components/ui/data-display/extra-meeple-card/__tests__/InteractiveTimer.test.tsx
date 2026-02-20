/**
 * Tests for InteractiveTimer component
 * Issue #4763 - Interactive Cards + Timer + Events Timeline UI + Phase 3 Tests
 */

import { render, screen } from '@testing-library/react';
import userEvent from '@testing-library/user-event';
import { describe, expect, it, vi } from 'vitest';

import { InteractiveTimer } from '../tabs/InteractiveTimer';
import type { TimerState, TimerActions } from '../types';

// ============================================================================
// Test Data
// ============================================================================

const mockTimerState: TimerState = {
  toolName: 'Turn Timer',
  timerType: 'Countdown',
  totalSeconds: 300,
  remainingSeconds: 180,
  status: 'running',
  color: '#4F46E5',
  isPerPlayer: false,
  warningThresholdSeconds: 30,
};

const mockPerPlayerState: TimerState = {
  ...mockTimerState,
  isPerPlayer: true,
  playerTimers: {
    p1: { remainingSeconds: 120, status: 'running' },
    p2: { remainingSeconds: 180, status: 'idle' },
    p3: { remainingSeconds: 0, status: 'expired' },
  },
  activePlayerId: 'p1',
};

const mockActions: TimerActions = {
  onStart: vi.fn(),
  onPause: vi.fn(),
  onResume: vi.fn(),
  onReset: vi.fn(),
};

const mockPlayerNames: Record<string, string> = {
  p1: 'Alice',
  p2: 'Bob',
  p3: 'Charlie',
};

// ============================================================================
// Tests
// ============================================================================

describe('InteractiveTimer', () => {
  it('renders empty state when no state provided', () => {
    render(<InteractiveTimer />);

    expect(screen.getByText('No timer active')).toBeInTheDocument();
  });

  it('renders timer name and type', () => {
    render(<InteractiveTimer state={mockTimerState} />);

    expect(screen.getByText('Turn Timer')).toBeInTheDocument();
    expect(screen.getByText('Countdown')).toBeInTheDocument();
  });

  it('renders formatted time display', () => {
    render(<InteractiveTimer state={mockTimerState} />);

    expect(screen.getByTestId('timer-display')).toHaveTextContent('03:00');
  });

  it('renders status badge', () => {
    render(<InteractiveTimer state={mockTimerState} />);

    expect(screen.getByTestId('timer-status-badge')).toHaveTextContent('running');
  });

  it('shows pause button when running', () => {
    render(<InteractiveTimer state={mockTimerState} actions={mockActions} />);

    expect(screen.getByTestId('timer-action-pause')).toBeInTheDocument();
  });

  it('calls onPause when pause clicked', async () => {
    const user = userEvent.setup();
    const onPause = vi.fn();
    render(<InteractiveTimer state={mockTimerState} actions={{ onPause }} />);

    await user.click(screen.getByTestId('timer-action-pause'));

    expect(onPause).toHaveBeenCalledTimes(1);
  });

  it('shows start button when idle', () => {
    const idle = { ...mockTimerState, status: 'idle' as const };
    render(<InteractiveTimer state={idle} actions={mockActions} />);

    expect(screen.getByTestId('timer-action-start')).toHaveTextContent('Start');
  });

  it('shows resume button when paused', () => {
    const paused = { ...mockTimerState, status: 'paused' as const };
    render(<InteractiveTimer state={paused} actions={mockActions} />);

    expect(screen.getByTestId('timer-action-start')).toHaveTextContent('Resume');
  });

  it('shows reset button when not idle', () => {
    render(<InteractiveTimer state={mockTimerState} actions={mockActions} />);

    expect(screen.getByTestId('timer-action-reset')).toBeInTheDocument();
  });

  it('hides reset button when idle', () => {
    const idle = { ...mockTimerState, status: 'idle' as const };
    render(<InteractiveTimer state={idle} actions={mockActions} />);

    expect(screen.queryByTestId('timer-action-reset')).not.toBeInTheDocument();
  });

  it('calls onReset when reset clicked', async () => {
    const user = userEvent.setup();
    const onReset = vi.fn();
    render(
      <InteractiveTimer state={mockTimerState} actions={{ onReset }} />
    );

    await user.click(screen.getByTestId('timer-action-reset'));

    expect(onReset).toHaveBeenCalledTimes(1);
  });

  it('renders warning status text', () => {
    const warning = { ...mockTimerState, status: 'warning' as const, remainingSeconds: 20 };
    render(<InteractiveTimer state={warning} />);

    expect(screen.getByText('Low time!')).toBeInTheDocument();
  });

  it('renders expired status badge', () => {
    const expired = { ...mockTimerState, status: 'expired' as const, remainingSeconds: 0 };
    render(<InteractiveTimer state={expired} />);

    expect(screen.getByTestId('timer-status-badge')).toHaveTextContent('expired');
    expect(screen.getByTestId('timer-display')).toHaveTextContent('00:00');
  });

  it('formats time with hours when > 60 minutes', () => {
    const longTimer = { ...mockTimerState, totalSeconds: 7200, remainingSeconds: 3723 };
    render(<InteractiveTimer state={longTimer} />);

    expect(screen.getByTestId('timer-display')).toHaveTextContent('1:02:03');
  });

  it('renders per-player timers', () => {
    render(
      <InteractiveTimer
        state={mockPerPlayerState}
        playerNames={mockPlayerNames}
      />
    );

    expect(screen.getByText('Player Timers')).toBeInTheDocument();
    expect(screen.getByTestId('player-timer-p1')).toBeInTheDocument();
    expect(screen.getByTestId('player-timer-p2')).toBeInTheDocument();
    expect(screen.getByTestId('player-timer-p3')).toBeInTheDocument();
  });

  it('shows player names in per-player timers', () => {
    render(
      <InteractiveTimer
        state={mockPerPlayerState}
        playerNames={mockPlayerNames}
      />
    );

    expect(screen.getByText('Alice')).toBeInTheDocument();
    expect(screen.getByText('Bob')).toBeInTheDocument();
    expect(screen.getByText('Charlie')).toBeInTheDocument();
  });

  it('falls back to playerId when no player name provided', () => {
    render(<InteractiveTimer state={mockPerPlayerState} />);

    expect(screen.getByText('p1')).toBeInTheDocument();
  });

  it('renders without actions (view-only mode)', () => {
    render(<InteractiveTimer state={mockTimerState} />);

    expect(screen.getByTestId('interactive-timer')).toBeInTheDocument();
    expect(screen.queryByTestId('timer-action-pause')).not.toBeInTheDocument();
  });
});
