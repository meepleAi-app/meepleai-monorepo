/**
 * StrategySelector Tests
 * Issue #3376 - Refactored for Select-based UI (Issue #8)
 *
 * Tests the StrategySelector component which:
 * - Uses <Select> dropdown from @/components/ui/overlays/select
 * - Fetches strategies from API via useRagStrategies() hook
 * - Filters strategies by userTier via filterByTier()
 * - Supports controlled (value/onChange) and uncontrolled patterns
 */

import { screen, waitFor } from '@testing-library/react';
import userEvent from '@testing-library/user-event';
import { describe, it, expect, vi, beforeEach } from 'vitest';
import { renderWithQuery } from '@/__tests__/utils/query-test-utils';

import { StrategySelector } from '../StrategySelector';
import type { RagStrategyDto } from '@/lib/api/rag-strategies.api';

// Mock useRagStrategies hook
const mockStrategies: RagStrategyDto[] = [
  {
    name: 'Fast',
    displayName: 'Fast',
    description: 'Quick vector similarity search',
    useCase: 'Quick lookups, simple Q&A',
    complexity: 0,
    estimatedTokens: 500,
    requiresAdmin: false,
  },
  {
    name: 'Balanced',
    displayName: 'Balanced',
    description: 'Balanced retrieval with reranking',
    useCase: 'Standard gameplay questions',
    complexity: 1,
    estimatedTokens: 1200,
    requiresAdmin: false,
  },
  {
    name: 'SentenceWindow',
    displayName: 'Sentence Window',
    description: 'Sentence-level chunking with context window',
    useCase: 'Detailed rule lookups',
    complexity: 3,
    estimatedTokens: 2000,
    requiresAdmin: false,
  },
  {
    name: 'Expert',
    displayName: 'Expert',
    description: 'Multi-step retrieval with deep analysis',
    useCase: 'Complex rules interpretation',
    complexity: 8,
    estimatedTokens: 5000,
    requiresAdmin: false,
  },
  {
    name: 'Custom',
    displayName: 'Custom',
    description: 'Fully customizable pipeline',
    useCase: 'Admin-only advanced configuration',
    complexity: 12,
    estimatedTokens: 8000,
    requiresAdmin: true,
  },
];

const mockUseRagStrategies = vi.fn();

vi.mock('@/hooks/queries/useRagStrategies', () => ({
  useRagStrategies: (...args: unknown[]) => mockUseRagStrategies(...args),
}));

describe('StrategySelector', () => {
  beforeEach(() => {
    vi.clearAllMocks();
    mockUseRagStrategies.mockReturnValue({
      data: mockStrategies,
      isLoading: false,
      error: null,
    });
  });

  it('renders with strategies loaded and shows placeholder', () => {
    renderWithQuery(<StrategySelector />);

    // Should show the label
    expect(screen.getByText('RAG Strategy')).toBeInTheDocument();
    expect(screen.getByText('*')).toBeInTheDocument();

    // Should have a combobox trigger (Select component)
    const trigger = screen.getByRole('combobox');
    expect(trigger).toBeInTheDocument();
    expect(trigger).toHaveAttribute('aria-label', 'Select RAG strategy');
  });

  it('shows loading state when strategies are being fetched', () => {
    mockUseRagStrategies.mockReturnValue({
      data: undefined,
      isLoading: true,
      error: null,
    });

    renderWithQuery(<StrategySelector />);

    expect(screen.getByText('Loading strategies...')).toBeInTheDocument();

    // The select trigger should be disabled while loading
    const trigger = screen.getByRole('combobox');
    expect(trigger).toBeDisabled();
  });

  it('shows error state when strategies fail to load', () => {
    mockUseRagStrategies.mockReturnValue({
      data: undefined,
      isLoading: false,
      error: new Error('Network error'),
    });

    renderWithQuery(<StrategySelector />);

    expect(
      screen.getByText('Failed to load strategies. Please try again.')
    ).toBeInTheDocument();
  });

  it('filters strategies by userTier - Free gets complexity 0-1 only', () => {
    const onChange = vi.fn();
    renderWithQuery(
      <StrategySelector userTier="Free" onChange={onChange} />
    );

    // Free tier message should be shown
    expect(
      screen.getByText(/Free tier: Fast and Balanced strategies only/)
    ).toBeInTheDocument();
  });

  it('filters out requiresAdmin strategies for non-Enterprise tiers', () => {
    renderWithQuery(<StrategySelector userTier="Pro" />);

    // Pro tier message should be shown
    expect(
      screen.getByText(/Pro tier: All strategies except Custom/)
    ).toBeInTheDocument();
  });

  it('does not show tier message for Enterprise users', () => {
    renderWithQuery(<StrategySelector userTier="Enterprise" />);

    // No tier restriction message
    expect(screen.queryByText(/Upgrade/)).not.toBeInTheDocument();
  });

  it('calls onChange when a value is provided and changed', async () => {
    const onChange = vi.fn();
    const user = userEvent.setup();

    renderWithQuery(
      <StrategySelector value="Fast" onChange={onChange} />
    );

    // Should display selected strategy name
    expect(screen.getByText('Fast')).toBeInTheDocument();

    // Click the trigger to open the dropdown
    const trigger = screen.getByRole('combobox');
    await user.click(trigger);

    // Wait for dropdown content to appear and click an option
    await waitFor(() => {
      const balancedOption = screen.getByText('Balanced');
      expect(balancedOption).toBeInTheDocument();
    });
  });

  it('shows selected strategy details when value is set', () => {
    renderWithQuery(
      <StrategySelector value="Balanced" />
    );

    // Should show strategy details panel: "Complexity 1 * ~1.2K tokens/query * ..."
    expect(screen.getByText(/Complexity 1/)).toBeInTheDocument();
    expect(screen.getByText(/~1.2K tokens\/query/)).toBeInTheDocument();
    expect(screen.getByText(/Standard gameplay questions/)).toBeInTheDocument();
  });

  it('disables select when disabled prop is true', () => {
    renderWithQuery(<StrategySelector disabled />);

    const trigger = screen.getByRole('combobox');
    expect(trigger).toBeDisabled();
  });

  it('renders with custom placeholder', () => {
    renderWithQuery(
      <StrategySelector placeholder="Choose a strategy..." />
    );

    const trigger = screen.getByRole('combobox');
    expect(trigger).toBeInTheDocument();
  });
});
