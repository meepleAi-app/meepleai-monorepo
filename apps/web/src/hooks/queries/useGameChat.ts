/**
 * useGameChat — hook per il chat-in-game V2 (G1+G5).
 *
 * FSM minimal: 'idle' | 'submitting' | 'error'. I "responding-*" stati
 * della spec §4.1 sono renderizzati dall'orchestrator in base ai campi
 * della risposta (isLowQuality, outOfContext) — non sono stati hook.
 *
 * Backend: zero modifiche. Usa l'API existente `qaStream()` (SSE) da
 * apps/web/src/lib/api/clients/chatClient.ts e accumula solo l'evento
 * `Complete` (type=4) — UX atomica, no streaming token (Q6 brainstorm).
 * Il payload Complete contiene confidence + Citations; l'answer arriva
 * via eventi Token (type=7) accumulati nel buffer.
 *
 * isLowQuality e outOfContext sono DERIVATI frontend (backend non li espone).
 *
 * Spec: docs/superpowers/specs/2026-05-09-game-chat-tab-v1-g5-design.md §3.4
 */
'use client';

import { useCallback, useState } from 'react';

import { qaStream } from '@/lib/api/clients/chatClient';
import type { Citation } from '@/types';

import type { AgentKind } from '@/components/v2/game-chat/GameChatHeader';

export interface ChatMessage {
  readonly id: string;
  readonly role: 'user' | 'agent';
  readonly content: string;
  readonly citations?: ReadonlyArray<Citation>;
  readonly overallConfidence?: number;
  readonly isLowQuality?: boolean;
  readonly outOfContext?: boolean;
  readonly createdAt: string;
}

export interface UseGameChatResult {
  readonly messages: readonly ChatMessage[];
  readonly isLoading: boolean;
  readonly isError: boolean;
  readonly currentAgent: AgentKind;
  readonly ask: (question: string) => Promise<void>;
  readonly switchAgent: (next: AgentKind) => void;
}

// Module-private id counter for ephemeral chat message ids.
let messageIdCounter = 0;
const nextId = (prefix: string) => `${prefix}-${++messageIdCounter}-${Date.now()}`;

// Event types from chatClient.ts QA_EVENT_TYPES
const TOKEN_EVENT_TYPE = 7;
const COMPLETE_EVENT_TYPE = 4;
const ERROR_EVENT_TYPE = 5;

// Soglie derivate frontend (backend non espone isLowQuality/outOfContext)
const LOW_QUALITY_THRESHOLD = 0.70;
const OUT_OF_CONTEXT_THRESHOLD = 0.30;

interface StreamingCompletePayload {
  readonly estimatedReadingTimeMinutes?: number;
  readonly promptTokens?: number;
  readonly completionTokens?: number;
  readonly totalTokens?: number;
  readonly confidence?: number | null;
  readonly chatThreadId?: string;
  readonly Citations?: ReadonlyArray<Citation>;
  readonly citations?: ReadonlyArray<Citation>;
}

interface ErrorPayload {
  readonly message?: string;
}

export function useGameChat(gameId: string, initialAgent: AgentKind = 'tutor'): UseGameChatResult {
  const [messages, setMessages] = useState<readonly ChatMessage[]>([]);
  const [currentAgent, setCurrentAgent] = useState<AgentKind>(initialAgent);
  const [isLoading, setIsLoading] = useState(false);
  const [isError, setIsError] = useState(false);

  const ask = useCallback(async (question: string) => {
    const userMsg: ChatMessage = {
      id: nextId('u'),
      role: 'user',
      content: question,
      createdAt: new Date().toISOString(),
    };
    setMessages(prev => [...prev, userMsg]);
    setIsLoading(true);
    setIsError(false);

    let answerBuffer = '';

    try {
      const stream = qaStream({
        gameId,
        query: question,
      });

      for await (const event of stream) {
        if (event.type === TOKEN_EVENT_TYPE) {
          const tokenData = event.data;
          if (typeof tokenData === 'string') {
            answerBuffer += tokenData;
          } else if (typeof tokenData === 'object' && tokenData !== null && 'content' in tokenData) {
            answerBuffer += String((tokenData as { content?: unknown }).content ?? '');
          }
        } else if (event.type === COMPLETE_EVENT_TYPE) {
          const payload = event.data as StreamingCompletePayload;
          const confidence = payload.confidence ?? undefined;
          const citations = payload.Citations ?? payload.citations ?? [];
          const isLowQuality = confidence !== undefined && confidence < LOW_QUALITY_THRESHOLD;
          const outOfContext =
            citations.length === 0 &&
            (confidence === undefined || confidence < OUT_OF_CONTEXT_THRESHOLD);

          const agentMsg: ChatMessage = {
            id: nextId('a'),
            role: 'agent',
            content: answerBuffer,
            citations: citations.length > 0 ? citations : undefined,
            overallConfidence: confidence,
            isLowQuality,
            outOfContext,
            createdAt: new Date().toISOString(),
          };
          setMessages(prev => [...prev, agentMsg]);
        } else if (event.type === ERROR_EVENT_TYPE) {
          const errPayload = event.data as ErrorPayload;
          throw new Error(errPayload?.message ?? 'QA stream error');
        }
        // Ignora type=0 (StateUpdate), type=1 (Citations early), type=8 (Follow-up).
      }
    } catch (e) {
      setIsError(true);
      throw e;
    } finally {
      setIsLoading(false);
    }
  }, [gameId]);

  const switchAgent = useCallback((next: AgentKind) => {
    setCurrentAgent(next);
  }, []);

  return {
    messages,
    isLoading,
    isError,
    currentAgent,
    ask,
    switchAgent,
  };
}
