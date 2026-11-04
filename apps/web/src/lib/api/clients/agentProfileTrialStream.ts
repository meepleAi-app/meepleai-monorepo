/**
 * #4169 (6c-3, ADR-095 D8): la prova della bozza del profilo dell'agente dalla pagina «Agente».
 *
 * `POST /api/v1/admin/agent-profile/draft/try` risponde con lo stesso stream SSE delle risposte agli
 * utenti, ma con la bozza al posto della versione pubblicata. Una risposta che non è 200 (per esempio
 * il 409 «nessuna bozza») porta un messaggio, che arriva a chi chiama come errore.
 */
import { getApiBase } from '../core/httpClient';

export interface AgentProfileTrialRequest {
  gameId: string;
  question: string;
}

export interface AgentProfileTrialEvent {
  type: number;
  data: unknown;
}

/** I tipi di evento che la pagina legge (StreamingEventType del backend). */
export const TRIAL_EVENT = {
  StateUpdate: 0,
  Citations: 1,
  Complete: 4,
  Error: 5,
  Token: 7,
} as const;

async function errorFrom(response: Response): Promise<Error> {
  try {
    const body = (await response.json()) as { message?: string; error?: string };
    return new Error(body.message ?? body.error ?? `HTTP ${response.status}`);
  } catch {
    return new Error(`HTTP ${response.status}`);
  }
}

export async function* tryAgentProfileDraft(
  request: AgentProfileTrialRequest,
  signal?: AbortSignal
): AsyncGenerator<AgentProfileTrialEvent> {
  const response = await fetch(`${getApiBase()}/api/v1/admin/agent-profile/draft/try`, {
    method: 'POST',
    headers: { 'Content-Type': 'application/json' },
    body: JSON.stringify(request),
    credentials: 'include',
    signal,
  });

  if (!response.ok) {
    throw await errorFrom(response);
  }

  const reader = response.body?.getReader();
  if (!reader) throw new Error('No response body');

  const decoder = new TextDecoder();
  let buffer = '';
  try {
    while (true) {
      const { done, value } = await reader.read();
      if (done) break;
      buffer += decoder.decode(value, { stream: true });

      const parts = buffer.split('\n\n');
      buffer = parts.pop() ?? '';
      for (const part of parts) {
        const raw = part
          .split('\n')
          .filter(line => line.startsWith('data:'))
          .map(line => line.slice(5).trim())
          .join('\n');
        if (!raw) continue;
        try {
          const event = JSON.parse(raw) as { type: number; data: unknown };
          yield { type: event.type, data: event.data };
        } catch {
          // Un evento malformato si salta: lo stream continua.
        }
      }
    }
  } finally {
    reader.releaseLock();
  }
}
