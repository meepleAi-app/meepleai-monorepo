/**
 * Answer intents (#4404, ADR-098 §5).
 *
 * The chat selector no longer chooses an «agent» — there is one (ADR-094) — it chooses the SHAPE of the answer that
 * agent gives:
 *
 *   - `auto`   — the answer of today;
 *   - `tutor`  — the rule step by step, with an example from play;
 *   - `secca`  — one line and the citation (shown as «Risposta secca»).
 *
 * The backend names the same three in `AnswerIntent.cs` and a test compares the two lists. The thread stores the
 * choice in `agentType`; a value outside the list — the retired `arbitro`, `decisore`, `stratega`, `narratore`, or
 * anything an older client wrote — is read as `auto`, here as on the server, so the selector is never left empty.
 *
 * The id of the third one is `secca` and not `decisore`: «Decisore» already means the strategic move suggestions
 * in this repo, which are the answers without sources that ADR-098 §5 takes out of the chat.
 */

export const ANSWER_INTENTS = ['auto', 'tutor', 'secca'] as const;

export type AnswerIntent = (typeof ANSWER_INTENTS)[number];

export const ANSWER_INTENT_LABELS: Record<AnswerIntent, string> = {
  auto: 'Auto',
  tutor: 'Tutor',
  secca: 'Risposta secca',
};

export const ANSWER_INTENT_DESCRIPTIONS: Record<AnswerIntent, string> = {
  auto: 'La risposta di sempre',
  tutor: 'Spiega la regola passo passo, con un esempio',
  secca: 'Una riga e la citazione',
};

/** The intent stored on a thread, as the server reads it: trimmed, lower case, and `auto` outside the list. */
export function intentOf(stored: string | null | undefined): AnswerIntent {
  const normalized = stored?.trim().toLowerCase() ?? '';
  return (ANSWER_INTENTS as readonly string[]).includes(normalized)
    ? (normalized as AnswerIntent)
    : 'auto';
}
