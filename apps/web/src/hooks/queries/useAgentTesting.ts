/**
 * Agent Testing Hooks
 * React Query hooks for running auto-test suites and interactive chat against game agents.
 */

import { useMutation } from '@tanstack/react-query';
import { toast } from 'sonner';

import { getApiBase } from '@/lib/api';

// ─── Types ───────────────────────────────────────────────────────────────────

export interface TestCaseResult {
  index: number;
  question: string;
  answer: string | null;
  confidenceScore: number;
  latencyMs: number;
  chunksRetrieved: number;
  passed: boolean;
  failureReason: string | null;
}

export interface AgentQualityReport {
  totalTests: number;
  passed: number;
  failed: number;
  averageConfidence: number;
  averageLatencyMs: number;
  overallGrade: string;
  passRate: number;
}

export interface AgentAutoTestResult {
  gameId: string;
  gameTitle: string;
  testCases: TestCaseResult[];
  report: AgentQualityReport;
  executedAt: string;
}

export interface AgentChatMessage {
  role: 'user' | 'assistant';
  content: string;
  confidence?: number;
  latencyMs?: number;
  chunksRetrieved?: number;
  timestamp: Date;
}

interface AskAgentParams {
  gameId: string;
  question: string;
}

interface AgentChatResponse {
  answer: string | null;
  retrievedChunks: Array<{
    filePath: string;
    startLine: number;
    endLine: number;
    codePreview: string;
    relevanceScore: number;
    boundedContext: string;
    chunkIndex: number;
  }>;
  latencyMs: number;
  tokenUsage: {
    promptTokens: number;
    completionTokens: number;
    totalTokens: number;
    embeddingTokens: number;
  };
}

// ─── API Calls ───────────────────────────────────────────────────────────────

async function runAgentAutoTest(gameId: string): Promise<AgentAutoTestResult> {
  const res = await fetch(
    `${getApiBase()}/api/v1/admin/games/${gameId}/agent/auto-test`,
    {
      method: 'POST',
      headers: { 'Content-Type': 'application/json' },
      credentials: 'include',
    }
  );

  if (!res.ok) {
    const error = await res.json().catch(() => ({ detail: 'Auto-test failed' }));
    throw new Error(error.detail || error.message || 'Failed to run auto-test');
  }

  return res.json();
}

async function askAgentQuestion(params: AskAgentParams): Promise<AgentChatResponse> {
  const res = await fetch(`${getApiBase()}/api/v1/agents/chat/ask`, {
    method: 'POST',
    headers: { 'Content-Type': 'application/json' },
    credentials: 'include',
    body: JSON.stringify({
      question: params.question,
      strategy: 1, // SingleModel
      gameId: params.gameId,
    }),
  });

  if (!res.ok) {
    const error = await res.json().catch(() => ({ detail: 'Agent query failed' }));
    throw new Error(error.detail || error.message || 'Failed to query agent');
  }

  return res.json();
}

// ─── Hooks ───────────────────────────────────────────────────────────────────

/** Mutation to run the auto-test suite against a game's RAG agent */
export function useAgentAutoTest() {
  return useMutation<AgentAutoTestResult, Error, string>({
    mutationFn: runAgentAutoTest,
    onError: (error) => {
      toast.error(error.message || 'Auto-test failed');
    },
  });
}

/** Mutation to ask a single question to the game's RAG agent */
export function useAskAgentQuestion() {
  return useMutation<AgentChatResponse, Error, AskAgentParams>({
    mutationFn: askAgentQuestion,
    onError: (error) => {
      toast.error(error.message || 'Failed to get agent response');
    },
  });
}
