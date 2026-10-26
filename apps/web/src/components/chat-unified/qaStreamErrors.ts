/**
 * qaStreamErrors — #4168.
 *
 * I codici con cui il backend chiude una risposta senza darla (gateway LLM e pipeline di risposta,
 * ADR-095), resi in italiano. Prima la chat mostrava il messaggio grezzo del backend, in inglese:
 * la traduzione dei codici del tetto di spesa stava solo in `useAgentChatStream`, che questa chat
 * non usa.
 */

const MESSAGES: Readonly<Record<string, string>> = {
  AI_BUDGET_DAILY_EXHAUSTED:
    'Il servizio AI è in pausa fino a domani: è stato raggiunto il budget di spesa del giorno.',
  AI_BUDGET_MONTHLY_EXHAUSTED:
    'Il servizio AI è in pausa fino al mese prossimo: è stato raggiunto il budget di spesa del mese.',
  AI_BUDGET_UNAVAILABLE: 'Il servizio AI non è disponibile al momento. Riprova più tardi.',
  AI_MODEL_NOT_IN_CATALOG: 'Il servizio AI non è disponibile al momento. Riprova più tardi.',
  AI_PROVIDER_UNAVAILABLE:
    'Il servizio AI è temporaneamente non disponibile. Riprova tra qualche minuto.',
  AI_STREAM_INTERRUPTED:
    'La risposta si è interrotta prima della fine, quindi non è completa. Riprova a chiedere.',
  AGENT_PROFILE_NOT_PUBLISHED: "L'assistente AI non è ancora configurato. Riprova più tardi.",
};

/** Il messaggio per l'utente: quello del codice se lo conosciamo, altrimenti quello del backend. */
export function qaStreamErrorMessage(errorCode?: string, backendMessage?: string): string {
  if (errorCode && Object.prototype.hasOwnProperty.call(MESSAGES, errorCode)) {
    return MESSAGES[errorCode];
  }

  return backendMessage ?? 'Errore nella risposta AI';
}

/**
 * #4388: le rotte di risposta (`/agents/qa/stream`, `/agents/qa`, `/knowledge-base/ask`,
 * `/knowledge-base/ask/global`) tengono aperte al massimo due risposte per utente. La terza domanda
 * riceve 429 prima che il gateway sia chiamato: non è un guasto, e un nuovo tentativo immediato
 * arriverebbe mentre le altre due sono ancora in corso. La frase dice di aspettare.
 */
export const QA_TOO_MANY_REQUESTS_MESSAGE =
  'Hai già due risposte in corso. Aspetta che una finisca, poi riprova.';

/** Il 429 delle rotte di risposta, come errore che i trasporti lanciano e le viste riconoscono. */
export class QaTooManyRequestsError extends Error {
  constructor() {
    super(QA_TOO_MANY_REQUESTS_MESSAGE);
    this.name = 'QaTooManyRequestsError';
  }
}

/** Chiamata dai trasporti subito dopo la fetch: per un 429 lancia, per ogni altro stato non fa nulla. */
export function throwIfQaTooManyRequests(response: { readonly status: number }): void {
  if (response.status === 429) {
    throw new QaTooManyRequestsError();
  }
}

/**
 * La frase per una domanda che non è nemmeno partita: quella del 429 se è lui, altrimenti
 * `fallback` (il guasto generico che la vista mostrava già).
 */
export function qaRequestFailureMessage(error: unknown, fallback: string): string {
  return error instanceof QaTooManyRequestsError ? error.message : fallback;
}
