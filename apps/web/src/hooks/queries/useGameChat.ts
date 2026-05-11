/**
 * useGameChat — hook per il chat-in-game V2 (G1+G5) con fast-resume hydrate (G2).
 *
 * FSM minimal: 'idle' | 'submitting' | 'error'. I "responding-*" stati
 * della spec §4.1 sono renderizzati dall'orchestrator in base ai campi
 * della risposta (isLowQuality, outOfContext) — non sono stati hook.
 *
 * G2 fast resume:
 *   - On mount → fetch latest ChatThread per (user, gameId) via api.chat.getThreadsByGame
 *   - Hydrate messages text-only (no metadata G1+G5: backend ChatThreadMessage non li persiste)
 *   - Persist chatThreadId per riusarlo nelle qaStream successive (continua thread esistente)
 *   - hasHistoricalMessages flag per UX banner
 *   - Silent fail: se il fetch fallisce, isError resta false (utente può comunque chiedere)
 *
 * Backend: zero modifiche. Usa qaStream() (SSE) + api.chat.getThreadsByGame() (REST).
 *
 * Spec: docs/superpowers/specs/2026-05-09-game-chat-tab-v1-g5-design.md §3.4
 *       docs/superpowers/specs/2026-05-10-fast-resume-audit-g2-design.md §3.1
 */
'use client';

import { useCallback, useEffect, useState } from 'react';

import type { AgentKind } from '@/components/features/game-chat/GameChatHeader';
import { api } from '@/lib/api';
import { qaStream } from '@/lib/api/clients/chatClient';
import type { ChatThreadMessageDto } from '@/lib/api/schemas/chat.schemas';
import type { Citation } from '@/types';

export interface ChatMessage {
  readonly id: string;
  readonly role: 'user' | 'agent';
  readonly content: string;
  readonly citations?: ReadonlyArray<Citation>;
  readonly overallConfidence?: number;
  readonly isLowQuality?: boolean;
  readonly outOfContext?: boolean;
  readonly createdAt: string;
  readonly isHistorical?: boolean;
}

export interface UseGameChatResult {
  readonly messages: readonly ChatMessage[];
  readonly isLoading: boolean;
  readonly isHydrating: boolean;
  readonly isError: boolean;
  readonly currentAgent: AgentKind;
  readonly chatThreadId: string | null;
  readonly hasHistoricalMessages: boolean;
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
const LOW_QUALITY_THRESHOLD = 0.7;
const OUT_OF_CONTEXT_THRESHOLD = 0.3;

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

function toChatMessage(dto: ChatThreadMessageDto, idx: number): ChatMessage {
  return {
    id: dto.backendMessageId ?? `historical-${idx}-${dto.timestamp}`,
    role: dto.role === 'user' ? 'user' : 'agent',
    content: dto.content,
    createdAt: dto.timestamp,
    isHistorical: true,
  };
}

export function useGameChat(gameId: string, initialAgent: AgentKind = 'tutor'): UseGameChatResult {
  const [messages, setMessages] = useState<readonly ChatMessage[]>([]);
  const [currentAgent, setCurrentAgent] = useState<AgentKind>(initialAgent);
  const [isLoading, setIsLoading] = useState(false);
  const [isError, setIsError] = useState(false);
  // G2: default false (set true in effect) per non rompere test esistenti
  const [isHydrating, setIsHydrating] = useState(false);
  const [chatThreadId, setChatThreadId] = useState<string | null>(null);
  const [hasHistoricalMessages, setHasHistoricalMessages] = useState(false);

  // G2: Hydrate latest thread on mount
  useEffect(() => {
    let cancelled = false;
    setIsHydrating(true);

    api.chat
      .getThreadsByGame(gameId)
      .then(threads => {
        if (cancelled) return;
        const latest = [...threads]
          .filter(t => t.lastMessageAt !== null)
          .sort((a, b) => {
            // Robusto su timezone offset: confronto numerico via getTime()
            if (a.lastMessageAt === null) return 1;
            if (b.lastMessageAt === null) return -1;
            return new Date(b.lastMessageAt).getTime() - new Date(a.lastMessageAt).getTime();
          })[0];

        if (latest && latest.messages.length > 0) {
          setChatThreadId(latest.id);
          setMessages(latest.messages.map(toChatMessage));
          setHasHistoricalMessages(true);
        }
      })
      .catch(() => {
        // Silent fail — utente vede chat vuota, può comunque chiedere
        // (no error state — backend ChatThread fetch non è bloccante per ask)
      })
      .finally(() => {
        if (!cancelled) setIsHydrating(false);
      });

    return () => {
      cancelled = true;
    };
  }, [gameId]);

  const ask = useCallback(
    async (question: string) => {
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
          chatId: chatThreadId ?? undefined,
        });

        for await (const event of stream) {
          if (event.type === TOKEN_EVENT_TYPE) {
            const tokenData = event.data;
            if (typeof tokenData === 'string') {
              answerBuffer += tokenData;
            } else if (
              typeof tokenData === 'object' &&
              tokenData !== null &&
              'content' in tokenData
            ) {
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

            // G2: salva chatThreadId se backend lo ritorna e non l'abbiamo ancora
            if (payload.chatThreadId && !chatThreadId) {
              setChatThreadId(payload.chatThreadId);
            }
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
    },
    [gameId, chatThreadId]
  );

  const switchAgent = useCallback((next: AgentKind) => {
    setCurrentAgent(next);
  }, []);

  return {
    messages,
    isLoading,
    isHydrating,
    isError,
    currentAgent,
    chatThreadId,
    hasHistoricalMessages,
    ask,
    switchAgent,
  };
}
