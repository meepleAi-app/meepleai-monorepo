/**
 * useAgentConfigModal - Hook for Agent Configuration Modal (Issue #3186 - AGT-012)
 *
 * Manages state, API calls, and business logic for the Agent Config Modal:
 * - Typology selection
 * - Tier-based model filtering
 * - Cost estimation
 * - Quota tracking with warnings
 * - localStorage caching
 */

import { useState, useEffect, useMemo } from 'react';

import { useQuery, useMutation, useQueryClient } from '@tanstack/react-query';
import { toast } from 'sonner';

import { useAvailableModels } from '@/hooks/queries/useModels';
import { useSessionQuotaWithStatus } from '@/hooks/queries/useSessionQuota';
import { useAuth } from '@/hooks/useAuth';
import { api } from '@/lib/api';
import type { Typology } from '@/lib/api/schemas';

// ========== Model Configuration ==========

export type UserTier = 'Free' | 'Premium';

export interface ModelConfig {
  name: string;
  cost: number; // Cost per query in dollars
  recommended?: boolean;
}

// ========== localStorage Keys ==========

function getStorageKey(gameId: string): string {
  return `meepleai:agent-config:${gameId}`;
}

interface CachedConfig {
  typologyId: string;
  modelName: string;
  timestamp: number;
}

function loadCachedConfig(gameId: string): CachedConfig | null {
  try {
    const stored = localStorage.getItem(getStorageKey(gameId));
    if (!stored) return null;
    return JSON.parse(stored) as CachedConfig;
  } catch {
    return null;
  }
}

function saveCachedConfig(gameId: string, config: Omit<CachedConfig, 'timestamp'>): void {
  try {
    const data: CachedConfig = {
      ...config,
      timestamp: Date.now(),
    };
    localStorage.setItem(getStorageKey(gameId), JSON.stringify(data));
  } catch {
    // Silent fail for localStorage errors (quota exceeded, etc.)
  }
}

// ========== Agent Config Request ==========

interface AgentConfigRequest {
  typologyId: string;
  modelName: string;
  costEstimate: number;
}

// ========== Hook Parameters ==========

interface UseAgentConfigModalParams {
  gameId: string;
  enabled?: boolean;
}

// ========== Hook Return Type ==========

export interface UseAgentConfigModalResult {
  // State
  selectedTypologyId: string | null;
  setSelectedTypologyId: (id: string | null) => void;
  selectedModelName: string | null;
  setSelectedModelName: (name: string | null) => void;

  // Data
  typologies: Typology[];
  typologiesLoading: boolean;
  typologiesError: Error | null;

  // Models (tier-filtered)
  availableModels: ModelConfig[];
  userTier: UserTier;

  // Cost
  estimatedCost: number | null;

  // Quota
  quota: ReturnType<typeof useSessionQuotaWithStatus>['data'];
  quotaLoading: boolean;
  showWarning: boolean; // true if >90% quota used

  // Actions
  saveConfig: () => Promise<void>;
  saving: boolean;
  saveError: Error | null;

  // Validation
  isValid: boolean;
}

// ========== Hook Implementation ==========

export function useAgentConfigModal({
  gameId,
  enabled = true,
}: UseAgentConfigModalParams): UseAgentConfigModalResult {
  const { user } = useAuth();
  const queryClient = useQueryClient();

  // User tier (simplified: Premium if authenticated, Free otherwise)
  // Note: Full tier detection pending user profile enhancement (Issue #3186)
  const userTier: UserTier = user ? 'Premium' : 'Free';

  // Form state
  const [selectedTypologyId, setSelectedTypologyId] = useState<string | null>(null);
  const [selectedModelName, setSelectedModelName] = useState<string | null>(null);

  // ========== Query: Typologies ==========

  const {
    data: typologies = [],
    isLoading: typologiesLoading,
    error: typologiesError,
  } = useQuery({
    queryKey: ['agent-typologies', 'approved'],
    queryFn: async () => api.agents.getTypologies('Approved'),
    enabled: enabled,
    staleTime: 5 * 60 * 1000, // 5 minutes (typologies rarely change)
    gcTime: 10 * 60 * 1000,
  });

  // ========== Query: Quota ==========

  const {
    data: quota,
    isLoading: quotaLoading,
  } = useSessionQuotaWithStatus(enabled);

  const showWarning = useMemo(() => {
    if (!quota || quota.isUnlimited) return false;
    return quota.percentageUsed >= 90;
  }, [quota]);

  // ========== Models (tier-filtered, from API) ==========

  const { data: apiModels = [] } = useAvailableModels(userTier.toLowerCase());

  const availableModels: ModelConfig[] = useMemo(() => {
    if (apiModels.length === 0) {
      // Fallback: show a default free model while API loads
      return [{ name: 'Llama 3.3 70B Free', cost: 0, recommended: true }];
    }
    return apiModels.map((m, i) => ({
      name: m.name,
      cost: (m.costPer1kInputTokens * 2 + m.costPer1kOutputTokens * 1),
      recommended: i === 0,
    }));
  }, [apiModels]);

  // ========== Cost Estimation ==========

  const estimatedCost = useMemo(() => {
    if (!selectedModelName) return null;
    const model = availableModels.find((m) => m.name === selectedModelName);
    return model?.cost ?? null;
  }, [selectedModelName, availableModels]);

  // ========== localStorage Cache: Load on mount ==========

  useEffect(() => {
    const cached = loadCachedConfig(gameId);
    if (cached) {
      setSelectedTypologyId(cached.typologyId);
      setSelectedModelName(cached.modelName);
    } else if (!selectedModelName) {
      // Pre-select recommended model only if user hasn't selected one yet
      const recommended = availableModels.find((m) => m.recommended);
      const defaultModel = recommended ?? availableModels[0];
      if (defaultModel) {
        setSelectedModelName(defaultModel.name);
      }
    }
  // eslint-disable-next-line react-hooks/exhaustive-deps
  }, [gameId]);

  // ========== Mutation: Save Config ==========

  const {
    mutateAsync: saveConfigMutation,
    isPending: saving,
    error: saveError,
  } = useMutation({
    mutationFn: async (request: AgentConfigRequest) => {
      // Issue #3212: Backend POST endpoint now implemented
      const response = await api.library.saveAgentConfig(gameId, request);
      return { ...request, configId: response.configId };
    },
    onSuccess: (data) => {
      // Save to localStorage
      saveCachedConfig(gameId, {
        typologyId: data.typologyId,
        modelName: data.modelName,
      });

      // Invalidate related queries
      queryClient.invalidateQueries({ queryKey: ['agent-config', gameId] });

      toast.success('Configurazione salvata', {
        description: 'La configurazione dell\'agente è stata salvata correttamente.',
      });
    },
    onError: (error: Error) => {
      toast.error('Errore nel salvataggio', {
        description: error.message || 'Si è verificato un errore durante il salvataggio.',
      });
    },
  });

  // ========== Action: Save Config ==========

  const saveConfig = async (): Promise<void> => {
    if (!selectedTypologyId || !selectedModelName || estimatedCost === null) {
      toast.error('Campi obbligatori mancanti', {
        description: 'Seleziona tipologia e modello prima di salvare.',
      });
      return;
    }

    await saveConfigMutation({
      typologyId: selectedTypologyId,
      modelName: selectedModelName,
      costEstimate: estimatedCost,
    });
  };

  // ========== Validation ==========

  const isValid =
    selectedTypologyId !== null &&
    selectedModelName !== null &&
    estimatedCost !== null;

  return {
    // State
    selectedTypologyId,
    setSelectedTypologyId,
    selectedModelName,
    setSelectedModelName,

    // Data
    typologies,
    typologiesLoading,
    typologiesError: typologiesError as Error | null,

    // Models
    availableModels,
    userTier,

    // Cost
    estimatedCost,

    // Quota
    quota,
    quotaLoading,
    showWarning,

    // Actions
    saveConfig,
    saving,
    saveError: saveError as Error | null,

    // Validation
    isValid,
  };
}
