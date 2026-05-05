/**
 * Gamebook — useTranslateParagraph hook (Phase 3, Task 3.5a — STUB)
 *
 * Hook for translating a game-book paragraph via NarrativeTranslationService.
 *
 * IMPORTANT: The backend endpoint /api/v1/translation/translate-narrative is NOT
 * yet exposed publicly. INarrativeTranslationService is currently internal-only.
 * For G5 MVP, this hook returns a passthrough (no actual translation).
 *
 * Wiring deferred to Phase 3 Task 3.5e — see relevant GitHub issue tracker.
 */

'use client';

import { useMutation, type UseMutationResult } from '@tanstack/react-query';

interface TranslateParagraphInput {
  text: string;
  sourceLanguage: string;
  targetLanguage: string;
  /** Optional game context for domain-aware translation */
  gameContext?: string;
}

interface TranslationResult {
  translatedText: string;
  cached: boolean;
}

/**
 * Mutation hook for translating a paragraph.
 *
 * @stub Backend endpoint deferred — Phase 3 Task 3.5e.
 * Currently returns the input text unchanged (passthrough).
 *
 * @example
 * ```tsx
 * const { mutateAsync: translate, isPending } = useTranslateParagraph();
 * const result = await translate({ text, sourceLanguage: 'it', targetLanguage: 'en' });
 * ```
 */
export function useTranslateParagraph(): UseMutationResult<
  TranslationResult,
  Error,
  TranslateParagraphInput
> {
  return useMutation<TranslationResult, Error, TranslateParagraphInput>({
    mutationFn: async input => {
      // STUB: passthrough — no real translation until backend endpoint is exposed.
      // Phase 3 Task 3.5e will wire this to /api/v1/translation/translate-narrative.

      console.warn(
        '[useTranslateParagraph] STUB — backend translation endpoint deferred to Phase 3 Task 3.5e'
      );
      return Promise.resolve({
        translatedText: input.text, // passthrough: return original text unchanged
        cached: false,
      });
    },
  });
}
