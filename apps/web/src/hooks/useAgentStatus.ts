/**
 * useAgentStatus - Hook for checking agent chat readiness
 * Calls GET /agents/{id}/status to validate KB and RAG initialization
 */

import { useState, useEffect } from 'react';

import { api } from '@/lib/api';

export interface AgentStatus {
  agentId: string;
  name: string;
  isActive: boolean;
  isReady: boolean;
  hasConfiguration: boolean;
  hasDocuments: boolean;
  documentCount: number;
  ragStatus: string;
  blockingReason?: string | null;
}

export interface UseAgentStatusResult {
  status: AgentStatus | null;
  isLoading: boolean;
  error: string | null;
  refetch: () => Promise<void>;
}

export function useAgentStatus(agentId: string): UseAgentStatusResult {
  const [status, setStatus] = useState<AgentStatus | null>(null);
  const [isLoading, setIsLoading] = useState(true);
  const [error, setError] = useState<string | null>(null);

  const fetchStatus = async () => {
    setIsLoading(true);
    setError(null);
    try {
      const result = await api.agents.getStatus(agentId);
      setStatus(result);
    } catch (err) {
      const message = err instanceof Error ? err.message : 'Failed to load agent status';
      setError(message);
      setStatus(null);
    } finally {
      setIsLoading(false);
    }
  };

  useEffect(() => {
    void fetchStatus();
    // eslint-disable-next-line react-hooks/exhaustive-deps
  }, [agentId]);

  return {
    status,
    isLoading,
    error,
    refetch: fetchStatus,
  };
}
