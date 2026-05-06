/**
 * useTranslateParagraph — DEMO-ONLY workaround for G4 paragraph-number gap.
 *
 * Tracked in: <issue URL once filed>
 * Demo session: Nanolith one-shot, NOT for production.
 *
 * Architecture: wraps useAgentChatStream + RAG-based prompt translation.
 * Citations preserved for audit trail. No persistent thread (each translate()
 * call starts a fresh request — no chatThreadId continuity).
 *
 * Anti-hallucination: prompt instructs LLM to reply "Paragrafo non trovato"
 * if the paragraph is not found in the knowledge base, rather than fabricating
 * a translation. This is testable behaviorally via the translation text output.
 *
 * Goal 3 / Path 5a workaround:
 *   The structural backend fix (G4 enhancement — expose typed paragraph-number
 *   lookup endpoint) is tracked in a separate backlog issue. This hook is a
 *   bridge until that endpoint ships.
 */

'use client';

import { useCallback, useState } from 'react';

import { useAgentChatStream, type AgentChatStreamCallbacks } from '@/hooks/useAgentChatStream';

// ---------------------------------------------------------------------------
// Public types
// ---------------------------------------------------------------------------

export interface ParagraphCitation {
  readonly docType: 'rulebook' | 'guide' | 'storybook' | 'encounterbook' | string;
  readonly pageNumber: number;
  readonly snippet: string;
  readonly relevanceScore?: number;
}

export interface TranslateParagraphInput {
  readonly paragraphRef: string | number;
  readonly chapterContext?: string;
  readonly preferDocType?: ParagraphCitation['docType'];
}

export interface UseTranslateParagraphInput {
  readonly gameId: string;
  readonly agentId: string;
  /** When false the hook is inert (no-op on translate()). Defaults to true. */
  readonly enabled?: boolean;
}

export interface UseTranslateParagraphResult {
  /** Accumulated translation text from the SSE stream. */
  readonly translation: string;
  /** Source citations from the RAG retrieval step. */
  readonly citations: ReadonlyArray<ParagraphCitation>;
  /** True while the SSE stream is open. */
  readonly isStreaming: boolean;
  /** True when the last request ended in error. */
  readonly isError: boolean;
  /** Human-readable error message, or null when no error. */
  readonly error: string | null;
  /** Trigger a translation. No-op when gameId/agentId are empty or enabled=false. */
  readonly translate: (input: TranslateParagraphInput) => void;
  /** Clear state (translation, citations, error). */
  readonly reset: () => void;
  /** The last input passed to translate(), or null before first call. */
  readonly lastInput: TranslateParagraphInput | null;
}

// ---------------------------------------------------------------------------
// Prompt composition — exported for unit testing
// ---------------------------------------------------------------------------

/**
 * Compose the Italian-language translation prompt sent to the chat agent.
 *
 * Structure:
 *  1. Task declaration (paragraph ref + optional chapter + optional docType hint)
 *  2. Output requirements list (format, tone, anti-hallucination guard)
 *
 * DEMO-ONLY: prompt is hardcoded in Italian; no next-intl integration.
 */
export function composeTranslationPrompt(input: TranslateParagraphInput): string {
  const { paragraphRef, chapterContext, preferDocType } = input;

  const chapterPart =
    chapterContext != null && chapterContext.trim() !== ''
      ? ` del capitolo "${chapterContext.trim()}"`
      : '';

  const docTypeHint =
    preferDocType != null && preferDocType.trim() !== ''
      ? ` (cerca preferibilmente nel ${preferDocType.trim()})`
      : '';

  const task = `Traduci in italiano il paragrafo ${paragraphRef}${chapterPart} di questo gamebook${docTypeHint}.`;

  const requirementLines = [
    'Mantieni la numerazione interna del paragrafo.',
    'Preserva il tono e la suspense narrativa del testo originale.',
    'Non aggiungere commenti o spiegazioni fuori dal testo tradotto.',
    'Se il paragrafo non è presente nella knowledge base, rispondi SOLO con: "Paragrafo non trovato".',
    'Includi alla fine la citazione della pagina sorgente tra parentesi, es.: (Storybook, p. 42).',
  ]
    .map(r => `- ${r}`)
    .join('\n');

  return `${task}\n\nRequisiti di output:\n${requirementLines}`;
}

// ---------------------------------------------------------------------------
// Internal helper: parse inline citations from completed answer text
// ---------------------------------------------------------------------------

/**
 * Best-effort parser for inline citations in the form "(DocType, p. N)".
 *
 * DEMO-ONLY: production should use typed citation objects from the backend.
 * Returns an empty array when no matches are found.
 */
function parseInlineCitations(answer: string): ParagraphCitation[] {
  // Match: (word-or-words, p. digits) — e.g. (Storybook, p. 42)
  const parts = answer.split(/\([^)]+,\s*p\.\s*\d+\)/);
  const result: ParagraphCitation[] = [];

  // Re-collect matched substrings by working through the original string
  let cursor = 0;
  for (let i = 0; i < parts.length - 1; i++) {
    cursor += parts[i].length;
    // Find the closing paren of the citation starting at cursor
    const closeIdx = answer.indexOf(')', cursor);
    if (closeIdx !== -1) {
      const raw = answer.slice(cursor + 1, closeIdx); // content between ( and )
      const commaIdx = raw.lastIndexOf(',');
      if (commaIdx !== -1) {
        const docType = raw.slice(0, commaIdx).trim().toLowerCase();
        const pageRaw = raw
          .slice(commaIdx + 1)
          .replace(/p\.\s*/i, '')
          .trim();
        const pageNumber = parseInt(pageRaw, 10);
        if (docType && !isNaN(pageNumber)) {
          result.push({ docType, pageNumber, snippet: '' });
        }
      }
      cursor = closeIdx + 1;
    }
  }

  return result;
}

// ---------------------------------------------------------------------------
// Hook implementation
// ---------------------------------------------------------------------------

/**
 * DEMO-ONLY hook — wraps useAgentChatStream to translate a storybook paragraph
 * from English to Italian via RAG-based prompt.
 *
 * Citations are parsed from the plain-text answer (inline "(DocType, p. N)"
 * pattern) since useAgentChatStream does not expose Citations SSE event data
 * via state — only via the onComplete callback's answer text.
 *
 * @example
 * ```tsx
 * const { translation, isStreaming, translate } = useTranslateParagraph({
 *   gameId: 'uuid-of-game',
 *   agentId: 'uuid-of-agent',
 * });
 * translate({ paragraphRef: 42, chapterContext: 'Il Mercato' });
 * ```
 */
export function useTranslateParagraph(
  input: UseTranslateParagraphInput
): UseTranslateParagraphResult {
  const { gameId, agentId, enabled = true } = input;

  const [citations, setCitations] = useState<ReadonlyArray<ParagraphCitation>>([]);
  const [lastInput, setLastInput] = useState<TranslateParagraphInput | null>(null);

  const callbacks: AgentChatStreamCallbacks = {
    onComplete: answer => {
      const parsed = parseInlineCitations(answer);
      if (parsed.length > 0) {
        setCitations(parsed);
      }
    },
  };

  const { state, sendMessage, reset: resetStream } = useAgentChatStream(callbacks);

  const translate = useCallback(
    (translateInput: TranslateParagraphInput): void => {
      if (!enabled) return;
      if (!gameId.trim() || !agentId.trim()) return;

      setLastInput(translateInput);
      setCitations([]);
      const prompt = composeTranslationPrompt(translateInput);
      // No chatThreadId — each translation is a fresh, stateless request.
      sendMessage(agentId, prompt);
    },
    [enabled, gameId, agentId, sendMessage]
  );

  const reset = useCallback((): void => {
    resetStream();
    setCitations([]);
    setLastInput(null);
  }, [resetStream]);

  return {
    translation: state.currentAnswer,
    citations,
    isStreaming: state.isStreaming,
    isError: state.connectionStatus === 'error',
    error: state.error,
    translate,
    reset,
    lastInput,
  };
}
