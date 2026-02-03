/**
 * Tests for useRagStrategy hook
 * Issue #3439: Strategy selector with tier-based filtering
 */

import { renderHook } from '@testing-library/react';
import { describe, it, expect } from 'vitest';

import { useRagStrategy } from '../useRagStrategy';

import type { RagStrategy, UserTier } from '@/components/rag-dashboard/types';

describe('useRagStrategy', () => {
  // =========================================================================
  // getAvailableStrategies Tests
  // =========================================================================

  describe('getAvailableStrategies', () => {
    it('should return empty array for Anonymous tier', () => {
      const { result } = renderHook(() => useRagStrategy());
      const strategies = result.current.getAvailableStrategies('Anonymous');

      expect(strategies).toEqual([]);
    });

    it('should return FAST and BALANCED for User tier', () => {
      const { result } = renderHook(() => useRagStrategy());
      const strategies = result.current.getAvailableStrategies('User');

      expect(strategies).toContain('FAST');
      expect(strategies).toContain('BALANCED');
      expect(strategies).not.toContain('PRECISE');
      expect(strategies).not.toContain('EXPERT');
    });

    it('should return FAST, BALANCED, and PRECISE for Editor tier', () => {
      const { result } = renderHook(() => useRagStrategy());
      const strategies = result.current.getAvailableStrategies('Editor');

      expect(strategies).toContain('FAST');
      expect(strategies).toContain('BALANCED');
      expect(strategies).toContain('PRECISE');
      expect(strategies).not.toContain('EXPERT');
      expect(strategies).not.toContain('CONSENSUS');
    });

    it('should return all strategies for Admin tier', () => {
      const { result } = renderHook(() => useRagStrategy());
      const strategies = result.current.getAvailableStrategies('Admin');

      expect(strategies).toContain('FAST');
      expect(strategies).toContain('BALANCED');
      expect(strategies).toContain('PRECISE');
      expect(strategies).toContain('EXPERT');
      expect(strategies).toContain('CONSENSUS');
      expect(strategies).toContain('CUSTOM');
    });

    it('should return all non-CUSTOM strategies for Premium tier', () => {
      const { result } = renderHook(() => useRagStrategy());
      const strategies = result.current.getAvailableStrategies('Premium');

      expect(strategies).toContain('FAST');
      expect(strategies).toContain('BALANCED');
      expect(strategies).toContain('PRECISE');
      expect(strategies).toContain('EXPERT');
      expect(strategies).toContain('CONSENSUS');
      expect(strategies).not.toContain('CUSTOM');
    });
  });

  // =========================================================================
  // isStrategyAvailable Tests
  // =========================================================================

  describe('isStrategyAvailable', () => {
    it('should return false for Anonymous tier with any strategy', () => {
      const { result } = renderHook(() => useRagStrategy());

      expect(result.current.isStrategyAvailable('Anonymous', 'FAST')).toBe(false);
      expect(result.current.isStrategyAvailable('Anonymous', 'BALANCED')).toBe(false);
      expect(result.current.isStrategyAvailable('Anonymous', 'PRECISE')).toBe(false);
    });

    it('should return true for User tier with FAST strategy', () => {
      const { result } = renderHook(() => useRagStrategy());

      expect(result.current.isStrategyAvailable('User', 'FAST')).toBe(true);
    });

    it('should return true for User tier with BALANCED strategy', () => {
      const { result } = renderHook(() => useRagStrategy());

      expect(result.current.isStrategyAvailable('User', 'BALANCED')).toBe(true);
    });

    it('should return false for User tier with PRECISE strategy', () => {
      const { result } = renderHook(() => useRagStrategy());

      expect(result.current.isStrategyAvailable('User', 'PRECISE')).toBe(false);
    });

    it('should return true for Editor tier with PRECISE strategy', () => {
      const { result } = renderHook(() => useRagStrategy());

      expect(result.current.isStrategyAvailable('Editor', 'PRECISE')).toBe(true);
    });

    it('should return false for Editor tier with EXPERT strategy', () => {
      const { result } = renderHook(() => useRagStrategy());

      expect(result.current.isStrategyAvailable('Editor', 'EXPERT')).toBe(false);
    });

    it('should return true for Admin tier with CUSTOM strategy', () => {
      const { result } = renderHook(() => useRagStrategy());

      expect(result.current.isStrategyAvailable('Admin', 'CUSTOM')).toBe(true);
    });
  });

  // =========================================================================
  // getRequiredTier Tests
  // =========================================================================

  describe('getRequiredTier', () => {
    it('should return User for FAST strategy', () => {
      const { result } = renderHook(() => useRagStrategy());

      expect(result.current.getRequiredTier('FAST')).toBe('User');
    });

    it('should return User for BALANCED strategy', () => {
      const { result } = renderHook(() => useRagStrategy());

      expect(result.current.getRequiredTier('BALANCED')).toBe('User');
    });

    it('should return Editor for PRECISE strategy', () => {
      const { result } = renderHook(() => useRagStrategy());

      expect(result.current.getRequiredTier('PRECISE')).toBe('Editor');
    });

    it('should return Admin for EXPERT strategy', () => {
      const { result } = renderHook(() => useRagStrategy());

      expect(result.current.getRequiredTier('EXPERT')).toBe('Admin');
    });

    it('should return Admin for CONSENSUS strategy', () => {
      const { result } = renderHook(() => useRagStrategy());

      expect(result.current.getRequiredTier('CONSENSUS')).toBe('Admin');
    });

    it('should return Admin for CUSTOM strategy', () => {
      const { result } = renderHook(() => useRagStrategy());

      expect(result.current.getRequiredTier('CUSTOM')).toBe('Admin');
    });
  });

  // =========================================================================
  // hasRagAccess Tests
  // =========================================================================

  describe('hasRagAccess', () => {
    it('should return false for Anonymous tier', () => {
      const { result } = renderHook(() => useRagStrategy());

      expect(result.current.hasRagAccess('Anonymous')).toBe(false);
    });

    it('should return true for User tier', () => {
      const { result } = renderHook(() => useRagStrategy());

      expect(result.current.hasRagAccess('User')).toBe(true);
    });

    it('should return true for Editor tier', () => {
      const { result } = renderHook(() => useRagStrategy());

      expect(result.current.hasRagAccess('Editor')).toBe(true);
    });

    it('should return true for Admin tier', () => {
      const { result } = renderHook(() => useRagStrategy());

      expect(result.current.hasRagAccess('Admin')).toBe(true);
    });

    it('should return true for Premium tier', () => {
      const { result } = renderHook(() => useRagStrategy());

      expect(result.current.hasRagAccess('Premium')).toBe(true);
    });
  });

  // =========================================================================
  // getStrategyAccessInfo Tests
  // =========================================================================

  describe('getStrategyAccessInfo', () => {
    it('should return available info for User tier with FAST strategy', () => {
      const { result } = renderHook(() => useRagStrategy());
      const info = result.current.getStrategyAccessInfo('User', 'FAST');

      expect(info.isAvailable).toBe(true);
      expect(info.strategy).toBe('FAST');
      expect(info.requiredTier).toBeNull();
      expect(info.tierBadge).toBeNull();
    });

    it('should return unavailable info for User tier with PRECISE strategy', () => {
      const { result } = renderHook(() => useRagStrategy());
      const info = result.current.getStrategyAccessInfo('User', 'PRECISE');

      expect(info.isAvailable).toBe(false);
      expect(info.strategy).toBe('PRECISE');
      expect(info.requiredTier).toBe('Editor');
      expect(info.tierBadge).toBe('Requires Editor');
    });

    it('should return unavailable info for User tier with EXPERT strategy', () => {
      const { result } = renderHook(() => useRagStrategy());
      const info = result.current.getStrategyAccessInfo('User', 'EXPERT');

      expect(info.isAvailable).toBe(false);
      expect(info.strategy).toBe('EXPERT');
      expect(info.requiredTier).toBe('Admin');
      expect(info.tierBadge).toBe('Requires Admin');
    });

    it('should return available info for Admin tier with any strategy', () => {
      const { result } = renderHook(() => useRagStrategy());
      const strategies: RagStrategy[] = ['FAST', 'BALANCED', 'PRECISE', 'EXPERT', 'CONSENSUS', 'CUSTOM'];

      strategies.forEach(strategy => {
        const info = result.current.getStrategyAccessInfo('Admin', strategy);
        expect(info.isAvailable).toBe(true);
        expect(info.requiredTier).toBeNull();
      });
    });
  });

  // =========================================================================
  // getAllStrategiesWithAccess Tests
  // =========================================================================

  describe('getAllStrategiesWithAccess', () => {
    it('should return 6 strategies for any tier', () => {
      const { result } = renderHook(() => useRagStrategy());
      const tiers: UserTier[] = ['Anonymous', 'User', 'Editor', 'Admin', 'Premium'];

      tiers.forEach(tier => {
        const allStrategies = result.current.getAllStrategiesWithAccess(tier);
        expect(allStrategies).toHaveLength(6);
      });
    });

    it('should mark 0 strategies as available for Anonymous tier', () => {
      const { result } = renderHook(() => useRagStrategy());
      const allStrategies = result.current.getAllStrategiesWithAccess('Anonymous');
      const available = allStrategies.filter(s => s.isAvailable);

      expect(available).toHaveLength(0);
    });

    it('should mark 2 strategies as available for User tier', () => {
      const { result } = renderHook(() => useRagStrategy());
      const allStrategies = result.current.getAllStrategiesWithAccess('User');
      const available = allStrategies.filter(s => s.isAvailable);

      expect(available).toHaveLength(2);
    });

    it('should mark 3 strategies as available for Editor tier', () => {
      const { result } = renderHook(() => useRagStrategy());
      const allStrategies = result.current.getAllStrategiesWithAccess('Editor');
      const available = allStrategies.filter(s => s.isAvailable);

      expect(available).toHaveLength(3);
    });

    it('should mark 6 strategies as available for Admin tier', () => {
      const { result } = renderHook(() => useRagStrategy());
      const allStrategies = result.current.getAllStrategiesWithAccess('Admin');
      const available = allStrategies.filter(s => s.isAvailable);

      expect(available).toHaveLength(6);
    });

    it('should mark 5 strategies as available for Premium tier', () => {
      const { result } = renderHook(() => useRagStrategy());
      const allStrategies = result.current.getAllStrategiesWithAccess('Premium');
      const available = allStrategies.filter(s => s.isAvailable);

      expect(available).toHaveLength(5);
    });
  });

  // =========================================================================
  // getStrategyData Tests
  // =========================================================================

  describe('getStrategyData', () => {
    it('should return strategy data for FAST strategy', () => {
      const { result } = renderHook(() => useRagStrategy());
      const data = result.current.getStrategyData('FAST');

      expect(data).toBeDefined();
      expect(data?.id).toBe('FAST');
      expect(data?.name).toBe('FAST');
    });

    it('should return strategy data for all valid strategies', () => {
      const { result } = renderHook(() => useRagStrategy());
      const strategies: RagStrategy[] = ['FAST', 'BALANCED', 'PRECISE', 'EXPERT', 'CONSENSUS', 'CUSTOM'];

      strategies.forEach(strategy => {
        const data = result.current.getStrategyData(strategy);
        expect(data).toBeDefined();
        expect(data?.id).toBe(strategy);
      });
    });
  });
});
