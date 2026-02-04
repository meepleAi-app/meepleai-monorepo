/**
 * ModelTierSelector Tests
 * Issue #3376
 */

import { render, screen, fireEvent } from '@testing-library/react';
import { describe, it, expect, vi, beforeEach } from 'vitest';

import { ModelTierSelector } from '../ModelTierSelector';

// Mock the agent store
const mockSetSelectedModel = vi.fn();
const mockSetSelectedTier = vi.fn();
vi.mock('@/stores/agentStore', () => ({
  useAgentStore: () => ({
    selectedModelId: null,
    selectedTierId: 'free',
    setSelectedModel: mockSetSelectedModel,
    setSelectedTier: mockSetSelectedTier,
  }),
}));

describe('ModelTierSelector', () => {
  beforeEach(() => {
    vi.clearAllMocks();
  });

  it('renders tier buttons', () => {
    render(<ModelTierSelector />);

    expect(screen.getByText('Free')).toBeInTheDocument();
    expect(screen.getByText('Normal')).toBeInTheDocument();
    expect(screen.getByText('Premium')).toBeInTheDocument();
    // Custom should NOT be visible for non-admin
    expect(screen.queryByText('Custom')).not.toBeInTheDocument();
  });

  it('renders Custom tier for admin users', () => {
    render(<ModelTierSelector isAdmin />);

    expect(screen.getByText('Custom')).toBeInTheDocument();
  });

  it('disables tiers above user tier', () => {
    render(<ModelTierSelector userTier="free" />);

    // Free tier should be enabled
    const freeButton = screen.getByText('Free').closest('button');
    expect(freeButton).not.toBeDisabled();

    // Normal and Premium should be disabled
    const normalButton = screen.getByText('Normal').closest('button');
    const premiumButton = screen.getByText('Premium').closest('button');
    expect(normalButton).toBeDisabled();
    expect(premiumButton).toBeDisabled();
  });

  it('enables all tiers for premium users', () => {
    render(<ModelTierSelector userTier="premium" />);

    const freeButton = screen.getByText('Free').closest('button');
    const normalButton = screen.getByText('Normal').closest('button');
    const premiumButton = screen.getByText('Premium').closest('button');

    expect(freeButton).not.toBeDisabled();
    expect(normalButton).not.toBeDisabled();
    expect(premiumButton).not.toBeDisabled();
  });

  it('calls setSelectedTier when tier button is clicked', () => {
    render(<ModelTierSelector userTier="premium" />);

    fireEvent.click(screen.getByText('Normal'));

    expect(mockSetSelectedTier).toHaveBeenCalledWith('normal');
  });

  it('renders model selector with placeholder', () => {
    render(<ModelTierSelector />);

    expect(screen.getByText('Choose AI model...')).toBeInTheDocument();
  });

  it('has required indicator on model label', () => {
    render(<ModelTierSelector />);

    expect(screen.getByText('AI Model')).toBeInTheDocument();
    // There should be an asterisk for required
    const asterisks = screen.getAllByText('*');
    expect(asterisks.length).toBeGreaterThan(0);
  });

  it('shows upgrade message for non-premium users', () => {
    render(<ModelTierSelector userTier="free" />);

    expect(screen.getByText(/Upgrade to access premium models/)).toBeInTheDocument();
  });

  it('does not show upgrade message for premium users', () => {
    render(<ModelTierSelector userTier="premium" />);

    expect(screen.queryByText(/Upgrade to access premium models/)).not.toBeInTheDocument();
  });

  it('admin has access to all tiers', () => {
    render(<ModelTierSelector userTier="free" isAdmin />);

    // All buttons should be enabled for admin
    const freeButton = screen.getByText('Free').closest('button');
    const normalButton = screen.getByText('Normal').closest('button');
    const premiumButton = screen.getByText('Premium').closest('button');
    const customButton = screen.getByText('Custom').closest('button');

    expect(freeButton).not.toBeDisabled();
    expect(normalButton).not.toBeDisabled();
    expect(premiumButton).not.toBeDisabled();
    expect(customButton).not.toBeDisabled();
  });
});
