/**
 * TurnIndicatorBar Component Tests (Issue #4975)
 *
 * Coverage:
 * - Renders player name and round when provided
 * - Shows placeholder when no activePlayerName
 * - End Turn button visibility (canEndTurn + onEndTurn both required)
 * - Callback triggered on End Turn click
 * - aria-live="polite" + aria-atomic for screen reader announcements
 */

import { describe, it, expect, vi } from 'vitest';
import { render, screen } from '@testing-library/react';
import userEvent from '@testing-library/user-event';

import { TurnIndicatorBar } from '../TurnIndicatorBar';

describe('TurnIndicatorBar', () => {
  // ── Default / uninitialised ─────────────────────────────────────────────────

  it('shows placeholder when activePlayerName is null', () => {
    render(<TurnIndicatorBar activePlayerName={null} />);
    expect(screen.getByText('Turn order not configured')).toBeInTheDocument();
  });

  it('shows placeholder when activePlayerName is undefined', () => {
    render(<TurnIndicatorBar />);
    expect(screen.getByText('Turn order not configured')).toBeInTheDocument();
  });

  // ── Active player + round ───────────────────────────────────────────────────

  it('shows player name when activePlayerName is set', () => {
    render(<TurnIndicatorBar activePlayerName="Alice" />);
    expect(screen.getByText('Alice')).toBeInTheDocument();
  });

  it('shows round number when roundNumber is provided', () => {
    render(<TurnIndicatorBar activePlayerName="Alice" roundNumber={3} />);
    expect(screen.getByText(/Round 3/)).toBeInTheDocument();
  });

  it('does not show round text when roundNumber is null', () => {
    render(<TurnIndicatorBar activePlayerName="Alice" roundNumber={null} />);
    expect(screen.queryByText(/Round/)).toBeNull();
  });

  it('does not show round text when roundNumber is undefined', () => {
    render(<TurnIndicatorBar activePlayerName="Alice" />);
    expect(screen.queryByText(/Round/)).toBeNull();
  });

  // ── End Turn button ─────────────────────────────────────────────────────────

  it('shows End Turn button when canEndTurn=true and onEndTurn is provided', () => {
    render(<TurnIndicatorBar canEndTurn onEndTurn={vi.fn()} />);
    expect(screen.getByRole('button', { name: 'End current turn' })).toBeInTheDocument();
  });

  it('does NOT show End Turn button when canEndTurn=false', () => {
    render(<TurnIndicatorBar canEndTurn={false} onEndTurn={vi.fn()} />);
    expect(screen.queryByRole('button')).toBeNull();
  });

  it('does NOT show End Turn button when onEndTurn is not provided', () => {
    render(<TurnIndicatorBar canEndTurn />);
    expect(screen.queryByRole('button')).toBeNull();
  });

  it('calls onEndTurn when End Turn button is clicked', async () => {
    const user = userEvent.setup();
    const onEndTurn = vi.fn();
    render(<TurnIndicatorBar canEndTurn onEndTurn={onEndTurn} />);

    await user.click(screen.getByRole('button', { name: 'End current turn' }));
    expect(onEndTurn).toHaveBeenCalledOnce();
  });

  // ── Accessibility ───────────────────────────────────────────────────────────

  it('container has aria-live="polite"', () => {
    const { container } = render(<TurnIndicatorBar />);
    const live = container.firstChild as HTMLElement;
    expect(live).toHaveAttribute('aria-live', 'polite');
  });

  it('container has aria-atomic="true"', () => {
    const { container } = render(<TurnIndicatorBar />);
    const live = container.firstChild as HTMLElement;
    expect(live).toHaveAttribute('aria-atomic', 'true');
  });

  // ── className ───────────────────────────────────────────────────────────────

  it('applies custom className', () => {
    const { container } = render(<TurnIndicatorBar className="test-bar" />);
    expect(container.querySelector('.test-bar')).not.toBeNull();
  });
});
