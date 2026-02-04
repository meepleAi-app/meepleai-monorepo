/**
 * useCostEstimate Hook - Unit Tests
 * Issue #3383: Cost Estimation Preview Before Launch
 */

import { renderHook, waitFor } from '@testing-library/react';
import { describe, it, expect, vi, beforeEach, afterEach } from 'vitest';

import {
  useCostEstimate,
  calculateSessionCost,
  getCostWarningLevel,
  costEstimateKeys,
} from '../useCostEstimate';

// Mock fetch globally
const mockFetch = vi.fn();
global.fetch = mockFetch;

// Mock TanStack Query
vi.mock('@tanstack/react-query', async () => {
  const actual = await vi.importActual('@tanstack/react-query');
  return {
    ...actual,
    useQuery: vi.fn(),
  };
});

const { useQuery } = await import('@tanstack/react-query');

describe('useCostEstimate', () => {
  beforeEach(() => {
    vi.clearAllMocks();
  });

  afterEach(() => {
    vi.restoreAllMocks();
  });

  describe('Query Key Factory', () => {
    it('generates correct query keys', () => {
      const typologyId = '123e4567-e89b-12d3-a456-426614174000';

      expect(costEstimateKeys.all).toEqual(['costEstimate']);
      expect(costEstimateKeys.byTypology(typologyId)).toEqual(['costEstimate', typologyId]);
    });
  });

  describe('Hook Behavior', () => {
    it('calls useQuery with correct parameters when typologyId provided', () => {
      const typologyId = '123e4567-e89b-12d3-a456-426614174000';
      const mockUseQuery = useQuery as ReturnType<typeof vi.fn>;

      mockUseQuery.mockReturnValue({
        data: null,
        isLoading: false,
        isError: false,
        error: null,
      } as any);

      renderHook(() => useCostEstimate(typologyId));

      expect(mockUseQuery).toHaveBeenCalledWith(
        expect.objectContaining({
          queryKey: ['costEstimate', typologyId],
          enabled: true,
          staleTime: 30000,
          gcTime: 300000,
        })
      );
    });

    it('disables query when typologyId is null', () => {
      const mockUseQuery = useQuery as ReturnType<typeof vi.fn>;

      mockUseQuery.mockReturnValue({
        data: null,
        isLoading: false,
        isError: false,
        error: null,
      } as any);

      renderHook(() => useCostEstimate(null));

      expect(mockUseQuery).toHaveBeenCalledWith(
        expect.objectContaining({
          enabled: false,
        })
      );
    });

    it('disables query when enabled is false', () => {
      const typologyId = '123e4567-e89b-12d3-a456-426614174000';
      const mockUseQuery = useQuery as ReturnType<typeof vi.fn>;

      mockUseQuery.mockReturnValue({
        data: null,
        isLoading: false,
        isError: false,
        error: null,
      } as any);

      renderHook(() => useCostEstimate(typologyId, false));

      expect(mockUseQuery).toHaveBeenCalledWith(
        expect.objectContaining({
          enabled: false,
        })
      );
    });
  });

  describe('calculateSessionCost', () => {
    it('calculates session cost with default queries', () => {
      const perQueryCost = 0.05;
      const sessionCost = calculateSessionCost(perQueryCost);

      expect(sessionCost).toBe(0.25); // 0.05 * 5 (default)
    });

    it('calculates session cost with custom queries', () => {
      const perQueryCost = 0.02;
      const estimatedQueries = 10;
      const sessionCost = calculateSessionCost(perQueryCost, estimatedQueries);

      expect(sessionCost).toBe(0.2); // 0.02 * 10
    });

    it('handles zero cost', () => {
      const sessionCost = calculateSessionCost(0, 10);

      expect(sessionCost).toBe(0);
    });

    it('handles fractional costs correctly', () => {
      const perQueryCost = 0.043;
      const sessionCost = calculateSessionCost(perQueryCost, 7);

      expect(sessionCost).toBeCloseTo(0.301, 3); // 0.043 * 7
    });
  });

  describe('getCostWarningLevel', () => {
    it('returns "low" for costs under $0.20', () => {
      expect(getCostWarningLevel(0.05)).toBe('low');
      expect(getCostWarningLevel(0.19)).toBe('low');
      expect(getCostWarningLevel(0.0)).toBe('low');
    });

    it('returns "medium" for costs between $0.20 and $0.49', () => {
      expect(getCostWarningLevel(0.2)).toBe('medium');
      expect(getCostWarningLevel(0.35)).toBe('medium');
      expect(getCostWarningLevel(0.49)).toBe('medium');
    });

    it('returns "high" for costs $0.50 and above', () => {
      expect(getCostWarningLevel(0.5)).toBe('high');
      expect(getCostWarningLevel(1.0)).toBe('high');
      expect(getCostWarningLevel(5.0)).toBe('high');
    });

    it('handles exact boundary values correctly', () => {
      expect(getCostWarningLevel(0.199999)).toBe('low');
      expect(getCostWarningLevel(0.2)).toBe('medium');
      expect(getCostWarningLevel(0.499999)).toBe('medium');
      expect(getCostWarningLevel(0.5)).toBe('high');
    });
  });

  describe('Caching Configuration', () => {
    it('configures correct staleTime', () => {
      const mockUseQuery = useQuery as ReturnType<typeof vi.fn>;

      mockUseQuery.mockReturnValue({
        data: null,
        isLoading: false,
        isError: false,
        error: null,
      } as any);

      renderHook(() => useCostEstimate('123'));

      expect(mockUseQuery).toHaveBeenCalledWith(
        expect.objectContaining({
          staleTime: 30 * 1000, // 30 seconds
          gcTime: 5 * 60 * 1000, // 5 minutes
        })
      );
    });
  });
});
