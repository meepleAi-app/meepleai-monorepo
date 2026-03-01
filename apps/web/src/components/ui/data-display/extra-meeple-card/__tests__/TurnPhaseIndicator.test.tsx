/**
 * Tests for TurnPhaseIndicator component
 * Issue #4763 - Interactive Cards + Timer + Events Timeline UI + Phase 3 Tests
 */

import { render, screen } from '@testing-library/react';
import { describe, expect, it } from 'vitest';

import { TurnPhaseIndicator } from '../tabs/TurnPhaseIndicator';
import type { TurnPhaseState } from '../types';

// ============================================================================
// Test Data
// ============================================================================

const mockPhaseState: TurnPhaseState = {
  phases: ['Planning', 'Action', 'Resolution'],
  currentPhaseIndex: 1,
  currentTurnNumber: 3,
  totalTurns: 10,
};

// ============================================================================
// Tests
// ============================================================================

describe('TurnPhaseIndicator', () => {
  it('renders nothing when no state provided', () => {
    const { container } = render(<TurnPhaseIndicator />);

    expect(container.firstChild).toBeNull();
  });

  it('renders nothing when phases array is empty', () => {
    const { container } = render(
      <TurnPhaseIndicator state={{ phases: [], currentPhaseIndex: 0, currentTurnNumber: 1 }} />
    );

    expect(container.firstChild).toBeNull();
  });

  it('renders turn number', () => {
    render(<TurnPhaseIndicator state={mockPhaseState} />);

    expect(screen.getByText('Turn 3')).toBeInTheDocument();
  });

  it('renders total turns when provided', () => {
    render(<TurnPhaseIndicator state={mockPhaseState} />);

    expect(screen.getByText('of 10')).toBeInTheDocument();
  });

  it('hides total turns when not provided', () => {
    const noTotal = { ...mockPhaseState, totalTurns: undefined };
    render(<TurnPhaseIndicator state={noTotal} />);

    expect(screen.queryByText(/of \d+/)).not.toBeInTheDocument();
  });

  it('renders current phase name in header and bar', () => {
    render(<TurnPhaseIndicator state={mockPhaseState} />);

    // Current phase "Action" appears in header and phase bar
    const actionTexts = screen.getAllByText('Action');
    expect(actionTexts.length).toBe(2);
  });

  it('renders all phase labels', () => {
    render(<TurnPhaseIndicator state={mockPhaseState} />);

    expect(screen.getByText('Planning')).toBeInTheDocument();
    expect(screen.getAllByText('Action').length).toBe(2); // header + bar
    expect(screen.getByText('Resolution')).toBeInTheDocument();
  });

  it('renders phase step elements for each phase', () => {
    render(<TurnPhaseIndicator state={mockPhaseState} />);

    expect(screen.getByTestId('phase-step-0')).toBeInTheDocument();
    expect(screen.getByTestId('phase-step-1')).toBeInTheDocument();
    expect(screen.getByTestId('phase-step-2')).toBeInTheDocument();
  });

  it('renders progressbar role', () => {
    render(<TurnPhaseIndicator state={mockPhaseState} />);

    const progressbar = screen.getByRole('progressbar');
    expect(progressbar).toHaveAttribute('aria-valuenow', '2');
    expect(progressbar).toHaveAttribute('aria-valuemin', '1');
    expect(progressbar).toHaveAttribute('aria-valuemax', '3');
  });

  it('works with single phase', () => {
    const single: TurnPhaseState = {
      phases: ['Main'],
      currentPhaseIndex: 0,
      currentTurnNumber: 1,
    };
    render(<TurnPhaseIndicator state={single} />);

    // "Main" appears in header and bar
    expect(screen.getAllByText('Main').length).toBe(2);
    expect(screen.getByText('Turn 1')).toBeInTheDocument();
  });

  it('works with many phases', () => {
    const many: TurnPhaseState = {
      phases: ['Upkeep', 'Draw', 'Main1', 'Combat', 'Main2', 'End'],
      currentPhaseIndex: 3,
      currentTurnNumber: 5,
    };
    render(<TurnPhaseIndicator state={many} />);

    // "Combat" appears in header + bar
    expect(screen.getAllByText('Combat').length).toBe(2);
    expect(screen.getByTestId('phase-step-0')).toBeInTheDocument();
    expect(screen.getByTestId('phase-step-5')).toBeInTheDocument();
  });

  it('renders first phase as current when index is 0', () => {
    const firstPhase = { ...mockPhaseState, currentPhaseIndex: 0 };
    render(<TurnPhaseIndicator state={firstPhase} />);

    // "Planning" appears in header and bar
    const planningTexts = screen.getAllByText('Planning');
    expect(planningTexts.length).toBe(2);
  });
});
