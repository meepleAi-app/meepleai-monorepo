/**
 * Tests for SnapshotHistorySlider
 * Issue #4758 - Snapshot History Slider UI + Time Travel Mode
 */

import { fireEvent, render, screen, within } from '@testing-library/react';
import { describe, expect, it, vi } from 'vitest';

import type { SnapshotInfo } from '../../extra-meeple-card/types';
import { SnapshotHistorySlider } from '../SnapshotHistorySlider';

// ============================================================================
// Test Data
// ============================================================================

const mockSnapshots: SnapshotInfo[] = [
  {
    id: 'snap-1',
    snapshotNumber: 1,
    triggerType: 'turnEnd',
    description: 'End of turn 1',
    turnNumber: 1,
    createdAt: '2026-02-19T10:00:00Z',
  },
  {
    id: 'snap-2',
    snapshotNumber: 2,
    triggerType: 'manual',
    description: 'Manual save',
    createdAt: '2026-02-19T10:15:00Z',
  },
  {
    id: 'snap-3',
    snapshotNumber: 3,
    triggerType: 'automatic',
    description: 'Auto-save after scoring',
    turnNumber: 3,
    createdAt: '2026-02-19T10:30:00Z',
  },
];

// ============================================================================
// Tests
// ============================================================================

describe('SnapshotHistorySlider', () => {
  // --- Rendering ---

  it('renders the slider with correct number of dots', () => {
    render(<SnapshotHistorySlider snapshots={mockSnapshots} />);

    expect(screen.getByTestId('snapshot-dot-0')).toBeInTheDocument();
    expect(screen.getByTestId('snapshot-dot-1')).toBeInTheDocument();
    expect(screen.getByTestId('snapshot-dot-2')).toBeInTheDocument();
  });

  it('renders nothing when snapshots is empty', () => {
    const { container } = render(<SnapshotHistorySlider snapshots={[]} />);
    expect(container.firstChild).toBeNull();
  });

  it('renders navigation buttons', () => {
    render(<SnapshotHistorySlider snapshots={mockSnapshots} />);

    expect(screen.getByTestId('snapshot-prev')).toBeInTheDocument();
    expect(screen.getByTestId('snapshot-next')).toBeInTheDocument();
  });

  it('applies custom className', () => {
    render(
      <SnapshotHistorySlider snapshots={mockSnapshots} className="custom-class" />
    );

    expect(screen.getByTestId('snapshot-history-slider')).toHaveClass('custom-class');
  });

  it('renders with custom data-testid', () => {
    render(
      <SnapshotHistorySlider
        snapshots={mockSnapshots}
        data-testid="my-slider"
      />
    );

    expect(screen.getByTestId('my-slider')).toBeInTheDocument();
  });

  it('has accessible navigation role', () => {
    render(<SnapshotHistorySlider snapshots={mockSnapshots} />);

    expect(screen.getByRole('navigation', { name: /snapshot history/i })).toBeInTheDocument();
  });

  // --- Selection ---

  it('highlights selected snapshot dot', () => {
    render(<SnapshotHistorySlider snapshots={mockSnapshots} currentIndex={1} />);

    const selectedDot = screen.getByTestId('snapshot-dot-1');
    expect(selectedDot).toHaveAttribute('aria-current', 'step');
  });

  it('defaults to first snapshot when no currentIndex', () => {
    render(<SnapshotHistorySlider snapshots={mockSnapshots} />);

    const firstDot = screen.getByTestId('snapshot-dot-0');
    expect(firstDot).toHaveAttribute('aria-current', 'step');
  });

  it('calls onSelect when dot is clicked', () => {
    const onSelect = vi.fn();
    render(
      <SnapshotHistorySlider snapshots={mockSnapshots} onSelect={onSelect} />
    );

    fireEvent.click(screen.getByTestId('snapshot-dot-2'));
    expect(onSelect).toHaveBeenCalledWith(2);
  });

  // --- Navigation ---

  it('disables prev button at first snapshot', () => {
    render(<SnapshotHistorySlider snapshots={mockSnapshots} currentIndex={0} />);

    expect(screen.getByTestId('snapshot-prev')).toBeDisabled();
  });

  it('disables next button at last snapshot', () => {
    render(
      <SnapshotHistorySlider snapshots={mockSnapshots} currentIndex={2} />
    );

    expect(screen.getByTestId('snapshot-next')).toBeDisabled();
  });

  it('calls onSelect with previous index when prev clicked', () => {
    const onSelect = vi.fn();
    render(
      <SnapshotHistorySlider
        snapshots={mockSnapshots}
        currentIndex={2}
        onSelect={onSelect}
      />
    );

    fireEvent.click(screen.getByTestId('snapshot-prev'));
    expect(onSelect).toHaveBeenCalledWith(1);
  });

  it('calls onSelect with next index when next clicked', () => {
    const onSelect = vi.fn();
    render(
      <SnapshotHistorySlider
        snapshots={mockSnapshots}
        currentIndex={0}
        onSelect={onSelect}
      />
    );

    fireEvent.click(screen.getByTestId('snapshot-next'));
    expect(onSelect).toHaveBeenCalledWith(1);
  });

  // --- Accessibility ---

  it('has aria-labels on snapshot dots', () => {
    render(<SnapshotHistorySlider snapshots={mockSnapshots} />);

    expect(screen.getByTestId('snapshot-dot-0')).toHaveAttribute(
      'aria-label',
      'Snapshot #1: End of turn 1'
    );
  });

  it('has aria-labels on navigation buttons', () => {
    render(<SnapshotHistorySlider snapshots={mockSnapshots} />);

    expect(screen.getByLabelText('Previous snapshot')).toBeInTheDocument();
    expect(screen.getByLabelText('Next snapshot')).toBeInTheDocument();
  });

  // --- Collapsed mode ---

  it('shows collapsed range for >20 snapshots', () => {
    const manySnapshots = Array.from({ length: 25 }, (_, i) => ({
      id: `snap-${i}`,
      snapshotNumber: i + 1,
      triggerType: 'automatic',
      description: `Snapshot ${i + 1}`,
      createdAt: '2026-02-19T10:00:00Z',
    }));

    render(
      <SnapshotHistorySlider snapshots={manySnapshots} currentIndex={10} />
    );

    expect(screen.getByTestId('snapshot-collapsed-range')).toBeInTheDocument();
    expect(screen.getByText('11')).toBeInTheDocument(); // 1-based display
    expect(screen.getByText('25')).toBeInTheDocument(); // total
  });

  // --- Time Travel Toggle ---

  it('renders time travel toggle when onTimeTravelToggle provided', () => {
    render(
      <SnapshotHistorySlider
        snapshots={mockSnapshots}
        onTimeTravelToggle={vi.fn()}
      />
    );

    expect(screen.getByTestId('snapshot-time-travel-toggle')).toBeInTheDocument();
  });

  it('does not render time travel toggle when handler not provided', () => {
    render(<SnapshotHistorySlider snapshots={mockSnapshots} />);

    expect(screen.queryByTestId('snapshot-time-travel-toggle')).not.toBeInTheDocument();
  });

  it('calls onTimeTravelToggle when toggle clicked', () => {
    const onToggle = vi.fn();
    render(
      <SnapshotHistorySlider
        snapshots={mockSnapshots}
        isTimeTravelMode={false}
        onTimeTravelToggle={onToggle}
      />
    );

    fireEvent.click(screen.getByTestId('snapshot-time-travel-toggle'));
    expect(onToggle).toHaveBeenCalledWith(true);
  });

  it('shows active state when time travel is active', () => {
    render(
      <SnapshotHistorySlider
        snapshots={mockSnapshots}
        isTimeTravelMode={true}
        onTimeTravelToggle={vi.fn()}
      />
    );

    const toggle = screen.getByTestId('snapshot-time-travel-toggle');
    expect(toggle).toHaveAttribute('aria-pressed', 'true');
  });

  // --- Edge cases ---

  it('clamps currentIndex to valid range', () => {
    const onSelect = vi.fn();
    render(
      <SnapshotHistorySlider
        snapshots={mockSnapshots}
        currentIndex={100}
        onSelect={onSelect}
      />
    );

    // Should clamp to last index (2)
    const lastDot = screen.getByTestId('snapshot-dot-2');
    expect(lastDot).toHaveAttribute('aria-current', 'step');
  });

  it('handles single snapshot correctly', () => {
    render(
      <SnapshotHistorySlider snapshots={[mockSnapshots[0]]} currentIndex={0} />
    );

    expect(screen.getByTestId('snapshot-dot-0')).toBeInTheDocument();
    expect(screen.getByTestId('snapshot-prev')).toBeDisabled();
    expect(screen.getByTestId('snapshot-next')).toBeDisabled();
  });
});
