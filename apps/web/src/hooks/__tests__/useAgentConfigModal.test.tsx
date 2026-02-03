/**
 * Tests for useAgentConfigModal hook
 * Issue #3190 (AGT-016): Frontend Agent Components Tests
 *
 * Coverage:
 * - Typology loading and selection
 * - Model filtering by tier (Free vs Premium)
 * - Cost estimation calculation
 * - Quota tracking and warnings
 * - Save config mutation
 * - localStorage caching
 * - Validation logic
 */

import { describe, it, expect, vi, beforeEach, afterEach } from 'vitest';
import { renderHook, waitFor } from '@testing-library/react';
import { QueryClient, QueryClientProvider } from '@tanstack/react-query';
import { act } from '@testing-library/react';
import { toast } from 'sonner';

import { useAgentConfigModal, type UserTier } from '../useAgentConfigModal';
import { api } from '@/lib/api';
import { useSessionQuotaWithStatus } from '@/hooks/queries/useSessionQuota';

// Mock dependencies
vi.mock('sonner', () => ({
  toast: {
    success: vi.fn(),
    error: vi.fn(),
  },
}));

vi.mock('@/hooks/useAuth', () => ({
  useAuth: () => ({ user: { id: 'user-1', email: 'test@example.com' } }),
}));

vi.mock('@/lib/api', () => ({
  api: {
    agents: {
      getTypologies: vi.fn(),
    },
    library: {
      saveAgentConfig: vi.fn(),
    },
  },
}));

vi.mock('@/hooks/queries/useSessionQuota', () => ({
  useSessionQuotaWithStatus: vi.fn(),
}));

// Mock localStorage
const localStorageMock = (() => {
  let store: Record<string, string> = {};

  return {
    getItem: (key: string) => store[key] || null,
    setItem: (key: string, value: string) => {
      store[key] = value;
    },
    clear: () => {
      store = {};
    },
    removeItem: (key: string) => {
      delete store[key];
    },
  };
})();

Object.defineProperty(window, 'localStorage', {
  value: localStorageMock,
});

// Mock data
const mockTypologies = [
  {
    id: 'typo-1',
    name: 'Tutor',
    description: 'Guida passo-passo',
    status: 'Approved' as const,
  },
  {
    id: 'typo-2',
    name: 'Strategia',
    description: 'Consigli strategici',
    status: 'Approved' as const,
  },
];

const mockQuota = {
  currentSessions: 5,
  maxSessions: 10,
  remainingSlots: 5,
  percentageUsed: 50,
  canCreateNew: true,
  isUnlimited: false,
};

describe('useAgentConfigModal', () => {
  let queryClient: QueryClient;

  const createWrapper = () => {
    queryClient = new QueryClient({
      defaultOptions: {
        queries: { retry: false },
        mutations: { retry: false },
      },
    });

    return ({ children }: { children: React.ReactNode }) => (
      <QueryClientProvider client={queryClient}>{children}</QueryClientProvider>
    );
  };

  beforeEach(() => {
    vi.clearAllMocks();
    localStorageMock.clear();

    // Setup default mocks
    vi.mocked(api.agents.getTypologies).mockResolvedValue(mockTypologies);
    vi.mocked(api.library.saveAgentConfig).mockResolvedValue({ configId: 'config-123' });

    vi.mocked(useSessionQuotaWithStatus).mockReturnValue({
      data: mockQuota,
      isLoading: false,
    });
  });

  afterEach(() => {
    localStorageMock.clear();
  });

  // =========================================================================
  // Initial State Tests
  // =========================================================================

  describe('Initial State', () => {
    it('should initialize with null selections', () => {
      const { result } = renderHook(() => useAgentConfigModal({ gameId: 'game-123' }), {
        wrapper: createWrapper(),
      });

      expect(result.current.selectedTypologyId).toBeNull();
    });

    it('should load typologies on mount', async () => {
      const { result } = renderHook(() => useAgentConfigModal({ gameId: 'game-123' }), {
        wrapper: createWrapper(),
      });

      await waitFor(() => {
        expect(result.current.typologies).toEqual(mockTypologies);
        expect(result.current.typologiesLoading).toBe(false);
      });
    });

    it('should load quota data on mount', async () => {
      const { result } = renderHook(() => useAgentConfigModal({ gameId: 'game-123' }), {
        wrapper: createWrapper(),
      });

      await waitFor(() => {
        expect(result.current.quota).toEqual(mockQuota);
        expect(result.current.quotaLoading).toBe(false);
      });
    });

    it('should not load data when disabled', () => {
      const { result } = renderHook(
        () => useAgentConfigModal({ gameId: 'game-123', enabled: false }),
        { wrapper: createWrapper() }
      );

      expect(result.current.typologies).toEqual([]);
    });
  });

  // =========================================================================
  // User Tier Tests
  // =========================================================================

  describe('User Tier', () => {
    it('should return Premium tier for authenticated users', () => {
      const { result } = renderHook(() => useAgentConfigModal({ gameId: 'game-123' }), {
        wrapper: createWrapper(),
      });

      expect(result.current.userTier).toBe('Premium');
    });

    it('should filter models by Premium tier', async () => {
      const { result } = renderHook(() => useAgentConfigModal({ gameId: 'game-123' }), {
        wrapper: createWrapper(),
      });

      await waitFor(() => {
        expect(result.current.availableModels).toEqual([
          { name: 'Claude-3.5-Haiku', cost: 0.003 },
          { name: 'GPT-4o', cost: 0.005, recommended: true },
        ]);
      });
    });
  });

  // =========================================================================
  // Model Selection Tests
  // =========================================================================

  describe('Model Selection', () => {
    it('should pre-select recommended model on first load', async () => {
      const { result } = renderHook(() => useAgentConfigModal({ gameId: 'game-123' }), {
        wrapper: createWrapper(),
      });

      await waitFor(() => {
        expect(result.current.selectedModelName).toBe('GPT-4o');
      });
    });

    it('should update selectedModelName', async () => {
      const { result } = renderHook(() => useAgentConfigModal({ gameId: 'game-123' }), {
        wrapper: createWrapper(),
      });

      await waitFor(() => {
        expect(result.current.selectedModelName).toBe('GPT-4o');
      });

      act(() => {
        result.current.setSelectedModelName('Claude-3.5-Haiku');
      });

      expect(result.current.selectedModelName).toBe('Claude-3.5-Haiku');
    });

    it('should update selectedTypologyId', () => {
      const { result } = renderHook(() => useAgentConfigModal({ gameId: 'game-123' }), {
        wrapper: createWrapper(),
      });

      act(() => {
        result.current.setSelectedTypologyId('typo-1');
      });

      expect(result.current.selectedTypologyId).toBe('typo-1');
    });
  });

  // =========================================================================
  // Cost Estimation Tests
  // =========================================================================

  describe('Cost Estimation', () => {
    it('should return null cost when no model selected', () => {
      const { result } = renderHook(() => useAgentConfigModal({ gameId: 'game-123' }), {
        wrapper: createWrapper(),
      });

      act(() => {
        result.current.setSelectedModelName(null);
      });

      expect(result.current.estimatedCost).toBeNull();
    });

    it('should calculate cost for selected model', async () => {
      const { result } = renderHook(() => useAgentConfigModal({ gameId: 'game-123' }), {
        wrapper: createWrapper(),
      });

      await waitFor(() => {
        expect(result.current.selectedModelName).toBe('GPT-4o');
      });

      expect(result.current.estimatedCost).toBe(0.005);
    });

    it('should update cost when model changes', async () => {
      const { result } = renderHook(() => useAgentConfigModal({ gameId: 'game-123' }), {
        wrapper: createWrapper(),
      });

      await waitFor(() => {
        expect(result.current.estimatedCost).toBe(0.005);
      });

      act(() => {
        result.current.setSelectedModelName('Claude-3.5-Haiku');
      });

      expect(result.current.estimatedCost).toBe(0.003);
    });
  });

  // =========================================================================
  // Quota Warning Tests
  // =========================================================================

  describe('Quota Warnings', () => {
    it('should not show warning when quota <90%', () => {
      const { result } = renderHook(() => useAgentConfigModal({ gameId: 'game-123' }), {
        wrapper: createWrapper(),
      });

      expect(result.current.showWarning).toBe(false);
    });

    it('should show warning when quota ≥90%', async () => {
      vi.mocked(useSessionQuotaWithStatus).mockReturnValue({
        data: { ...mockQuota, percentageUsed: 95, currentSessions: 95, maxSessions: 100 },
        isLoading: false,
      });

      const { result } = renderHook(() => useAgentConfigModal({ gameId: 'game-123' }), {
        wrapper: createWrapper(),
      });

      await waitFor(() => {
        expect(result.current.showWarning).toBe(true);
      });
    });

    it('should not show warning for unlimited quota', async () => {
      vi.mocked(useSessionQuotaWithStatus).mockReturnValue({
        data: { ...mockQuota, isUnlimited: true },
        isLoading: false,
      });

      const { result } = renderHook(() => useAgentConfigModal({ gameId: 'game-123' }), {
        wrapper: createWrapper(),
      });

      await waitFor(() => {
        expect(result.current.showWarning).toBe(false);
      });
    });
  });

  // =========================================================================
  // localStorage Caching Tests
  // =========================================================================

  describe('localStorage Caching', () => {
    it('should load cached config on mount', async () => {
      const cachedConfig = {
        typologyId: 'typo-2',
        modelName: 'Claude-3.5-Haiku',
        timestamp: Date.now(),
      };

      localStorageMock.setItem('meepleai:agent-config:game-123', JSON.stringify(cachedConfig));

      const { result } = renderHook(() => useAgentConfigModal({ gameId: 'game-123' }), {
        wrapper: createWrapper(),
      });

      await waitFor(() => {
        expect(result.current.selectedTypologyId).toBe('typo-2');
        expect(result.current.selectedModelName).toBe('Claude-3.5-Haiku');
      });
    });

    it('should save config to localStorage on successful save', async () => {
      const { result } = renderHook(() => useAgentConfigModal({ gameId: 'game-123' }), {
        wrapper: createWrapper(),
      });

      await waitFor(() => {
        expect(result.current.selectedModelName).toBe('GPT-4o');
      });

      act(() => {
        result.current.setSelectedTypologyId('typo-1');
      });

      await act(async () => {
        await result.current.saveConfig();
      });

      await waitFor(() => {
        const stored = localStorageMock.getItem('meepleai:agent-config:game-123');
        expect(stored).toBeTruthy();

        if (stored) {
          const parsed = JSON.parse(stored);
          expect(parsed.typologyId).toBe('typo-1');
          expect(parsed.modelName).toBe('GPT-4o');
          expect(parsed.timestamp).toBeDefined();
        }
      });
    });

    it('should handle invalid localStorage data gracefully', () => {
      localStorageMock.setItem('meepleai:agent-config:game-123', 'invalid-json');

      const { result } = renderHook(() => useAgentConfigModal({ gameId: 'game-123' }), {
        wrapper: createWrapper(),
      });

      // Should not throw and should use defaults
      expect(result.current.selectedTypologyId).toBeNull();
    });

    it('should use game-specific storage keys', async () => {
      const { result: result1 } = renderHook(() => useAgentConfigModal({ gameId: 'game-1' }), {
        wrapper: createWrapper(),
      });

      await waitFor(() => {
        expect(result1.current.selectedModelName).toBe('GPT-4o');
      });

      act(() => {
        result1.current.setSelectedTypologyId('typo-1');
      });

      await act(async () => {
        await result1.current.saveConfig();
      });

      await waitFor(() => {
        const stored1 = localStorageMock.getItem('meepleai:agent-config:game-1');
        const stored2 = localStorageMock.getItem('meepleai:agent-config:game-2');

        expect(stored1).toBeTruthy();
        expect(stored2).toBeNull();
      });
    });
  });

  // =========================================================================
  // Save Config Tests
  // =========================================================================

  describe('Save Config', () => {
    it('should call API with correct parameters', async () => {
      const { result } = renderHook(() => useAgentConfigModal({ gameId: 'game-123' }), {
        wrapper: createWrapper(),
      });

      await waitFor(() => {
        expect(result.current.selectedModelName).toBe('GPT-4o');
      });

      act(() => {
        result.current.setSelectedTypologyId('typo-1');
      });

      await act(async () => {
        await result.current.saveConfig();
      });

      await waitFor(() => {
        expect(api.library.saveAgentConfig).toHaveBeenCalledWith('game-123', {
          typologyId: 'typo-1',
          modelName: 'GPT-4o',
          costEstimate: 0.005,
        });
      });
    });

    it('should show success toast on successful save', async () => {
      const { result } = renderHook(() => useAgentConfigModal({ gameId: 'game-123' }), {
        wrapper: createWrapper(),
      });

      await waitFor(() => {
        expect(result.current.selectedModelName).toBe('GPT-4o');
      });

      act(() => {
        result.current.setSelectedTypologyId('typo-1');
      });

      await act(async () => {
        await result.current.saveConfig();
      });

      await waitFor(() => {
        expect(toast.success).toHaveBeenCalledWith(
          'Configurazione salvata',
          expect.objectContaining({
            description: expect.stringContaining('salvata correttamente'),
          })
        );
      });
    });

    it('should show error toast on save failure', async () => {
      const { result } = renderHook(() => useAgentConfigModal({ gameId: 'game-123' }), {
        wrapper: createWrapper(),
      });

      await waitFor(() => {
        expect(result.current.selectedModelName).toBe('GPT-4o');
      });

      act(() => {
        result.current.setSelectedTypologyId('typo-1');
      });

      // Set up rejection AFTER hook is rendered but BEFORE calling saveConfig
      vi.mocked(api.library.saveAgentConfig).mockRejectedValueOnce(
        new Error('Network error')
      );

      // saveConfig doesn't have try-catch, so we need to catch the error
      await act(async () => {
        try {
          await result.current.saveConfig();
        } catch {
          // Error is expected and handled by onError callback
        }
      });

      await waitFor(() => {
        expect(toast.error).toHaveBeenCalledWith(
          'Errore nel salvataggio',
          expect.objectContaining({
            description: expect.stringContaining('Network error'),
          })
        );
      });
    });

    it('should show error toast when required fields missing', async () => {
      const { result } = renderHook(() => useAgentConfigModal({ gameId: 'game-123' }), {
        wrapper: createWrapper(),
      });

      // Don't select typology
      await act(async () => {
        await result.current.saveConfig();
      });

      expect(toast.error).toHaveBeenCalledWith(
        'Campi obbligatori mancanti',
        expect.objectContaining({
          description: expect.stringContaining('Seleziona tipologia e modello'),
        })
      );
    });

    it('should invalidate related queries on success', async () => {
      // Create wrapper first to get access to the queryClient instance
      const wrapper = createWrapper();
      const invalidateSpy = vi.spyOn(queryClient, 'invalidateQueries');

      const { result } = renderHook(() => useAgentConfigModal({ gameId: 'game-123' }), {
        wrapper,
      });

      await waitFor(() => {
        expect(result.current.selectedModelName).toBe('GPT-4o');
      });

      act(() => {
        result.current.setSelectedTypologyId('typo-1');
      });

      await act(async () => {
        await result.current.saveConfig();
      });

      await waitFor(() => {
        expect(invalidateSpy).toHaveBeenCalledWith({
          queryKey: ['agent-config', 'game-123'],
        });
      });
    });

    it('should set saving state during save', async () => {
      // Slow API response
      vi.mocked(api.library.saveAgentConfig).mockImplementationOnce(
        () =>
          new Promise((resolve) => {
            setTimeout(() => resolve({ configId: 'config-123' }), 100);
          })
      );

      const { result } = renderHook(() => useAgentConfigModal({ gameId: 'game-123' }), {
        wrapper: createWrapper(),
      });

      await waitFor(() => {
        expect(result.current.selectedModelName).toBe('GPT-4o');
      });

      act(() => {
        result.current.setSelectedTypologyId('typo-1');
      });

      expect(result.current.saving).toBe(false);

      let savePromise: Promise<void>;
      act(() => {
        savePromise = result.current.saveConfig();
      });

      // Wait for saving state to become true
      await waitFor(() => {
        expect(result.current.saving).toBe(true);
      });

      // Wait for save to complete
      await act(async () => {
        await savePromise;
      });

      // Wait for React Query to update isPending back to false
      await waitFor(() => {
        expect(result.current.saving).toBe(false);
      });
    });
  });

  // =========================================================================
  // Validation Tests
  // =========================================================================

  describe('Validation', () => {
    it('should be invalid when typology not selected', async () => {
      const { result } = renderHook(() => useAgentConfigModal({ gameId: 'game-123' }), {
        wrapper: createWrapper(),
      });

      await waitFor(() => {
        expect(result.current.selectedModelName).toBe('GPT-4o');
      });

      expect(result.current.isValid).toBe(false);
    });

    it('should be invalid when model not selected', () => {
      const { result } = renderHook(() => useAgentConfigModal({ gameId: 'game-123' }), {
        wrapper: createWrapper(),
      });

      act(() => {
        result.current.setSelectedTypologyId('typo-1');
        result.current.setSelectedModelName(null);
      });

      expect(result.current.isValid).toBe(false);
    });

    it('should be valid when all fields selected', async () => {
      const { result } = renderHook(() => useAgentConfigModal({ gameId: 'game-123' }), {
        wrapper: createWrapper(),
      });

      await waitFor(() => {
        expect(result.current.selectedModelName).toBe('GPT-4o');
      });

      act(() => {
        result.current.setSelectedTypologyId('typo-1');
      });

      await waitFor(() => {
        expect(result.current.isValid).toBe(true);
      });
    });
  });
});
