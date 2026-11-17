/**
 * The chat history of /chat, grouped by agent: a custom agent by id, a system one by its type, the rest under
 * «Chat generali». Most recently active group first.
 */

import type { ChatSessionSummaryDto } from '@/lib/api/schemas/chat-sessions.schemas';
import { ANSWER_INTENT_LABELS, intentOf } from '@/lib/chat/answer-intent';

export interface AgentGroup {
  /** Stable key for collapsing state */
  key: string;
  /** Display label (agent name or system type) */
  label: string;
  sessions: ChatSessionSummaryDto[];
}

export function groupSessionsByAgent(sessions: ChatSessionSummaryDto[]): AgentGroup[] {
  const map = new Map<string, AgentGroup>();

  for (const session of sessions) {
    let key: string;
    let label: string;

    if (session.agentId) {
      key = `agent:${session.agentId}`;
      label = session.agentName ?? 'Agente personalizzato';
    } else if (session.agentType) {
      // #5001 (ADR-098 §5): the labels of the intents, as in the selector. A retired or unknown value reads as
      // «Auto», as in the rest of the app, and joins the auto group.
      const intent = intentOf(session.agentType);
      key = `type:${intent}`;
      label = ANSWER_INTENT_LABELS[intent];
    } else {
      key = 'none';
      label = 'Chat generali';
    }

    if (!map.has(key)) {
      map.set(key, { key, label, sessions: [] });
    }
    const group = map.get(key);
    if (group) group.sessions.push(session);
  }

  // Sort: most recently active group first (use max across all sessions in group)
  const latestDate = (group: AgentGroup) =>
    group.sessions.reduce((max, s) => {
      const d = s.lastMessageAt ?? s.createdAt ?? '';
      return d > max ? d : max;
    }, '');

  return [...map.values()].sort(
    (a, b) => new Date(latestDate(b)).getTime() - new Date(latestDate(a)).getTime()
  );
}
