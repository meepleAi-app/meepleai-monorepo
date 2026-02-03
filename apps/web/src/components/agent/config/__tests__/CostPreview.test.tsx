/**
 * CostPreview Tests
 * Issue #3376
 */

import { render, screen } from '@testing-library/react';
import { describe, it, expect, vi, beforeEach } from 'vitest';

import { CostPreview } from '../CostPreview';

// Mock the agent store with different configurations
const createMockStore = (overrides = {}) => ({
  selectedStrategyId: null,
  selectedTierId: null,
  selectedModelId: null,
  ...overrides,
});

let mockStore = createMockStore();

vi.mock('@/stores/agentStore', () => ({
  useAgentStore: () => mockStore,
}));

describe('CostPreview', () => {
  beforeEach(() => {
    vi.clearAllMocks();
    mockStore = createMockStore();
  });

  it('returns null when no selections are made', () => {
    mockStore = createMockStore({
      selectedStrategyId: null,
      selectedTierId: null,
      selectedModelId: null,
    });

    const { container } = render(<CostPreview />);
    expect(container.firstChild).toBeNull();
  });

  it('shows placeholder text when strategy or tier not selected', () => {
    mockStore = createMockStore({
      selectedStrategyId: 'FAST',
      selectedTierId: null,
      selectedModelId: 'gpt-4o-mini',
    });

    render(<CostPreview />);

    expect(
      screen.getByText('Select strategy and model tier to see cost estimate')
    ).toBeInTheDocument();
  });

  it('shows cost breakdown when strategy and tier are selected', () => {
    mockStore = createMockStore({
      selectedStrategyId: 'FAST',
      selectedTierId: 'free',
      selectedModelId: 'gpt-4o-mini',
    });

    render(<CostPreview />);

    expect(screen.getByText('Cost Preview')).toBeInTheDocument();
    expect(screen.getByText('Per query')).toBeInTheDocument();
    expect(screen.getByText(/Est\. daily/)).toBeInTheDocument();
    expect(screen.getByText('Est. monthly')).toBeInTheDocument();
  });

  it('shows low cost indicator for cheap configurations', () => {
    mockStore = createMockStore({
      selectedStrategyId: 'FAST',
      selectedTierId: 'free',
      selectedModelId: 'gpt-4o-mini',
    });

    render(<CostPreview />);

    // Should have green color for low cost
    const monthlyElement = screen.getByText('Est. monthly').nextElementSibling;
    expect(monthlyElement).toHaveClass('text-green-400');
  });

  it('shows warning for expensive configurations', () => {
    mockStore = createMockStore({
      selectedStrategyId: 'CONSENSUS',
      selectedTierId: 'premium',
      selectedModelId: 'claude-3-opus',
    });

    render(<CostPreview estimatedQueriesPerDay={100} />);

    // Should show warning for high cost
    expect(
      screen.getByText(/High estimated cost|Moderate cost/)
    ).toBeInTheDocument();
  });

  it('uses custom estimatedQueriesPerDay', () => {
    mockStore = createMockStore({
      selectedStrategyId: 'BALANCED',
      selectedTierId: 'free',
      selectedModelId: 'gpt-4o-mini',
    });

    render(<CostPreview estimatedQueriesPerDay={50} />);

    expect(screen.getByText(/50 queries/)).toBeInTheDocument();
  });

  it('calculates costs correctly for FAST + free tier', () => {
    mockStore = createMockStore({
      selectedStrategyId: 'FAST',
      selectedTierId: 'free',
      selectedModelId: 'gpt-4o-mini',
    });

    render(<CostPreview estimatedQueriesPerDay={10} />);

    // FAST multiplier = 1.0, free base = 0.0005
    // Per query = 0.0005 * 1.0 = $0.0005
    expect(screen.getByText('$0.0005')).toBeInTheDocument();
  });

  it('calculates costs correctly for CONSENSUS + premium tier', () => {
    mockStore = createMockStore({
      selectedStrategyId: 'CONSENSUS',
      selectedTierId: 'premium',
      selectedModelId: 'claude-3-opus',
    });

    render(<CostPreview estimatedQueriesPerDay={10} />);

    // CONSENSUS multiplier = 10.0, premium base = 0.005
    // Per query = 0.005 * 10.0 = $0.05
    expect(screen.getByText('$0.0500')).toBeInTheDocument();
  });

  it('applies custom className', () => {
    mockStore = createMockStore({
      selectedStrategyId: 'FAST',
      selectedTierId: 'free',
      selectedModelId: 'gpt-4o-mini',
    });

    render(<CostPreview className="custom-class" />);

    // The className is applied to the outer container (parent of header div)
    const header = screen.getByText('Cost Preview').closest('div');
    const container = header?.parentElement;
    expect(container).toHaveClass('custom-class');
  });
});
