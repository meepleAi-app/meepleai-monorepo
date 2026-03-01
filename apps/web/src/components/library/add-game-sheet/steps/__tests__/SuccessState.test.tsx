/**
 * Tests for SuccessState
 * Issue #4821: Step 3 Info & Save
 */

import { render, screen, fireEvent, act } from '@testing-library/react';
import { describe, expect, it, vi, beforeEach, afterEach } from 'vitest';

import { SuccessState } from '../SuccessState';

// Mock next/link
vi.mock('next/link', () => ({
  default: ({ children, href, ...props }: React.PropsWithChildren<{ href: string }>) => (
    <a href={href} {...props}>{children}</a>
  ),
}));

describe('SuccessState', () => {
  const defaultProps = {
    gameTitle: 'Catan',
    libraryEntryId: 'entry-123',
    gameId: 'game-abc',
    onAddAnother: vi.fn(),
    onAutoClose: vi.fn(),
    autoCloseDelay: 5000,
  };

  beforeEach(() => {
    vi.clearAllMocks();
    vi.useFakeTimers();
  });

  afterEach(() => {
    vi.useRealTimers();
  });

  it('renders success message with game title', () => {
    render(<SuccessState {...defaultProps} />);

    expect(screen.getByTestId('success-state')).toBeInTheDocument();
    expect(screen.getByText('Gioco aggiunto alla tua collezione!')).toBeInTheDocument();
    expect(screen.getByText('Catan')).toBeInTheDocument();
  });

  it('renders navigation links', () => {
    render(<SuccessState {...defaultProps} />);

    expect(screen.getByText('Vai alla collezione')).toBeInTheDocument();
    expect(screen.getByText('Vedi dettaglio gioco')).toBeInTheDocument();
    expect(screen.getByText('Aggiungi un altro gioco')).toBeInTheDocument();
  });

  it('hides game detail link when no gameId', () => {
    render(<SuccessState {...defaultProps} gameId={undefined} />);

    expect(screen.queryByText('Vedi dettaglio gioco')).not.toBeInTheDocument();
  });

  it('calls onAddAnother when button clicked', () => {
    render(<SuccessState {...defaultProps} />);

    fireEvent.click(screen.getByTestId('add-another-button'));
    expect(defaultProps.onAddAnother).toHaveBeenCalledTimes(1);
  });

  it('auto-closes after delay', () => {
    render(<SuccessState {...defaultProps} autoCloseDelay={3000} />);

    expect(defaultProps.onAutoClose).not.toHaveBeenCalled();

    act(() => {
      vi.advanceTimersByTime(3000);
    });

    expect(defaultProps.onAutoClose).toHaveBeenCalledTimes(1);
  });

  it('clears timer on unmount', () => {
    const { unmount } = render(<SuccessState {...defaultProps} />);

    unmount();

    act(() => {
      vi.advanceTimersByTime(10000);
    });

    expect(defaultProps.onAutoClose).not.toHaveBeenCalled();
  });

  it('renders collection link with correct href', () => {
    render(<SuccessState {...defaultProps} />);

    const link = screen.getByText('Vai alla collezione').closest('a');
    expect(link).toHaveAttribute('href', '/library');
  });

  it('renders game detail link with correct href', () => {
    render(<SuccessState {...defaultProps} />);

    const link = screen.getByText('Vedi dettaglio gioco').closest('a');
    expect(link).toHaveAttribute('href', '/library/games/game-abc');
  });
});
