/**
 * Tests for useTierStrategy hooks
 * Issue #3441: Tests for tier-strategy-model architecture
 */

import { QueryClient, QueryClientProvider } from '@tanstack/react-query';
import { renderHook, waitFor } from '@testing-library/react';
import { describe, it, expect, vi, beforeEach, afterEach } from 'vitest';
import React from 'react';

import type {
  TierStrategyMatrixDto,
  StrategyModelMappingDto,
  TierStrategyAccessDto,
  TierStrategyResetResultDto,
} from '@/lib/api';

// Mock functions with hoisted declaration
const mockGetMatrix = vi.fn();
const mockGetModelMappings = vi.fn();
const mockUpdateAccess = vi.fn();
const mockUpdateModelMapping = vi.fn();
const mockReset = vi.fn();

// Mock the api object before importing hooks
vi.mock('@/lib/api', () => ({
  api: {
    tierStrategy: {
      getMatrix: () => mockGetMatrix(),
      getModelMappings: () => mockGetModelMappings(),
      updateAccess: (req: unknown) => mockUpdateAccess(req),
      updateModelMapping: (req: unknown) => mockUpdateModelMapping(req),
      reset: (req?: unknown) => mockReset(req),
    },
  },
}));

// Import hooks after mocking
import {
  useTierStrategyMatrix,
  useStrategyModelMappings,
  useUpdateTierStrategyAccess,
  useUpdateStrategyModelMapping,
  useResetTierStrategyConfig,
} from '../useTierStrategy';

describe('useTierStrategy hooks', () => {
  let queryClient: QueryClient;

  const createWrapper = () => {
    return function Wrapper({ children }: { children: React.ReactNode }) {
      return React.createElement(
        QueryClientProvider,
        { client: queryClient },
        children
      );
    };
  };

  beforeEach(() => {
    queryClient = new QueryClient({
      defaultOptions: {
        queries: {
          retry: false,
        },
      },
    });
    vi.clearAllMocks();
  });

  afterEach(() => {
    queryClient.clear();
  });

  // =========================================================================
  // useTierStrategyMatrix Tests
  // =========================================================================

  describe('useTierStrategyMatrix', () => {
    const mockMatrix: TierStrategyMatrixDto = {
      tiers: ['Anonymous', 'User', 'Editor', 'Admin'],
      strategies: [
        {
          name: 'FAST',
          displayName: 'Fast',
          description: 'Quick responses',
          complexityLevel: 1,
          requiresAdmin: false,
        },
        {
          name: 'BALANCED',
          displayName: 'Balanced',
          description: 'Balanced quality',
          complexityLevel: 2,
          requiresAdmin: false,
        },
      ],
      accessMatrix: [
        { id: '1', tier: 'User', strategy: 'FAST', isEnabled: true, isDefault: true },
        { id: '2', tier: 'User', strategy: 'BALANCED', isEnabled: true, isDefault: true },
        { id: '3', tier: 'Editor', strategy: 'FAST', isEnabled: true, isDefault: true },
      ],
    };

    it('should fetch tier strategy matrix', async () => {
      mockGetMatrix.mockResolvedValue(mockMatrix);

      const { result } = renderHook(() => useTierStrategyMatrix(), {
        wrapper: createWrapper(),
      });

      await waitFor(() => expect(result.current.isSuccess).toBe(true));

      expect(result.current.data).toEqual(mockMatrix);
      expect(mockGetMatrix).toHaveBeenCalledTimes(1);
    });

    it('should handle fetch error', async () => {
      // Use 401 error to disable retry
      mockGetMatrix.mockRejectedValue(new Error('401 Unauthorized'));

      const { result } = renderHook(() => useTierStrategyMatrix(), {
        wrapper: createWrapper(),
      });

      await waitFor(() => expect(result.current.isError).toBe(true), {
        timeout: 2000,
      });

      expect(result.current.error).toBeDefined();
    });

    it('should return loading state initially', () => {
      mockGetMatrix.mockImplementation(() => new Promise(() => {})); // Never resolves

      const { result } = renderHook(() => useTierStrategyMatrix(), {
        wrapper: createWrapper(),
      });

      expect(result.current.isLoading).toBe(true);
      expect(result.current.data).toBeUndefined();
    });
  });

  // =========================================================================
  // useStrategyModelMappings Tests
  // =========================================================================

  describe('useStrategyModelMappings', () => {
    const mockMappings: StrategyModelMappingDto[] = [
      {
        id: '1',
        strategy: 'FAST',
        provider: 'OpenRouter',
        primaryModel: 'meta-llama/llama-3.3-70b-instruct:free',
        fallbackModels: [],
        isCustomizable: false,
        adminOnly: false,
        isDefault: true,
      },
      {
        id: '2',
        strategy: 'BALANCED',
        provider: 'DeepSeek',
        primaryModel: 'deepseek-chat',
        fallbackModels: ['openai/gpt-4o-mini'],
        isCustomizable: false,
        adminOnly: false,
        isDefault: true,
      },
    ];

    it('should fetch strategy model mappings', async () => {
      mockGetModelMappings.mockResolvedValue(mockMappings);

      const { result } = renderHook(() => useStrategyModelMappings(), {
        wrapper: createWrapper(),
      });

      await waitFor(() => expect(result.current.isSuccess).toBe(true));

      expect(result.current.data).toEqual(mockMappings);
      expect(mockGetModelMappings).toHaveBeenCalledTimes(1);
    });

    it('should handle empty mappings', async () => {
      mockGetModelMappings.mockResolvedValue([]);

      const { result } = renderHook(() => useStrategyModelMappings(), {
        wrapper: createWrapper(),
      });

      await waitFor(() => expect(result.current.isSuccess).toBe(true));

      expect(result.current.data).toEqual([]);
    });
  });

  // =========================================================================
  // useUpdateTierStrategyAccess Tests
  // =========================================================================

  describe('useUpdateTierStrategyAccess', () => {
    const mockUpdatedAccess: TierStrategyAccessDto = {
      id: '1',
      tier: 'User',
      strategy: 'PRECISE',
      isEnabled: true,
      isDefault: false,
    };

    it('should update tier strategy access', async () => {
      mockUpdateAccess.mockResolvedValue(mockUpdatedAccess);

      const { result } = renderHook(() => useUpdateTierStrategyAccess(), {
        wrapper: createWrapper(),
      });

      await result.current.mutateAsync({
        tier: 'User',
        strategy: 'PRECISE',
        isEnabled: true,
      });

      expect(mockUpdateAccess).toHaveBeenCalledWith({
        tier: 'User',
        strategy: 'PRECISE',
        isEnabled: true,
      });
    });

    it('should handle update error', async () => {
      mockUpdateAccess.mockRejectedValue(new Error('Update failed'));

      const { result } = renderHook(() => useUpdateTierStrategyAccess(), {
        wrapper: createWrapper(),
      });

      await expect(
        result.current.mutateAsync({
          tier: 'User',
          strategy: 'PRECISE',
          isEnabled: true,
        })
      ).rejects.toThrow('Update failed');
    });
  });

  // =========================================================================
  // useUpdateStrategyModelMapping Tests
  // =========================================================================

  describe('useUpdateStrategyModelMapping', () => {
    const mockUpdatedMapping: StrategyModelMappingDto = {
      id: '1',
      strategy: 'BALANCED',
      provider: 'Anthropic',
      primaryModel: 'anthropic/claude-sonnet-4.5',
      fallbackModels: ['openai/gpt-4o'],
      isCustomizable: false,
      adminOnly: false,
      isDefault: false,
    };

    it('should update strategy model mapping', async () => {
      mockUpdateModelMapping.mockResolvedValue(mockUpdatedMapping);

      const { result } = renderHook(() => useUpdateStrategyModelMapping(), {
        wrapper: createWrapper(),
      });

      await result.current.mutateAsync({
        strategy: 'BALANCED',
        provider: 'Anthropic',
        primaryModel: 'anthropic/claude-sonnet-4.5',
        fallbackModels: ['openai/gpt-4o'],
      });

      expect(mockUpdateModelMapping).toHaveBeenCalledWith({
        strategy: 'BALANCED',
        provider: 'Anthropic',
        primaryModel: 'anthropic/claude-sonnet-4.5',
        fallbackModels: ['openai/gpt-4o'],
      });
    });

    it('should handle update without fallback models', async () => {
      mockUpdateModelMapping.mockResolvedValue({
        ...mockUpdatedMapping,
        fallbackModels: [],
      });

      const { result } = renderHook(() => useUpdateStrategyModelMapping(), {
        wrapper: createWrapper(),
      });

      await result.current.mutateAsync({
        strategy: 'FAST',
        provider: 'OpenRouter',
        primaryModel: 'some-model',
      });

      expect(mockUpdateModelMapping).toHaveBeenCalledWith({
        strategy: 'FAST',
        provider: 'OpenRouter',
        primaryModel: 'some-model',
      });
    });
  });

  // =========================================================================
  // useResetTierStrategyConfig Tests
  // =========================================================================

  describe('useResetTierStrategyConfig', () => {
    const mockResetResult: TierStrategyResetResultDto = {
      accessEntriesDeleted: 10,
      modelMappingsDeleted: 5,
      message: 'Configuration reset successfully',
    };

    it('should reset tier strategy config', async () => {
      mockReset.mockResolvedValue(mockResetResult);

      const { result } = renderHook(() => useResetTierStrategyConfig(), {
        wrapper: createWrapper(),
      });

      const response = await result.current.mutateAsync(undefined);

      expect(response).toEqual(mockResetResult);
      expect(mockReset).toHaveBeenCalledTimes(1);
    });

    it('should reset with custom options', async () => {
      mockReset.mockResolvedValue({
        accessEntriesDeleted: 10,
        modelMappingsDeleted: 0,
        message: 'Access matrix reset successfully',
      });

      const { result } = renderHook(() => useResetTierStrategyConfig(), {
        wrapper: createWrapper(),
      });

      await result.current.mutateAsync({
        resetAccessMatrix: true,
        resetModelMappings: false,
      });

      expect(mockReset).toHaveBeenCalledWith({
        resetAccessMatrix: true,
        resetModelMappings: false,
      });
    });

    it('should handle reset error', async () => {
      mockReset.mockRejectedValue(new Error('Reset failed'));

      const { result } = renderHook(() => useResetTierStrategyConfig(), {
        wrapper: createWrapper(),
      });

      await expect(result.current.mutateAsync(undefined)).rejects.toThrow(
        'Reset failed'
      );
    });
  });
});
