/**
 * QuickActions Component Tests
 * Issue #5587 — Live Game Session UI
 */

import { render, screen, fireEvent } from '@testing-library/react';
import { describe, it, expect, vi } from 'vitest';

import { QuickActions } from '../QuickActions';

describe('QuickActions', () => {
  const defaultProps = {
    isPaused: false,
    onOpenRules: vi.fn(),
    onAskArbiter: vi.fn(),
    onTogglePause: vi.fn(),
    onOpenScores: vi.fn(),
  };

  it('should render all four action buttons', () => {
    render(<QuickActions {...defaultProps} />);
    expect(screen.getByTestId('quick-action-rules')).toBeInTheDocument();
    expect(screen.getByTestId('quick-action-arbiter')).toBeInTheDocument();
    expect(screen.getByTestId('quick-action-pause')).toBeInTheDocument();
    expect(screen.getByTestId('quick-action-scores')).toBeInTheDocument();
  });

  it('should display Italian labels', () => {
    render(<QuickActions {...defaultProps} />);
    expect(screen.getByText('Regole')).toBeInTheDocument();
    expect(screen.getByText('Arbitro')).toBeInTheDocument();
    expect(screen.getByText('Pausa')).toBeInTheDocument();
    expect(screen.getByText('Punteggi')).toBeInTheDocument();
  });

  it('should show "Riprendi" when paused', () => {
    render(<QuickActions {...defaultProps} isPaused={true} />);
    expect(screen.getByText('Riprendi')).toBeInTheDocument();
    expect(screen.queryByText('Pausa')).not.toBeInTheDocument();
  });

  it('should call onOpenRules when Rules button is clicked', () => {
    render(<QuickActions {...defaultProps} />);
    fireEvent.click(screen.getByTestId('quick-action-rules'));
    expect(defaultProps.onOpenRules).toHaveBeenCalledTimes(1);
  });

  it('should call onAskArbiter when Arbiter button is clicked', () => {
    render(<QuickActions {...defaultProps} />);
    fireEvent.click(screen.getByTestId('quick-action-arbiter'));
    expect(defaultProps.onAskArbiter).toHaveBeenCalledTimes(1);
  });

  it('should call onTogglePause when Pause button is clicked', () => {
    render(<QuickActions {...defaultProps} />);
    fireEvent.click(screen.getByTestId('quick-action-pause'));
    expect(defaultProps.onTogglePause).toHaveBeenCalledTimes(1);
  });

  it('should call onOpenScores when Scores button is clicked', () => {
    render(<QuickActions {...defaultProps} />);
    fireEvent.click(screen.getByTestId('quick-action-scores'));
    expect(defaultProps.onOpenScores).toHaveBeenCalledTimes(1);
  });

  it('should disable buttons when loading', () => {
    render(<QuickActions {...defaultProps} isLoading={true} />);
    const buttons = screen.getAllByRole('button');
    buttons.forEach(button => {
      expect(button).toBeDisabled();
    });
  });

  it('should have data-testid on container', () => {
    render(<QuickActions {...defaultProps} />);
    expect(screen.getByTestId('quick-actions')).toBeInTheDocument();
  });
});
