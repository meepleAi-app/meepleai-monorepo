/**
 * useSessionAssistant — the state of the in-game assistant panel (#4238 A4, spec 2026-10-11 §5–§6).
 *
 * The truth is the panel read (`GET /live-sessions/{id}/assistant`, #4372): availability,
 * conversation and proposals. A turn streams on top of it, and when it ends the read is fetched
 * again, so what stays on screen is always what the server saved.
 *
 * Two rules come from the reviews of A3 (#4297, #4301):
 *   - the first `status` event of a turn expires the proposals still `Pending` from earlier turns,
 *     however the turn ends: from there they are shown as expired;
 *   - proposals are confirmed one at a time. A score card and a turn card confirmed in the same
 *     instant collide on the concurrency of the session and one becomes `Failed`, so while a
 *     confirmation is in flight no other one starts.
 */

import { useCallback, useEffect, useRef, useState } from 'react';

import { useQuery, useQueryClient } from '@tanstack/react-query';

import { api } from '@/lib/api';
import { ApiError } from '@/lib/api/core/errors';
import {
  ASSISTANT_PROPOSAL_STATUS,
  type AssistantProposalDto,
  type AssistantTurnEvent,
  type SessionAssistantPanelDto,
} from '@/lib/api/schemas/live-sessions.schemas';
import { askSessionAssistant } from '@/lib/session-live/assistant-stream';

import { liveSessionKeys } from './useLiveSession';

export const sessionAssistantKeys = {
  panel: (sessionId: string, language: string) =>
    [...liveSessionKeys.all, 'assistant', sessionId, language] as const,
};

export interface AssistantConversationMessage {
  readonly id: string;
  readonly role: string;
  readonly content: string;
}

export interface AssistantTurnState {
  /** A turn is streaming: sending is disabled. */
  readonly running: boolean;
  /** The tool running now, from the last `status` event; null at the start of the turn. */
  readonly tool: string | null;
  /** The code of the last turn that failed: from an `error` event, or the HTTP refusal. */
  readonly errorCode: string | null;
}

export interface AssistantActionError {
  readonly proposalId: string;
  readonly code: string | null;
}

export interface UseSessionAssistantResult {
  /** Null until the first read arrives. */
  readonly availability: { readonly enabled: boolean; readonly code: string | null } | null;
  readonly isLoading: boolean;
  readonly loadError: Error | null;
  readonly messages: readonly AssistantConversationMessage[];
  readonly proposals: readonly AssistantProposalDto[];
  readonly turn: AssistantTurnState;
  /** The proposal being confirmed; while it is set, no other confirmation starts. */
  readonly confirmingId: string | null;
  readonly rejectingId: string | null;
  readonly actionError: AssistantActionError | null;
  ask(question: string): Promise<void>;
  confirm(proposalId: string, points?: number): Promise<void>;
  reject(proposalId: string): Promise<void>;
}

const IDLE_TURN: AssistantTurnState = { running: false, tool: null, errorCode: null };

/** The fallback when a turn fails without a code (a network error, a broken stream). */
export const ASSISTANT_UNAVAILABLE_CODE = 'assistant_unavailable';

function upsert(
  proposals: readonly AssistantProposalDto[],
  proposal: AssistantProposalDto
): AssistantProposalDto[] {
  const index = proposals.findIndex(p => p.id === proposal.id);
  if (index < 0) return [...proposals, proposal];
  const next = [...proposals];
  next[index] = proposal;
  return next;
}

function messagesOf(panel: SessionAssistantPanelDto): AssistantConversationMessage[] {
  return panel.messages.map(m => ({ id: m.id, role: m.role, content: m.content }));
}

function codeOf(error: unknown): string | null {
  return error instanceof ApiError ? (error.code ?? null) : null;
}

export function useSessionAssistant(
  sessionId: string,
  language: string,
  enabled = true
): UseSessionAssistantResult {
  const queryClient = useQueryClient();
  const panel = useQuery({
    queryKey: sessionAssistantKeys.panel(sessionId, language),
    queryFn: () => api.liveSessions.getAssistant(sessionId, language),
    enabled: enabled && sessionId.length > 0,
  });

  const [messages, setMessages] = useState<readonly AssistantConversationMessage[]>([]);
  const [proposals, setProposals] = useState<readonly AssistantProposalDto[]>([]);
  const [turn, setTurn] = useState<AssistantTurnState>(IDLE_TURN);
  const [confirmingId, setConfirmingId] = useState<string | null>(null);
  const [rejectingId, setRejectingId] = useState<string | null>(null);
  const [actionError, setActionError] = useState<AssistantActionError | null>(null);

  // Refs, not state: a second tap in the same tick must see the first one.
  const turnRunning = useRef(false);
  const confirming = useRef<string | null>(null);

  // The read is the truth: when it arrives, it replaces what a turn added on top.
  useEffect(() => {
    if (!panel.data || turnRunning.current) return;
    setMessages(messagesOf(panel.data));
    setProposals(panel.data.proposals);
  }, [panel.data]);

  /**
   * Reads the panel again and shows exactly that. Not an invalidation: when the server did not
   * change (a turn refused before it started), the query would return the same data, the effect
   * above would not run, and an unsaved question would stay on screen.
   */
  const resync = useCallback(async () => {
    try {
      const fresh = await queryClient.fetchQuery({
        queryKey: sessionAssistantKeys.panel(sessionId, language),
        queryFn: () => api.liveSessions.getAssistant(sessionId, language),
        staleTime: 0,
      });
      setMessages(messagesOf(fresh));
      setProposals(fresh.proposals);
    } catch {
      // The panel keeps what it shows; the next read fixes it.
    }
  }, [queryClient, sessionId, language]);

  const apply = useCallback(
    (event: AssistantTurnEvent, turnMessageId: { current: string | null }) => {
      switch (event.type) {
        case 'status':
          setTurn(previous => ({ ...previous, tool: event.tool ?? null }));
          // Spec §5.2: the turn has expired every proposal still Pending from earlier turns.
          setProposals(previous =>
            previous.map(p =>
              p.status === ASSISTANT_PROPOSAL_STATUS.pending &&
              p.messageId !== turnMessageId.current
                ? { ...p, status: ASSISTANT_PROPOSAL_STATUS.expired }
                : p
            )
          );
          break;
        case 'proposal':
          if (event.proposal) {
            const proposal = event.proposal;
            turnMessageId.current = proposal.messageId;
            setProposals(previous => upsert(previous, proposal));
          }
          break;
        case 'answer':
          if (event.text) {
            const text = event.text;
            setMessages(previous => [
              ...previous,
              {
                id: turnMessageId.current ?? `answer-${previous.length}`,
                role: 'assistant',
                content: text,
              },
            ]);
          }
          break;
        case 'error':
          setTurn(previous => ({
            ...previous,
            errorCode: event.code ?? ASSISTANT_UNAVAILABLE_CODE,
          }));
          break;
        default:
          break;
      }
    },
    []
  );

  const ask = useCallback(
    async (question: string) => {
      const text = question.trim();
      if (!text || turnRunning.current) return;
      turnRunning.current = true;
      setTurn({ running: true, tool: null, errorCode: null });
      setMessages(previous => [
        ...previous,
        { id: `question-${previous.length}`, role: 'user', content: text },
      ]);

      const turnMessageId: { current: string | null } = { current: null };
      try {
        for await (const event of askSessionAssistant(sessionId, text, language)) {
          apply(event, turnMessageId);
        }
      } catch (error) {
        setTurn(previous => ({
          ...previous,
          errorCode: codeOf(error) ?? ASSISTANT_UNAVAILABLE_CODE,
        }));
      } finally {
        turnRunning.current = false;
        setTurn(previous => ({ ...previous, running: false, tool: null }));
        await resync();
      }
    },
    [sessionId, language, apply, resync]
  );

  const confirm = useCallback(
    async (proposalId: string, points?: number) => {
      if (confirming.current !== null) return;
      confirming.current = proposalId;
      setConfirmingId(proposalId);
      setActionError(null);
      try {
        const result = await api.liveSessions.confirmAssistantProposal(
          sessionId,
          proposalId,
          points
        );
        setProposals(previous => upsert(previous, result));
        // The confirmation wrote the game: the scores and the turn on screen are stale.
        await queryClient.invalidateQueries({ queryKey: liveSessionKeys.detail(sessionId) });
      } catch (error) {
        setActionError({ proposalId, code: codeOf(error) });
        // A refusal may have closed the proposal (Stale, Failed): the read says how.
        await resync();
      } finally {
        confirming.current = null;
        setConfirmingId(null);
      }
    },
    [sessionId, queryClient, resync]
  );

  const reject = useCallback(
    async (proposalId: string) => {
      if (confirming.current === proposalId) return;
      setRejectingId(proposalId);
      setActionError(null);
      try {
        const result = await api.liveSessions.rejectAssistantProposal(sessionId, proposalId);
        setProposals(previous => upsert(previous, result));
      } catch (error) {
        setActionError({ proposalId, code: codeOf(error) });
        await resync();
      } finally {
        setRejectingId(null);
      }
    },
    [sessionId, resync]
  );

  return {
    availability: panel.data
      ? {
          enabled: panel.data.availability.enabled,
          code: panel.data.availability.code ?? null,
        }
      : null,
    isLoading: panel.isLoading,
    loadError: panel.error,
    messages,
    proposals,
    turn,
    confirmingId,
    rejectingId,
    actionError,
    ask,
    confirm,
    reject,
  };
}
