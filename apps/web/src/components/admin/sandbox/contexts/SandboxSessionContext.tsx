'use client';

import { createContext, useContext, useState, useMemo, useCallback, type ReactNode } from 'react';

import { api } from '@/lib/api';

import { useSource } from './SourceContext';

export interface AgentConfigDraft {
  // RAG Strategy
  strategy: string;
  denseWeight: number;
  sparseWeight: number;
  topK: number;
  reranking: boolean;
  rerankerModel: string;
  minConfidence: number;
  // LLM Settings
  model: string;
  temperature: number;
  maxTokens: number;
  systemPromptOverride: string | null;
  // Chunking
  chunkStrategy: string;
  chunkSize: number;
  overlap: number;
  respectPageBoundaries: boolean;
}

export const DEFAULT_CONFIG: AgentConfigDraft = {
  strategy: 'hybrid-v2',
  denseWeight: 0.7,
  sparseWeight: 0.3,
  topK: 5,
  reranking: true,
  rerankerModel: 'cross-encoder/ms-marco',
  minConfidence: 0.6,
  model: '',
  temperature: 0.3,
  maxTokens: 2048,
  systemPromptOverride: null,
  chunkStrategy: 'semantic',
  chunkSize: 512,
  overlap: 50,
  respectPageBoundaries: true,
};

interface SandboxSessionContextValue {
  sessionId: string;
  agentConfig: AgentConfigDraft;
  appliedConfig: AgentConfigDraft;
  updateConfig: (partial: Partial<AgentConfigDraft>) => void;
  applyConfig: () => Promise<void>;
  resetConfig: () => void;
  isDirty: boolean;
  pendingChanges: number;
  isApplying: boolean;
  applyError: string | null;
}

const SandboxSessionContext = createContext<SandboxSessionContextValue | null>(null);

export function SandboxSessionProvider({ children }: { children: ReactNode }) {
  const [sessionId] = useState(() => crypto.randomUUID());
  const [agentConfig, setAgentConfig] = useState<AgentConfigDraft>({ ...DEFAULT_CONFIG });
  const [appliedConfig, setAppliedConfig] = useState<AgentConfigDraft>({ ...DEFAULT_CONFIG });
  const [isApplying, setIsApplying] = useState(false);
  const [applyError, setApplyError] = useState<string | null>(null);
  const { selectedGame } = useSource();

  const updateConfig = useCallback((partial: Partial<AgentConfigDraft>) => {
    setAgentConfig(prev => {
      const updated = { ...prev, ...partial };
      if ('denseWeight' in partial) {
        updated.sparseWeight = Math.round((1 - updated.denseWeight) * 10) / 10;
      }
      return updated;
    });
  }, []);

  const applyConfig = useCallback(async () => {
    if (!selectedGame) return;

    setIsApplying(true);
    setApplyError(null);
    try {
      await api.sandbox.applyConfig({
        gameId: selectedGame.id,
        strategy: agentConfig.strategy || undefined,
        denseWeight: agentConfig.denseWeight,
        topK: agentConfig.topK,
        rerankingEnabled: agentConfig.reranking,
        temperature: agentConfig.temperature,
        maxTokens: agentConfig.maxTokens,
        model: agentConfig.model || undefined,
        systemPromptOverride: agentConfig.systemPromptOverride || undefined,
        chunkingStrategy: agentConfig.chunkStrategy || undefined,
        chunkSize: agentConfig.chunkSize,
        chunkOverlap: agentConfig.overlap,
      });
      setAppliedConfig({ ...agentConfig });
    } catch (err) {
      setApplyError(err instanceof Error ? err.message : 'Failed to apply config');
    } finally {
      setIsApplying(false);
    }
  }, [agentConfig, selectedGame]);

  const resetConfig = useCallback(() => {
    setAgentConfig({ ...DEFAULT_CONFIG });
    setApplyError(null);
  }, []);

  const pendingChanges = useMemo(() => {
    const keys = Object.keys(agentConfig) as (keyof AgentConfigDraft)[];
    return keys.filter(key => agentConfig[key] !== appliedConfig[key]).length;
  }, [agentConfig, appliedConfig]);

  const isDirty = pendingChanges > 0;

  return (
    <SandboxSessionContext.Provider
      value={{
        sessionId,
        agentConfig,
        appliedConfig,
        updateConfig,
        applyConfig,
        resetConfig,
        isDirty,
        pendingChanges,
        isApplying,
        applyError,
      }}
    >
      {children}
    </SandboxSessionContext.Provider>
  );
}

export function useSandboxSession() {
  const ctx = useContext(SandboxSessionContext);
  if (!ctx) throw new Error('useSandboxSession must be used within SandboxSessionProvider');
  return ctx;
}
