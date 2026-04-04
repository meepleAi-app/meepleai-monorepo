/**
 * SlotCards Component Tests
 * Issue #3240: [FRONT-004] Visual agent slot management
 * Issue #3247: [FRONT-011] LockedSlotCard integration
 */

import { render, screen, fireEvent } from '@testing-library/react';
import { describe, it, expect, vi, beforeEach } from 'vitest';
import { QueryClient, QueryClientProvider } from '@tanstack/react-query';
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

// Default mock slots data to replicate what real API returns
const mockSlotsData = {
  total: 5,
  used: 2,
  available: 3,
  slots: [
    {
      slotIndex: 0,
      status: 'active' as const,
      agentId: 'a1',
      agentName: 'Agent Alpha',
      gameId: 'game1',
    },
    {
      slotIndex: 1,
      status: 'active' as const,
      agentId: 'a2',
      agentName: 'Agent Beta',
      gameId: 'game2',
    },
    { slotIndex: 2, status: 'available' as const, agentId: null, agentName: null, gameId: null },
    { slotIndex: 3, status: 'available' as const, agentId: null, agentName: null, gameId: null },
    { slotIndex: 4, status: 'available' as const, agentId: null, agentName: null, gameId: null },
    { slotIndex: 5, status: 'locked' as const, agentId: null, agentName: null, gameId: null },
    { slotIndex: 6, status: 'locked' as const, agentId: null, agentName: null, gameId: null },
    { slotIndex: 7, status: 'locked' as const, agentId: null, agentName: null, gameId: null },
  ],
};

vi.mock('@/hooks/queries/useAgentSlots', () => ({
  useAgentSlots: () => ({
    data: mockSlotsData,
    isLoading: false,
  }),
}));

// Import after mocks
import { SlotCards } from '../SlotCards';

function createWrapper() {
  const client = new QueryClient({
    defaultOptions: { queries: { retry: false } },
  });
  return ({ children }: { children: React.ReactNode }) => (
    <QueryClientProvider client={client}>{children}</QueryClientProvider>
  );
}

describe('SlotCards', () => {
  beforeEach(() => {
    vi.clearAllMocks();
  });

  describe('Rendering', () => {
    it('renders Agent Slots label', () => {
      render(<SlotCards />, { wrapper: createWrapper() });
      expect(screen.getByText('Agent Slots')).toBeInTheDocument();
    });

    it('displays slot usage count from API data', () => {
      render(<SlotCards />, { wrapper: createWrapper() });
      // Mock data: 2 used out of 5 total
      expect(screen.getByText('2 / 5 slots used')).toBeInTheDocument();
    });

    it('renders active slot cards with agent names', () => {
      render(<SlotCards />, { wrapper: createWrapper() });
      expect(screen.getByText('Agent Alpha')).toBeInTheDocument();
      expect(screen.getByText('Agent Beta')).toBeInTheDocument();
    });

    it('renders robot emoji for active slots', () => {
      render(<SlotCards />, { wrapper: createWrapper() });
      const robotEmojis = screen.getAllByText('🤖');
      expect(robotEmojis).toHaveLength(2);
    });

    it('renders non-locked slot cards', () => {
      render(<SlotCards />, { wrapper: createWrapper() });
      // 5 non-locked slots: 2 active + 3 available
      const slots = document.querySelectorAll('.rounded-lg.border-2');
      expect(slots.length).toBe(5);
    });
  });

  describe('LockedSlotCard Integration', () => {
    it('renders LockedSlotCard when there are locked slots', () => {
      render(<SlotCards />, { wrapper: createWrapper() });
      expect(screen.getByTestId('locked-slot-card')).toBeInTheDocument();
    });

    it('passes correct locked count to LockedSlotCard', () => {
      render(<SlotCards />, { wrapper: createWrapper() });
      // Mock data has 3 locked slots
      expect(screen.getByTestId('locked-count')).toHaveTextContent('3');
    });

    it('passes default tier to LockedSlotCard', () => {
      render(<SlotCards />, { wrapper: createWrapper() });
      expect(screen.getByTestId('current-tier')).toHaveTextContent('free');
    });

    it('passes custom tier to LockedSlotCard', () => {
      render(<SlotCards currentTier="premium" />, { wrapper: createWrapper() });
      expect(screen.getByTestId('current-tier')).toHaveTextContent('premium');
    });

    it('calls onUpgradeClick when upgrade button clicked', () => {
      const mockOnUpgrade = vi.fn();
      render(<SlotCards onUpgradeClick={mockOnUpgrade} />, { wrapper: createWrapper() });

      fireEvent.click(screen.getByTestId('upgrade-button'));

      expect(mockOnUpgrade).toHaveBeenCalledTimes(1);
    });
  });

  describe('Slot States', () => {
    it('applies active styling to active slots', () => {
      render(<SlotCards />, { wrapper: createWrapper() });
      // Check for active slot styling (cyan border)
      const slots = document.querySelectorAll('.border-cyan-400');
      expect(slots.length).toBe(2);
    });

    it('applies available styling to available slots', () => {
      render(<SlotCards />, { wrapper: createWrapper() });
      // Check for available slot styling (slate border)
      const slots = document.querySelectorAll('.border-slate-700');
      expect(slots.length).toBe(3);
    });
  });

  describe('Default Props', () => {
    it('uses free as default tier', () => {
      render(<SlotCards />, { wrapper: createWrapper() });
      expect(screen.getByTestId('current-tier')).toHaveTextContent('free');
    });

    it('handles missing onUpgradeClick gracefully', () => {
      render(<SlotCards />, { wrapper: createWrapper() });
      expect(() => {
        fireEvent.click(screen.getByTestId('upgrade-button'));
      }).not.toThrow();
    });
  });

  describe('Loading State', () => {
    it('shows slot label when data is available', () => {
      render(<SlotCards />, { wrapper: createWrapper() });
      // Verify it renders correctly with real-ish API data from mock
      expect(screen.getByText('Agent Slots')).toBeInTheDocument();
    });
  });
});
