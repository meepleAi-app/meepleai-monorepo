/**
 * Tests for TimeTravelOverlay
 * Issue #4758 - Snapshot History Slider UI + Time Travel Mode
 */

import { fireEvent, render, screen } from '@testing-library/react';
import { describe, expect, it, vi } from 'vitest';

import type { SnapshotInfo } from '../../extra-meeple-card/types';
import { TimeTravelOverlay } from '../TimeTravelOverlay';

// ============================================================================
// Test Data
// ============================================================================

const mockSnapshot: SnapshotInfo = {
  id: 'snap-3',
  snapshotNumber: 3,
  triggerType: 'turnEnd',
  description: 'End of turn 3 - Alice scored 15 points',
  turnNumber: 3,
  createdAt: new Date(Date.now() - 10 * 60 * 1000).toISOString(), // 10 min ago
};

const defaultProps = {
  snapshot: mockSnapshot,
  totalSnapshots: 5,
  isActive: true,
};

// ============================================================================
// Tests
// ============================================================================

describe('TimeTravelOverlay', () => {
  // --- Rendering ---

  it('renders when isActive is true', () => {
    render(<TimeTravelOverlay {...defaultProps} />);

    expect(screen.getByTestId('time-travel-overlay')).toBeInTheDocument();
  });

  it('does not render when isActive is false', () => {
    render(<TimeTravelOverlay {...defaultProps} isActive={false} />);

    expect(screen.queryByTestId('time-travel-overlay')).not.toBeInTheDocument();
  });

  it('renders the time travel badge with turn info', () => {
    render(<TimeTravelOverlay {...defaultProps} />);

    const badge = screen.getByTestId('time-travel-badge');
    expect(badge).toBeInTheDocument();
    expect(badge).toHaveTextContent('Turn 3');
  });

  it('renders snapshot number when no turn number', () => {
    const snapshotWithoutTurn = { ...mockSnapshot, turnNumber: undefined };
    render(
      <TimeTravelOverlay
        {...defaultProps}
        snapshot={snapshotWithoutTurn}
      />
    );

    const badge = screen.getByTestId('time-travel-badge');
    expect(badge).toHaveTextContent('Snapshot #3');
  });

  it('renders snapshot position indicator', () => {
    render(<TimeTravelOverlay {...defaultProps} />);

    expect(screen.getByText('3/5')).toBeInTheDocument();
  });

  it('renders description text', () => {
    render(<TimeTravelOverlay {...defaultProps} />);

    expect(screen.getByText('End of turn 3 - Alice scored 15 points')).toBeInTheDocument();
  });

  it('renders "Viewing historical state" label', () => {
    render(<TimeTravelOverlay {...defaultProps} />);

    expect(screen.getByText('Viewing historical state')).toBeInTheDocument();
  });

  it('renders the exit button', () => {
    render(<TimeTravelOverlay {...defaultProps} onExit={vi.fn()} />);

    expect(screen.getByTestId('time-travel-exit')).toBeInTheDocument();
    expect(screen.getByText('Present')).toBeInTheDocument();
  });

  // --- Time ago display ---

  it('shows time ago for recent snapshots', () => {
    render(<TimeTravelOverlay {...defaultProps} />);

    const badge = screen.getByTestId('time-travel-badge');
    expect(badge).toHaveTextContent(/\d+m ago/);
  });

  // --- Interactions ---

  it('calls onExit when exit button clicked', () => {
    const onExit = vi.fn();
    render(<TimeTravelOverlay {...defaultProps} onExit={onExit} />);

    fireEvent.click(screen.getByTestId('time-travel-exit'));
    expect(onExit).toHaveBeenCalledOnce();
  });

  it('calls onExit on Escape key press', () => {
    const onExit = vi.fn();
    render(<TimeTravelOverlay {...defaultProps} onExit={onExit} />);

    fireEvent.keyDown(document, { key: 'Escape' });
    expect(onExit).toHaveBeenCalledOnce();
  });

  it('does not call onExit on other key presses', () => {
    const onExit = vi.fn();
    render(<TimeTravelOverlay {...defaultProps} onExit={onExit} />);

    fireEvent.keyDown(document, { key: 'Enter' });
    expect(onExit).not.toHaveBeenCalled();
  });

  // --- Loading state ---

  it('shows loading state when isLoading', () => {
    render(
      <TimeTravelOverlay {...defaultProps} isLoading onExit={vi.fn()} />
    );

    expect(screen.getByText('Loading...')).toBeInTheDocument();
  });

  it('shows normal button text when not loading', () => {
    render(
      <TimeTravelOverlay {...defaultProps} isLoading={false} onExit={vi.fn()} />
    );

    expect(screen.getByText('Present')).toBeInTheDocument();
    expect(screen.queryByText('Loading...')).not.toBeInTheDocument();
  });

  // --- Accessibility ---

  it('has aria-live region for screen readers', () => {
    render(<TimeTravelOverlay {...defaultProps} />);

    // aria-live is on a separate sr-only element, not the pointer-events-none root
    const liveRegion = screen.getByText(/Viewing Turn 3/);
    expect(liveRegion).toHaveAttribute('aria-live', 'polite');
    expect(liveRegion).toHaveClass('sr-only');
  });

  it('has accessible exit button label', () => {
    render(<TimeTravelOverlay {...defaultProps} onExit={vi.fn()} />);

    expect(screen.getByLabelText('Return to present state')).toBeInTheDocument();
  });

  // --- Custom props ---

  it('applies custom className', () => {
    render(
      <TimeTravelOverlay {...defaultProps} className="custom-class" />
    );

    expect(screen.getByTestId('time-travel-overlay')).toHaveClass('custom-class');
  });

  it('applies custom data-testid', () => {
    render(
      <TimeTravelOverlay {...defaultProps} data-testid="my-overlay" />
    );

    expect(screen.getByTestId('my-overlay')).toBeInTheDocument();
  });

  // --- Cleanup ---

  it('cleans up event listener on unmount', () => {
    const onExit = vi.fn();
    const { unmount } = render(
      <TimeTravelOverlay {...defaultProps} onExit={onExit} />
    );

    unmount();

    // After unmount, keydown should not call onExit
    fireEvent.keyDown(document, { key: 'Escape' });
    expect(onExit).not.toHaveBeenCalled();
  });

  it('cleans up event listener when isActive becomes false', () => {
    const onExit = vi.fn();
    const { rerender } = render(
      <TimeTravelOverlay {...defaultProps} onExit={onExit} />
    );

    rerender(
      <TimeTravelOverlay {...defaultProps} isActive={false} onExit={onExit} />
    );

    fireEvent.keyDown(document, { key: 'Escape' });
    expect(onExit).not.toHaveBeenCalled();
  });
});
