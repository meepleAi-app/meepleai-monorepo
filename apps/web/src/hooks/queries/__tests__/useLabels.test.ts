/**
 * useLabels Hook Tests (Issue #3516)
 *
 * Coverage: React Query hooks for game labels
 */

import { describe, it, expect, vi, beforeEach } from 'vitest';
import { renderHook, waitFor } from '@testing-library/react';
import { QueryClient, QueryClientProvider } from '@tanstack/react-query';
import React from 'react';

import {
  useLabels,
  useGameLabels,
  useAddLabelToGame,
  useRemoveLabelFromGame,
  useCreateCustomLabel,
  useDeleteCustomLabel,
  labelKeys,
} from '../useLabels';
import type { LabelDto } from '@/lib/api/schemas/library.schemas';

// Mock the API
vi.mock('@/lib/api', () => ({
  api: {
    library: {
      getLabels: vi.fn(),
      getGameLabels: vi.fn(),
      addLabelToGame: vi.fn(),
      removeLabelFromGame: vi.fn(),
      createCustomLabel: vi.fn(),
      deleteCustomLabel: vi.fn(),
    },
  },
}));

import { api } from '@/lib/api';

const mockLabels: LabelDto[] = [
  {
    id: 'label-1',
    name: 'Strategy',
    color: '#3b82f6',
    isPredefined: true,
    createdAt: '2024-01-15T10:00:00Z',
  },
  {
    id: 'label-2',
    name: 'Family',
    color: '#22c55e',
    isPredefined: true,
    createdAt: '2024-01-15T10:00:00Z',
  },
  {
    id: 'label-3',
    name: 'My Custom',
    color: '#f59e0b',
    isPredefined: false,
    createdAt: '2024-01-16T10:00:00Z',
  },
];

function createTestQueryClient() {
  return new QueryClient({
    defaultOptions: {
      queries: {
        retry: false,
        gcTime: 0,
      },
      mutations: {
        retry: false,
      },
    },
  });
}

function createWrapper() {
  const queryClient = createTestQueryClient();
  return function Wrapper({ children }: { children: React.ReactNode }) {
    return React.createElement(
      QueryClientProvider,
      { client: queryClient },
      children
    );
  };
}

describe('useLabels - Issue #3516', () => {
  beforeEach(() => {
    vi.clearAllMocks();
  });

  describe('labelKeys', () => {
    it('should generate correct query keys', () => {
      expect(labelKeys.all).toEqual(['labels']);
      expect(labelKeys.list()).toEqual(['labels', 'list']);
      expect(labelKeys.gameLabels('game-123')).toEqual(['labels', 'game', 'game-123']);
    });
  });

  describe('useLabels', () => {
    it('should fetch all labels', async () => {
      vi.mocked(api.library.getLabels).mockResolvedValue(mockLabels);

      const { result } = renderHook(() => useLabels(), {
        wrapper: createWrapper(),
      });

      await waitFor(() => {
        expect(result.current.isSuccess).toBe(true);
      });

      expect(result.current.data).toEqual(mockLabels);
      expect(api.library.getLabels).toHaveBeenCalledTimes(1);
    });

    it('should not fetch when disabled', async () => {
      const { result } = renderHook(() => useLabels(false), {
        wrapper: createWrapper(),
      });

      expect(result.current.fetchStatus).toBe('idle');
      expect(api.library.getLabels).not.toHaveBeenCalled();
    });
  });

  describe('useGameLabels', () => {
    it('should fetch labels for a specific game', async () => {
      const gameLabels = mockLabels.slice(0, 2);
      vi.mocked(api.library.getGameLabels).mockResolvedValue(gameLabels);

      const { result } = renderHook(() => useGameLabels('game-123'), {
        wrapper: createWrapper(),
      });

      await waitFor(() => {
        expect(result.current.isSuccess).toBe(true);
      });

      expect(result.current.data).toEqual(gameLabels);
      expect(api.library.getGameLabels).toHaveBeenCalledWith('game-123');
    });

    it('should not fetch when gameId is empty', async () => {
      const { result } = renderHook(() => useGameLabels(''), {
        wrapper: createWrapper(),
      });

      expect(result.current.fetchStatus).toBe('idle');
      expect(api.library.getGameLabels).not.toHaveBeenCalled();
    });
  });

  describe('useAddLabelToGame', () => {
    it('should add label to game', async () => {
      const label = mockLabels[0];
      vi.mocked(api.library.addLabelToGame).mockResolvedValue(label);

      const { result } = renderHook(() => useAddLabelToGame(), {
        wrapper: createWrapper(),
      });

      await result.current.mutateAsync({ gameId: 'game-123', label });

      expect(api.library.addLabelToGame).toHaveBeenCalledWith('game-123', 'label-1');
    });
  });

  describe('useRemoveLabelFromGame', () => {
    it('should remove label from game', async () => {
      vi.mocked(api.library.removeLabelFromGame).mockResolvedValue(undefined);

      const { result } = renderHook(() => useRemoveLabelFromGame(), {
        wrapper: createWrapper(),
      });

      await result.current.mutateAsync({ gameId: 'game-123', labelId: 'label-1' });

      expect(api.library.removeLabelFromGame).toHaveBeenCalledWith('game-123', 'label-1');
    });
  });

  describe('useCreateCustomLabel', () => {
    it('should create custom label', async () => {
      const newLabel: LabelDto = {
        id: 'label-new',
        name: 'New Label',
        color: '#ff0000',
        isPredefined: false,
        createdAt: '2024-01-17T10:00:00Z',
      };
      vi.mocked(api.library.createCustomLabel).mockResolvedValue(newLabel);

      const { result } = renderHook(() => useCreateCustomLabel(), {
        wrapper: createWrapper(),
      });

      const createdLabel = await result.current.mutateAsync({
        name: 'New Label',
        color: '#ff0000',
      });

      expect(createdLabel).toEqual(newLabel);
      expect(api.library.createCustomLabel).toHaveBeenCalledWith({
        name: 'New Label',
        color: '#ff0000',
      });
    });
  });

  describe('useDeleteCustomLabel', () => {
    it('should delete custom label', async () => {
      vi.mocked(api.library.deleteCustomLabel).mockResolvedValue(undefined);

      const { result } = renderHook(() => useDeleteCustomLabel(), {
        wrapper: createWrapper(),
      });

      await result.current.mutateAsync('label-3');

      expect(api.library.deleteCustomLabel).toHaveBeenCalledWith('label-3');
    });
  });
});
