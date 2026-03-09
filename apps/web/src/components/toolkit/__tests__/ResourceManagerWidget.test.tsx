/**
 * Unit tests for ResourceManagerWidget.
 * Issue #5156 — Epic B13.
 */

import { describe, it, expect, vi } from 'vitest';
import { render, screen, fireEvent } from '@testing-library/react';
import React from 'react';

vi.mock('@/lib/hooks/useWidgetSync', () => ({
  useWidgetSync: () => ({ broadcastState: vi.fn(), isConnected: false }),
}));

import { ResourceManagerWidget } from '../ResourceManagerWidget';

describe('ResourceManagerWidget', () => {
  it('renders default resources', () => {
    render(<ResourceManagerWidget isEnabled={true} />);
    expect(screen.getByText('Meeples')).toBeInTheDocument();
    expect(screen.getByText('Tokens')).toBeInTheDocument();
  });

  it('starts all counters at zero', () => {
    render(<ResourceManagerWidget isEnabled={true} />);
    const zeros = screen.getAllByText('0');
    expect(zeros.length).toBeGreaterThanOrEqual(2);
  });

  it('increments a resource count', () => {
    const onStateChange = vi.fn();
    render(<ResourceManagerWidget isEnabled={true} onStateChange={onStateChange} />);
    fireEvent.click(screen.getByLabelText('Increase Meeples'));
    expect(screen.getByLabelText('Meeples count')).toHaveTextContent('1');
    expect(onStateChange).toHaveBeenCalledTimes(1);
  });

  it('decrements but not below zero', () => {
    render(<ResourceManagerWidget isEnabled={true} />);
    // Count is 0; decrement should stay at 0
    fireEvent.click(screen.getByLabelText('Decrease Meeples'));
    expect(screen.getByLabelText('Meeples count')).toHaveTextContent('0');
  });

  it('adds a new resource', () => {
    render(<ResourceManagerWidget isEnabled={true} />);
    const input = screen.getByLabelText('New resource name');
    fireEvent.change(input, { target: { value: 'Wood' } });
    fireEvent.click(screen.getByLabelText('Add resource'));
    expect(screen.getByText('Wood')).toBeInTheDocument();
  });

  it('removes a resource', () => {
    render(<ResourceManagerWidget isEnabled={true} />);
    fireEvent.click(screen.getByLabelText('Remove Meeples'));
    expect(screen.queryByText('Meeples')).not.toBeInTheDocument();
  });

  it('shows reset button after incrementing', () => {
    render(<ResourceManagerWidget isEnabled={true} />);
    expect(screen.queryByLabelText('Reset all resources to zero')).not.toBeInTheDocument();
    fireEvent.click(screen.getByLabelText('Increase Meeples'));
    expect(screen.getByLabelText('Reset all resources to zero')).toBeInTheDocument();
  });

  it('resets all resources to zero', () => {
    render(<ResourceManagerWidget isEnabled={true} />);
    fireEvent.click(screen.getByLabelText('Increase Meeples'));
    fireEvent.click(screen.getByLabelText('Reset all resources to zero'));
    expect(screen.getByLabelText('Meeples count')).toHaveTextContent('0');
  });
});
