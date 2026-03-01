/**
 * Unit tests for RandomGeneratorWidget.
 * Issue #5156 — Epic B13.
 */

import { describe, it, expect, vi } from 'vitest';
import { render, screen, fireEvent } from '@testing-library/react';
import React from 'react';

import { RandomGeneratorWidget } from '../RandomGeneratorWidget';

// Mock framer-motion
vi.mock('framer-motion', () => ({
  motion: {
    div: ({ children, ...p }: { children?: React.ReactNode }) => <div {...p}>{children}</div>,
  },
  AnimatePresence: ({ children }: { children?: React.ReactNode }) => <>{children}</>,
}));

describe('RandomGeneratorWidget', () => {
  it('renders when enabled', () => {
    render(<RandomGeneratorWidget isEnabled={true} data-testid="rng" />);
    expect(screen.getByTestId('rng')).toBeInTheDocument();
    expect(screen.getByText('Random Generator')).toBeInTheDocument();
  });

  it('renders roll button with correct formula', () => {
    render(<RandomGeneratorWidget isEnabled={true} />);
    expect(screen.getByRole('button', { name: /roll 1d6/i })).toBeInTheDocument();
  });

  it('shows result after rolling', () => {
    render(<RandomGeneratorWidget isEnabled={true} />);
    fireEvent.click(screen.getByRole('button', { name: /roll/i }));
    expect(screen.getByTestId('roll-result')).toBeInTheDocument();
  });

  it('calls onStateChange when rolling', () => {
    const onStateChange = vi.fn();
    render(<RandomGeneratorWidget isEnabled={true} onStateChange={onStateChange} />);
    fireEvent.click(screen.getByRole('button', { name: /roll/i }));
    expect(onStateChange).toHaveBeenCalledTimes(1);
    expect(onStateChange).toHaveBeenCalledWith(expect.stringContaining('lastRoll'));
  });

  it('calls onToggle when switch is toggled', () => {
    const onToggle = vi.fn();
    render(<RandomGeneratorWidget isEnabled={true} onToggle={onToggle} />);
    fireEvent.click(screen.getByRole('switch'));
    expect(onToggle).toHaveBeenCalledWith(false);
  });

  it('increments dice count', () => {
    render(<RandomGeneratorWidget isEnabled={true} />);
    const incBtn = screen.getByRole('button', { name: /increase dice count/i });
    fireEvent.click(incBtn);
    expect(screen.getByLabelText('Dice count')).toHaveTextContent('2');
  });

  it('decrements dice count but not below 1', () => {
    render(<RandomGeneratorWidget isEnabled={true} />);
    const decBtn = screen.getByRole('button', { name: /decrease dice count/i });
    fireEvent.click(decBtn);
    expect(screen.getByLabelText('Dice count')).toHaveTextContent('1');
  });

  it('clears history', () => {
    render(<RandomGeneratorWidget isEnabled={true} />);
    // Roll first to create history
    fireEvent.click(screen.getByRole('button', { name: /roll/i }));
    // Open history
    fireEvent.click(screen.getByRole('button', { name: /show roll history/i }));
    expect(screen.getByText(/history \(1\)/i)).toBeInTheDocument();
    // Clear
    fireEvent.click(screen.getByRole('button', { name: /clear roll history/i }));
    expect(screen.queryByTestId('roll-result')).not.toBeInTheDocument();
  });
});
