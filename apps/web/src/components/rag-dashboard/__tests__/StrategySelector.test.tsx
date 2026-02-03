/**
 * Tests for StrategySelector and StrategyCard components
 * Issue #3439: Strategy selector with tier-based filtering
 */

import React from 'react';

import { render, screen, fireEvent, within } from '@testing-library/react';
import userEvent from '@testing-library/user-event';
import { describe, it, expect, vi, beforeEach } from 'vitest';

import { StrategySelector } from '../StrategySelector';
import { StrategyCard } from '../StrategyCard';
import { STRATEGIES } from '../rag-data';

import type { RagStrategy, UserTier } from '../types';

describe('StrategySelector', () => {
  const defaultProps = {
    userTier: 'User' as UserTier,
    selectedStrategy: null as RagStrategy | null,
    onStrategySelect: vi.fn(),
  };

  beforeEach(() => {
    vi.clearAllMocks();
  });

  // =========================================================================
  // Rendering Tests
  // =========================================================================

  describe('Rendering', () => {
    it('should render the strategy selector card', () => {
      render(<StrategySelector {...defaultProps} />);

      expect(screen.getByText('Select Strategy')).toBeInTheDocument();
    });

    it('should display user tier badge', () => {
      render(<StrategySelector {...defaultProps} userTier="Editor" />);

      expect(screen.getByText('Editor Tier')).toBeInTheDocument();
    });

    it('should render all 6 strategy cards', () => {
      render(<StrategySelector {...defaultProps} userTier="Admin" />);

      expect(screen.getByText('FAST')).toBeInTheDocument();
      expect(screen.getByText('BALANCED')).toBeInTheDocument();
      expect(screen.getByText('PRECISE')).toBeInTheDocument();
      expect(screen.getByText('EXPERT')).toBeInTheDocument();
      expect(screen.getByText('CONSENSUS')).toBeInTheDocument();
      expect(screen.getByText('CUSTOM')).toBeInTheDocument();
    });

    it('should apply custom className', () => {
      const { container } = render(
        <StrategySelector {...defaultProps} className="custom-class" />
      );

      expect(container.querySelector('.custom-class')).toBeInTheDocument();
    });

    it('should show custom title and description', () => {
      render(
        <StrategySelector
          {...defaultProps}
          title="Choose a RAG Strategy"
          description="Select the best strategy for your needs"
        />
      );

      expect(screen.getByText('Choose a RAG Strategy')).toBeInTheDocument();
      expect(screen.getByText('Select the best strategy for your needs')).toBeInTheDocument();
    });
  });

  // =========================================================================
  // Tier-Based Access Tests
  // =========================================================================

  describe('Tier-Based Access', () => {
    it('should show authentication required for Anonymous tier', () => {
      render(<StrategySelector {...defaultProps} userTier="Anonymous" />);

      expect(screen.getByText('Authentication Required')).toBeInTheDocument();
      expect(screen.getByText(/Anonymous users cannot access the RAG system/)).toBeInTheDocument();
    });

    it('should show 2 available strategies for User tier', () => {
      render(<StrategySelector {...defaultProps} userTier="User" />);

      // Text is split across elements, so check individual parts
      expect(screen.getByText('2')).toBeInTheDocument();
      expect(screen.getByText(/of.*6 strategies available/)).toBeInTheDocument();
    });

    it('should show 3 available strategies for Editor tier', () => {
      render(<StrategySelector {...defaultProps} userTier="Editor" />);

      expect(screen.getByText('3')).toBeInTheDocument();
      expect(screen.getByText(/of.*6 strategies available/)).toBeInTheDocument();
    });

    it('should show 6 available strategies for Admin tier', () => {
      render(<StrategySelector {...defaultProps} userTier="Admin" />);

      expect(screen.getByText('6', { selector: '.font-medium' })).toBeInTheDocument();
      expect(screen.getByText(/of.*6 strategies available/)).toBeInTheDocument();
    });

    it('should show 5 available strategies for Premium tier', () => {
      render(<StrategySelector {...defaultProps} userTier="Premium" />);

      expect(screen.getByText('5')).toBeInTheDocument();
      expect(screen.getByText(/of.*6 strategies available/)).toBeInTheDocument();
    });
  });

  // =========================================================================
  // Selection Tests
  // =========================================================================

  describe('Selection', () => {
    it('should call onStrategySelect when available strategy is clicked', async () => {
      const onSelect = vi.fn();
      const user = userEvent.setup();

      render(
        <StrategySelector
          {...defaultProps}
          userTier="Admin"
          onStrategySelect={onSelect}
        />
      );

      const fastCard = screen.getByRole('button', { name: /FAST strategy/i });
      await user.click(fastCard);

      expect(onSelect).toHaveBeenCalledWith('FAST');
    });

    it('should not call onStrategySelect when unavailable strategy is clicked', async () => {
      const onSelect = vi.fn();
      const user = userEvent.setup();

      render(
        <StrategySelector
          {...defaultProps}
          userTier="User"
          onStrategySelect={onSelect}
        />
      );

      // PRECISE requires Editor tier
      const preciseCard = screen.getByRole('button', { name: /PRECISE strategy \(requires Editor tier\)/i });
      await user.click(preciseCard);

      expect(onSelect).not.toHaveBeenCalled();
    });

    it('should show selected strategy badge when strategy is selected', () => {
      render(
        <StrategySelector
          {...defaultProps}
          userTier="Admin"
          selectedStrategy="BALANCED"
        />
      );

      expect(screen.getByText(/Selected:/)).toBeInTheDocument();
      // Look for badge with strategy name
      const selectedBadge = screen.getByRole('button', { name: /BALANCED strategy/i });
      expect(selectedBadge).toHaveAttribute('aria-selected', 'true');
    });

    it('should call onUpgradeRequest when locked strategy is clicked', async () => {
      const onUpgrade = vi.fn();
      const user = userEvent.setup();

      render(
        <StrategySelector
          {...defaultProps}
          userTier="User"
          onUpgradeRequest={onUpgrade}
        />
      );

      // PRECISE requires Editor tier
      const preciseCard = screen.getByRole('button', { name: /PRECISE strategy \(requires Editor tier\)/i });
      await user.click(preciseCard);

      expect(onUpgrade).toHaveBeenCalledWith('PRECISE', 'Editor');
    });
  });

  // =========================================================================
  // Accessibility Tests
  // =========================================================================

  describe('Accessibility', () => {
    it('should have listbox role for strategy grid', () => {
      render(<StrategySelector {...defaultProps} userTier="Admin" />);

      expect(screen.getByRole('listbox', { name: /RAG Strategies/i })).toBeInTheDocument();
    });

    it('should have aria-selected on selected card', () => {
      render(
        <StrategySelector
          {...defaultProps}
          userTier="Admin"
          selectedStrategy="FAST"
        />
      );

      const fastCard = screen.getByRole('button', { name: /FAST strategy/i });
      expect(fastCard).toHaveAttribute('aria-selected', 'true');
    });

    it('should have aria-disabled on unavailable cards', () => {
      render(<StrategySelector {...defaultProps} userTier="User" />);

      const preciseCard = screen.getByRole('button', { name: /PRECISE strategy \(requires Editor tier\)/i });
      expect(preciseCard).toHaveAttribute('aria-disabled', 'true');
    });

    it('should support keyboard navigation with Enter key', async () => {
      const onSelect = vi.fn();
      const user = userEvent.setup();

      render(
        <StrategySelector
          {...defaultProps}
          userTier="Admin"
          onStrategySelect={onSelect}
        />
      );

      const fastCard = screen.getByRole('button', { name: /FAST strategy/i });
      fastCard.focus();
      await user.keyboard('{Enter}');

      expect(onSelect).toHaveBeenCalledWith('FAST');
    });

    it('should support keyboard navigation with Space key', async () => {
      const onSelect = vi.fn();
      const user = userEvent.setup();

      render(
        <StrategySelector
          {...defaultProps}
          userTier="Admin"
          onStrategySelect={onSelect}
        />
      );

      const balancedCard = screen.getByRole('button', { name: /BALANCED strategy/i });
      balancedCard.focus();
      await user.keyboard(' ');

      expect(onSelect).toHaveBeenCalledWith('BALANCED');
    });
  });

  // =========================================================================
  // Dropdown Variant Tests
  // =========================================================================

  describe('Dropdown Variant', () => {
    it('should render as dropdown when variant is dropdown', () => {
      render(
        <StrategySelector
          {...defaultProps}
          userTier="Admin"
          variant="dropdown"
        />
      );

      expect(screen.getByRole('combobox')).toBeInTheDocument();
      expect(screen.getByText('Select a strategy...')).toBeInTheDocument();
    });

    it('should show selected strategy in dropdown button', () => {
      render(
        <StrategySelector
          {...defaultProps}
          userTier="Admin"
          variant="dropdown"
          selectedStrategy="BALANCED"
        />
      );

      expect(screen.getByText('BALANCED')).toBeInTheDocument();
    });

    it('should open dropdown on click', async () => {
      const user = userEvent.setup();

      render(
        <StrategySelector
          {...defaultProps}
          userTier="Admin"
          variant="dropdown"
        />
      );

      const combobox = screen.getByRole('combobox');
      await user.click(combobox);

      expect(screen.getByRole('listbox', { name: /RAG Strategies/i })).toBeInTheDocument();
    });
  });

  // =========================================================================
  // Disabled State Tests
  // =========================================================================

  describe('Disabled State', () => {
    it('should not call onStrategySelect when disabled', async () => {
      const onSelect = vi.fn();
      const user = userEvent.setup();

      render(
        <StrategySelector
          {...defaultProps}
          userTier="Admin"
          onStrategySelect={onSelect}
          disabled={true}
        />
      );

      const fastCard = screen.getByRole('button', { name: /FAST strategy/i });
      await user.click(fastCard);

      expect(onSelect).not.toHaveBeenCalled();
    });
  });
});

// =========================================================================
// StrategyCard Tests
// =========================================================================

describe('StrategyCard', () => {
  const defaultCardProps = {
    strategy: STRATEGIES.FAST,
    isAvailable: true,
    onSelect: vi.fn(),
  };

  beforeEach(() => {
    vi.clearAllMocks();
  });

  describe('Rendering', () => {
    it('should render strategy name and icon', () => {
      render(<StrategyCard {...defaultCardProps} />);

      expect(screen.getByText('FAST')).toBeInTheDocument();
    });

    it('should render strategy description', () => {
      render(<StrategyCard {...defaultCardProps} />);

      expect(screen.getByText(STRATEGIES.FAST.description)).toBeInTheDocument();
    });

    it('should render token, latency, and accuracy details when showDetails is true', () => {
      render(<StrategyCard {...defaultCardProps} showDetails={true} />);

      expect(screen.getByText('Tokens')).toBeInTheDocument();
      expect(screen.getByText('Latency')).toBeInTheDocument();
      expect(screen.getByText('Accuracy')).toBeInTheDocument();
    });

    it('should not render details when showDetails is false', () => {
      render(<StrategyCard {...defaultCardProps} showDetails={false} />);

      expect(screen.queryByText('Tokens')).not.toBeInTheDocument();
      expect(screen.queryByText('Latency')).not.toBeInTheDocument();
    });
  });

  describe('Available State', () => {
    it('should not show lock icon when available', () => {
      render(<StrategyCard {...defaultCardProps} isAvailable={true} />);

      // Lock icon should not be present
      expect(screen.queryByText(/Upgrade to/)).not.toBeInTheDocument();
    });

    it('should call onSelect when clicked and available', async () => {
      const onSelect = vi.fn();
      const user = userEvent.setup();

      render(
        <StrategyCard
          {...defaultCardProps}
          isAvailable={true}
          onSelect={onSelect}
        />
      );

      const card = screen.getByRole('button', { name: /FAST strategy/i });
      await user.click(card);

      expect(onSelect).toHaveBeenCalledWith('FAST');
    });
  });

  describe('Unavailable State', () => {
    it('should show tier badge when unavailable', () => {
      render(
        <StrategyCard
          {...defaultCardProps}
          isAvailable={false}
          requiredTier="Editor"
        />
      );

      expect(screen.getByText('Editor')).toBeInTheDocument();
    });

    it('should show upgrade message when unavailable', () => {
      render(
        <StrategyCard
          {...defaultCardProps}
          isAvailable={false}
          requiredTier="Editor"
        />
      );

      expect(screen.getByText(/Upgrade to Editor to unlock/)).toBeInTheDocument();
    });

    it('should have reduced opacity when unavailable', () => {
      const { container } = render(
        <StrategyCard
          {...defaultCardProps}
          isAvailable={false}
          requiredTier="Editor"
        />
      );

      const card = container.querySelector('[role="button"]');
      expect(card).toHaveClass('opacity-60');
    });

    it('should call onUpgradeClick when clicked and unavailable', async () => {
      const onUpgrade = vi.fn();
      const user = userEvent.setup();

      render(
        <StrategyCard
          {...defaultCardProps}
          isAvailable={false}
          requiredTier="Editor"
          onUpgradeClick={onUpgrade}
        />
      );

      const card = screen.getByRole('button', { name: /FAST strategy/i });
      await user.click(card);

      expect(onUpgrade).toHaveBeenCalledWith('FAST', 'Editor');
    });
  });

  describe('Selected State', () => {
    it('should have aria-selected true when selected', () => {
      render(
        <StrategyCard
          {...defaultCardProps}
          isAvailable={true}
          isSelected={true}
        />
      );

      const card = screen.getByRole('button', { name: /FAST strategy/i });
      expect(card).toHaveAttribute('aria-selected', 'true');
    });

    it('should have ring styling when selected', () => {
      const { container } = render(
        <StrategyCard
          {...defaultCardProps}
          isAvailable={true}
          isSelected={true}
        />
      );

      const card = container.querySelector('[role="button"]');
      expect(card).toHaveClass('ring-2');
    });
  });

  describe('Accessibility', () => {
    it('should have button role', () => {
      render(<StrategyCard {...defaultCardProps} />);

      expect(screen.getByRole('button', { name: /FAST strategy/i })).toBeInTheDocument();
    });

    it('should have accessible name', () => {
      render(<StrategyCard {...defaultCardProps} />);

      expect(screen.getByRole('button', { name: /FAST strategy/i })).toBeInTheDocument();
    });

    it('should include tier requirement in accessible name when unavailable', () => {
      render(
        <StrategyCard
          {...defaultCardProps}
          isAvailable={false}
          requiredTier="Editor"
        />
      );

      expect(
        screen.getByRole('button', { name: /FAST strategy \(requires Editor tier\)/i })
      ).toBeInTheDocument();
    });

    it('should be focusable', () => {
      render(<StrategyCard {...defaultCardProps} />);

      const card = screen.getByRole('button', { name: /FAST strategy/i });
      expect(card).toHaveAttribute('tabIndex', '0');
    });
  });
});
