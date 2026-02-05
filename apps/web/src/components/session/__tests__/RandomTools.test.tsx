/**
 * Unit tests for Random Tools components (Issue #3345)
 */

import { describe, it, expect, vi, beforeEach } from 'vitest';
import { render, screen, fireEvent, waitFor } from '@testing-library/react';
import userEvent from '@testing-library/user-event';

import { CountdownTimer } from '../CountdownTimer';
import { CoinFlip } from '../CoinFlip';
import { WheelSpinner } from '../WheelSpinner';
import { RandomTools } from '../RandomTools';

import type { TimerState, CoinFlipResult, WheelOption, WheelSpinResult } from '../types';

// Mock framer-motion to avoid animation issues in tests
vi.mock('framer-motion', async () => {
  const actual = await vi.importActual('framer-motion');
  return {
    ...actual,
    motion: {
      div: ({ children, ...props }: { children?: React.ReactNode }) => <div {...props}>{children}</div>,
      span: ({ children, ...props }: { children?: React.ReactNode }) => <span {...props}>{children}</span>,
      svg: ({ children, ...props }: { children?: React.ReactNode }) => <svg {...props}>{children}</svg>,
    },
    AnimatePresence: ({ children }: { children?: React.ReactNode }) => <>{children}</>,
  };
});

// Mock audio
beforeEach(() => {
  vi.spyOn(window, 'Audio').mockImplementation(() => ({
    play: vi.fn().mockResolvedValue(undefined),
    pause: vi.fn(),
    currentTime: 0,
    volume: 1,
  } as unknown as HTMLAudioElement));
});

describe('CountdownTimer', () => {
  it('renders initial state correctly', () => {
    render(<CountdownTimer />);

    expect(screen.getByText('Start Timer')).toBeInTheDocument();
    expect(screen.getByText('05:00')).toBeInTheDocument(); // Default 5 minutes
  });

  it('allows setting custom duration', async () => {
    const user = userEvent.setup();
    render(<CountdownTimer />);

    const minutesInput = screen.getAllByRole('spinbutton')[0];
    const secondsInput = screen.getAllByRole('spinbutton')[1];

    await user.clear(minutesInput);
    await user.type(minutesInput, '10');
    await user.clear(secondsInput);
    await user.type(secondsInput, '30');

    expect(screen.getByText('10:30')).toBeInTheDocument();
  });

  it('calls onStart when Start Timer is clicked', async () => {
    const onStart = vi.fn().mockResolvedValue(undefined);
    const user = userEvent.setup();

    render(<CountdownTimer onStart={onStart} />);

    await user.click(screen.getByText('Start Timer'));

    expect(onStart).toHaveBeenCalledWith(300); // 5 minutes = 300 seconds
  });

  it('disables start button when duration is 0', async () => {
    const user = userEvent.setup();
    render(<CountdownTimer />);

    const minutesInput = screen.getAllByRole('spinbutton')[0];
    const secondsInput = screen.getAllByRole('spinbutton')[1];

    await user.clear(minutesInput);
    await user.type(minutesInput, '0');
    await user.clear(secondsInput);
    await user.type(secondsInput, '0');

    expect(screen.getByText('Start Timer')).toBeDisabled();
  });

  it('shows quick preset buttons', () => {
    render(<CountdownTimer />);

    expect(screen.getByText('1m')).toBeInTheDocument();
    expect(screen.getByText('5m')).toBeInTheDocument();
    expect(screen.getByText('10m')).toBeInTheDocument();
    expect(screen.getByText('30m')).toBeInTheDocument();
  });

  it('shows pause button when running', () => {
    const timerState: TimerState = {
      id: 'timer-1',
      sessionId: 'session-1',
      durationSeconds: 300,
      remainingSeconds: 250,
      status: 'running',
      startedBy: 'user-1',
      startedByName: 'Player 1',
      startedAt: new Date(),
    };

    render(<CountdownTimer timerState={timerState} />);

    expect(screen.getByText('Pause')).toBeInTheDocument();
  });

  it('shows resume and reset buttons when paused', () => {
    const timerState: TimerState = {
      id: 'timer-1',
      sessionId: 'session-1',
      durationSeconds: 300,
      remainingSeconds: 250,
      status: 'paused',
      startedBy: 'user-1',
      startedByName: 'Player 1',
      startedAt: new Date(),
      pausedAt: new Date(),
    };

    render(<CountdownTimer timerState={timerState} />);

    expect(screen.getByText('Resume')).toBeInTheDocument();
    expect(screen.getByText('Reset')).toBeInTheDocument();
  });
});

describe('CoinFlip', () => {
  const defaultProps = {
    sessionId: 'session-1',
    participantId: 'user-1',
    participantName: 'Player 1',
  };

  it('renders flip button', () => {
    render(<CoinFlip {...defaultProps} />);

    expect(screen.getByText('Flip Coin')).toBeInTheDocument();
  });

  it('calls onFlip when button is clicked', async () => {
    const onFlip = vi.fn().mockResolvedValue({
      id: 'flip-1',
      sessionId: 'session-1',
      participantId: 'user-1',
      participantName: 'Player 1',
      result: 'heads',
      timestamp: new Date(),
    } as CoinFlipResult);

    const user = userEvent.setup();
    render(<CoinFlip {...defaultProps} onFlip={onFlip} />);

    await user.click(screen.getByText('Flip Coin'));

    await waitFor(() => {
      expect(onFlip).toHaveBeenCalled();
    });
  });

  it('shows flip history stats when history is provided', () => {
    const flipHistory: CoinFlipResult[] = [
      { id: '1', sessionId: 's1', participantId: 'u1', participantName: 'P1', result: 'heads', timestamp: new Date() },
      { id: '2', sessionId: 's1', participantId: 'u1', participantName: 'P1', result: 'tails', timestamp: new Date() },
      { id: '3', sessionId: 's1', participantId: 'u1', participantName: 'P1', result: 'heads', timestamp: new Date() },
    ];

    render(<CoinFlip {...defaultProps} flipHistory={flipHistory} />);

    expect(screen.getByText('2')).toBeInTheDocument(); // 2 heads
    expect(screen.getByText('1')).toBeInTheDocument(); // 1 tails
    expect(screen.getByText('3')).toBeInTheDocument(); // 3 total
  });

  it('displays heads and tails labels', () => {
    const flipHistory: CoinFlipResult[] = [
      { id: '1', sessionId: 's1', participantId: 'u1', participantName: 'P1', result: 'heads', timestamp: new Date() },
    ];

    render(<CoinFlip {...defaultProps} flipHistory={flipHistory} />);

    expect(screen.getByText('Heads')).toBeInTheDocument();
    expect(screen.getByText('Tails')).toBeInTheDocument();
    expect(screen.getByText('Total')).toBeInTheDocument();
  });
});

describe('WheelSpinner', () => {
  const defaultProps = {
    sessionId: 'session-1',
    participantId: 'user-1',
    participantName: 'Player 1',
  };

  it('renders spin button', () => {
    render(<WheelSpinner {...defaultProps} />);

    expect(screen.getByText('Spin Wheel')).toBeInTheDocument();
  });

  it('renders default options on the wheel', () => {
    render(<WheelSpinner {...defaultProps} />);

    // Default options are Option 1-4
    expect(screen.getByText('Option 1')).toBeInTheDocument();
    expect(screen.getByText('Option 2')).toBeInTheDocument();
    expect(screen.getByText('Option 3')).toBeInTheDocument();
    expect(screen.getByText('Option 4')).toBeInTheDocument();
  });

  it('renders with custom options', () => {
    const options: WheelOption[] = [
      { id: '1', label: 'Pizza', color: '#ff0000', weight: 1 },
      { id: '2', label: 'Burger', color: '#00ff00', weight: 1 },
    ];

    render(<WheelSpinner {...defaultProps} options={options} />);

    expect(screen.getByText('Pizza')).toBeInTheDocument();
    expect(screen.getByText('Burger')).toBeInTheDocument();
  });

  it('calls onSpin when button is clicked', async () => {
    const options: WheelOption[] = [
      { id: '1', label: 'Pizza', color: '#ff0000', weight: 1 },
      { id: '2', label: 'Burger', color: '#00ff00', weight: 1 },
    ];

    const onSpin = vi.fn().mockResolvedValue({
      id: 'spin-1',
      sessionId: 'session-1',
      participantId: 'user-1',
      participantName: 'Player 1',
      selectedOption: options[0],
      timestamp: new Date(),
    } as WheelSpinResult);

    const user = userEvent.setup();
    render(<WheelSpinner {...defaultProps} options={options} onSpin={onSpin} />);

    await user.click(screen.getByText('Spin Wheel'));

    await waitFor(() => {
      expect(onSpin).toHaveBeenCalledWith(options);
    });
  });

  it('disables spin with less than 2 options', () => {
    const options: WheelOption[] = [
      { id: '1', label: 'Solo', color: '#ff0000', weight: 1 },
    ];

    render(<WheelSpinner {...defaultProps} options={options} />);

    expect(screen.getByText('Spin Wheel')).toBeDisabled();
  });
});

describe('RandomTools', () => {
  const defaultProps = {
    sessionId: 'session-1',
    participantId: 'user-1',
    participantName: 'Player 1',
  };

  it('renders all three tool tabs', () => {
    render(<RandomTools {...defaultProps} />);

    expect(screen.getByText('Timer')).toBeInTheDocument();
    expect(screen.getByText('Coin')).toBeInTheDocument();
    expect(screen.getByText('Wheel')).toBeInTheDocument();
  });

  it('shows timer content by default', () => {
    render(<RandomTools {...defaultProps} />);

    expect(screen.getByText('Start Timer')).toBeInTheDocument();
  });

  it('switches to coin flip when tab is clicked', async () => {
    const user = userEvent.setup();
    render(<RandomTools {...defaultProps} />);

    await user.click(screen.getByText('Coin'));

    expect(screen.getByText('Flip Coin')).toBeInTheDocument();
  });

  it('switches to wheel spinner when tab is clicked', async () => {
    const user = userEvent.setup();
    render(<RandomTools {...defaultProps} />);

    await user.click(screen.getByText('Wheel'));

    expect(screen.getByText('Spin Wheel')).toBeInTheDocument();
  });

  it('displays title Random Tools', () => {
    render(<RandomTools {...defaultProps} />);

    expect(screen.getByText('Random Tools')).toBeInTheDocument();
  });
});
