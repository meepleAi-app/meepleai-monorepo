/**
 * SlotCards Component Tests
 * Issue #3240: [FRONT-004] Visual agent slot management
 * Issue #3247: [FRONT-011] LockedSlotCard integration
 */

import { render, screen, fireEvent } from '@testing-library/react';
import { describe, it, expect, vi, beforeEach } from 'vitest';
import React from 'react';

// Mock child components
vi.mock('../../slots/LockedSlotCard', () => ({
  LockedSlotCard: ({
    lockedCount,
    currentTier,
    onUpgradeClick,
  }: {
    lockedCount: number;
    currentTier: string;
    onUpgradeClick?: () => void;
  }) => (
    <div data-testid="locked-slot-card">
      <span data-testid="locked-count">{lockedCount}</span>
      <span data-testid="current-tier">{currentTier}</span>
      <button onClick={onUpgradeClick} data-testid="upgrade-button">
        Upgrade
      </button>
    </div>
  ),
}));

// Import after mocks
import { SlotCards } from '../SlotCards';

describe('SlotCards', () => {
  beforeEach(() => {
    vi.clearAllMocks();
  });

  describe('Rendering', () => {
    it('renders Agent Slots label', () => {
      render(<SlotCards />);
      expect(screen.getByText('Agent Slots')).toBeInTheDocument();
    });

    it('displays slot usage count', () => {
      render(<SlotCards />);
      // Mock data has 2 active slots out of 5 non-locked
      expect(screen.getByText('2 / 5 slots used')).toBeInTheDocument();
    });

    it('renders active slot cards with game info', () => {
      render(<SlotCards />);
      expect(screen.getByText('7 Wonders')).toBeInTheDocument();
      expect(screen.getByText('Rules Helper')).toBeInTheDocument();
      expect(screen.getByText('Splendor')).toBeInTheDocument();
      expect(screen.getByText('Strategy')).toBeInTheDocument();
    });

    it('renders robot emoji for active slots', () => {
      render(<SlotCards />);
      const robotEmojis = screen.getAllByText('🤖');
      expect(robotEmojis).toHaveLength(2);
    });

    it('renders available slots', () => {
      render(<SlotCards />);
      // 5 non-locked slots total, 2 active + 3 available
      const slots = screen.getAllByRole('generic').filter(el =>
        el.className.includes('rounded-lg') && el.className.includes('border-2')
      );
      expect(slots).toHaveLength(5);
    });
  });

  describe('LockedSlotCard Integration', () => {
    it('renders LockedSlotCard when there are locked slots', () => {
      render(<SlotCards />);
      expect(screen.getByTestId('locked-slot-card')).toBeInTheDocument();
    });

    it('passes correct locked count to LockedSlotCard', () => {
      render(<SlotCards />);
      // Mock data has 3 locked slots
      expect(screen.getByTestId('locked-count')).toHaveTextContent('3');
    });

    it('passes default tier to LockedSlotCard', () => {
      render(<SlotCards />);
      expect(screen.getByTestId('current-tier')).toHaveTextContent('free');
    });

    it('passes custom tier to LockedSlotCard', () => {
      render(<SlotCards currentTier="premium" />);
      expect(screen.getByTestId('current-tier')).toHaveTextContent('premium');
    });

    it('calls onUpgradeClick when upgrade button clicked', () => {
      const mockOnUpgrade = vi.fn();
      render(<SlotCards onUpgradeClick={mockOnUpgrade} />);

      fireEvent.click(screen.getByTestId('upgrade-button'));

      expect(mockOnUpgrade).toHaveBeenCalledTimes(1);
    });
  });

  describe('Slot States', () => {
    it('applies active styling to active slots', () => {
      render(<SlotCards />);
      // Check for active slot styling (cyan border)
      const slots = document.querySelectorAll('.border-cyan-400');
      expect(slots.length).toBe(2);
    });

    it('applies available styling to available slots', () => {
      render(<SlotCards />);
      // Check for available slot styling (slate border)
      const slots = document.querySelectorAll('.border-slate-700');
      expect(slots.length).toBe(3);
    });
  });

  describe('Default Props', () => {
    it('uses free as default tier', () => {
      render(<SlotCards />);
      expect(screen.getByTestId('current-tier')).toHaveTextContent('free');
    });

    it('handles missing onUpgradeClick gracefully', () => {
      render(<SlotCards />);
      expect(() => {
        fireEvent.click(screen.getByTestId('upgrade-button'));
      }).not.toThrow();
    });
  });
});
