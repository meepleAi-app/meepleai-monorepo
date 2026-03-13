'use client';

import { createContext, useContext, useState, useMemo, useCallback, type ReactNode } from 'react';

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
}

const SandboxSessionContext = createContext<SandboxSessionContextValue | null>(null);

export function SandboxSessionProvider({ children }: { children: ReactNode }) {
  const [sessionId] = useState(() => crypto.randomUUID());
  const [agentConfig, setAgentConfig] = useState<AgentConfigDraft>({ ...DEFAULT_CONFIG });
  const [appliedConfig, setAppliedConfig] = useState<AgentConfigDraft>({ ...DEFAULT_CONFIG });
  const [isApplying, setIsApplying] = useState(false);

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
    setIsApplying(true);
    try {
      // Will call POST /admin/sandbox/apply-config when wired
      setAppliedConfig({ ...agentConfig });
    } finally {
      setIsApplying(false);
    }
  }, [agentConfig]);

  const resetConfig = useCallback(() => {
    setAgentConfig({ ...DEFAULT_CONFIG });
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
