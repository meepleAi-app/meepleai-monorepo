/**
 * SessionHeader (game-night) Component Tests
 * Issue #5587 — Live Game Session UI
 */

import { render, screen } from '@testing-library/react';
import { describe, it, expect } from 'vitest';

import { SessionHeader } from '../SessionHeader';

describe('SessionHeader (game-night)', () => {
  const defaultProps = {
    gameName: 'Catan',
    turnNumber: 5,
    currentPhase: 'Scambi',
    status: 'Active' as const,
  };

  it('should render game name', () => {
    render(<SessionHeader {...defaultProps} />);
    expect(screen.getByText('Catan')).toBeInTheDocument();
  });

  it('should render turn number', () => {
    render(<SessionHeader {...defaultProps} />);
    expect(screen.getByText('Turno 5')).toBeInTheDocument();
  });

  it('should render current phase', () => {
    render(<SessionHeader {...defaultProps} />);
    expect(screen.getByText('Fase: Scambi')).toBeInTheDocument();
  });

  it('should render status badge', () => {
    render(<SessionHeader {...defaultProps} />);
    expect(screen.getByText('Active')).toBeInTheDocument();
  });

  it('should render Paused status', () => {
    render(<SessionHeader {...defaultProps} status="Paused" />);
    expect(screen.getByText('Paused')).toBeInTheDocument();
  });

  it('should render expansions when provided', () => {
    render(<SessionHeader {...defaultProps} expansions={['Marinai', 'Città e Cavalieri']} />);
    expect(screen.getByText('+ Marinai, Città e Cavalieri')).toBeInTheDocument();
  });

  it('should not render phase section when null', () => {
    render(<SessionHeader {...defaultProps} currentPhase={null} />);
    expect(screen.queryByText(/Fase:/)).not.toBeInTheDocument();
  });

  it('should have data-testid', () => {
    render(<SessionHeader {...defaultProps} />);
    expect(screen.getByTestId('live-session-header')).toBeInTheDocument();
  });
});
