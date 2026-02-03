/**
 * SessionHeader Component Tests (Issue #3166 - GST-007)
 */

import { render, screen, fireEvent } from '@testing-library/react';
import { describe, it, expect, vi } from 'vitest';

import { SessionHeader } from '../SessionHeader';
import type { Session } from '../types';

describe('SessionHeader', () => {
  const mockSession: Session = {
    id: 'session-1',
    sessionCode: 'ABC123',
    sessionType: 'GameSpecific',
    gameName: '7 Wonders',
    gameIcon: '🏛️',
    sessionDate: new Date('2026-01-30T10:00:00Z'),
    status: 'Active',
    participantCount: 4,
  };

  it('should render session code', () => {
    render(<SessionHeader session={mockSession} />);
    expect(screen.getByText('ABC123')).toBeInTheDocument();
  });

  it('should display game name and icon', () => {
    render(<SessionHeader session={mockSession} />);
    expect(screen.getByText('7 Wonders')).toBeInTheDocument();
    expect(screen.getByText('🏛️')).toBeInTheDocument();
  });

  it('should show status badge', () => {
    render(<SessionHeader session={mockSession} />);
    expect(screen.getByText('Active')).toBeInTheDocument();
  });

  it('should format session date correctly', () => {
    render(<SessionHeader session={mockSession} />);
    expect(screen.getByText(/Jan 30/)).toBeInTheDocument();
  });

  it('should display participant count', () => {
    render(<SessionHeader session={mockSession} />);
    expect(screen.getByText(/4 players/)).toBeInTheDocument();
  });

  it('should render with action callbacks', () => {
    const onPause = vi.fn();
    const onFinalize = vi.fn();

    render(<SessionHeader session={mockSession} onPause={onPause} onFinalize={onFinalize} />);

    // Component renders successfully with callbacks
    expect(screen.getByText('ABC123')).toBeInTheDocument();
  });

  it('should render without action callbacks', () => {
    render(<SessionHeader session={mockSession} />);

    // Component renders successfully without callbacks
    expect(screen.getByText('ABC123')).toBeInTheDocument();
  });

  it('should handle generic session (no game name)', () => {
    const genericSession: Session = {
      ...mockSession,
      sessionType: 'Generic',
      gameName: undefined,
      gameIcon: undefined,
    };

    render(<SessionHeader session={genericSession} />);
    expect(screen.getByText('Game Session')).toBeInTheDocument();
  });

  it('should render for paused session', () => {
    const pausedSession: Session = {
      ...mockSession,
      status: 'Paused',
    };

    render(<SessionHeader session={pausedSession} />);
    expect(screen.getByText('Paused')).toBeInTheDocument();
  });

  it('should render for finalized session', () => {
    const finalizedSession: Session = {
      ...mockSession,
      status: 'Finalized',
    };

    render(<SessionHeader session={finalizedSession} />);
    expect(screen.getByText('Finalized')).toBeInTheDocument();
  });
});
