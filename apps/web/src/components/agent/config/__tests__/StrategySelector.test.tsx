/**
 * StrategySelector Tests
 * Issue #3376
 */

import { render, screen, fireEvent } from '@testing-library/react';
import { describe, it, expect, vi, beforeEach } from 'vitest';

import { StrategySelector } from '../StrategySelector';

// Mock the agent store
const mockSetSelectedStrategy = vi.fn();
vi.mock('@/stores/agentStore', () => ({
  useAgentStore: () => ({
    selectedStrategyId: 'BALANCED',
    setSelectedStrategy: mockSetSelectedStrategy,
  }),
}));

describe('StrategySelector', () => {
  beforeEach(() => {
    vi.clearAllMocks();
  });

  it('renders all non-admin strategies by default', () => {
    render(<StrategySelector />);

    expect(screen.getByText('Fast')).toBeInTheDocument();
    expect(screen.getByText('Balanced')).toBeInTheDocument();
    expect(screen.getByText('Precise')).toBeInTheDocument();
    expect(screen.getByText('Expert')).toBeInTheDocument();
    expect(screen.getByText('Consensus')).toBeInTheDocument();
    // Custom should NOT be visible for non-admin
    expect(screen.queryByText('Custom')).not.toBeInTheDocument();
  });

  it('renders Custom strategy for admin users', () => {
    render(<StrategySelector isAdmin />);

    expect(screen.getByText('Custom')).toBeInTheDocument();
  });

  it('calls setSelectedStrategy when a strategy is clicked', () => {
    render(<StrategySelector />);

    fireEvent.click(screen.getByText('Fast'));

    expect(mockSetSelectedStrategy).toHaveBeenCalledWith('FAST');
  });

  it('displays strategy descriptions', () => {
    render(<StrategySelector />);

    expect(screen.getByText('Quick lookups, simple Q&A')).toBeInTheDocument();
    expect(screen.getByText('Standard gameplay questions')).toBeInTheDocument();
    expect(screen.getByText('Complex rules interpretation')).toBeInTheDocument();
  });

  it('shows strategy details when selected', () => {
    render(<StrategySelector />);

    // Should show metrics for BALANCED (the mocked selected strategy)
    expect(screen.getByText(/~2s/)).toBeInTheDocument();
    expect(screen.getByText(/85%/)).toBeInTheDocument();
    expect(screen.getByText(/\$0.002\/query/)).toBeInTheDocument();
  });

  it('has required indicator on label', () => {
    render(<StrategySelector />);

    expect(screen.getByText('Strategy')).toBeInTheDocument();
    expect(screen.getByText('*')).toBeInTheDocument();
  });

  it('renders all strategy icons', () => {
    render(<StrategySelector />);

    // Check that all 5 non-admin strategy buttons are rendered
    const buttons = screen.getAllByRole('button');
    expect(buttons).toHaveLength(5);
  });
});
