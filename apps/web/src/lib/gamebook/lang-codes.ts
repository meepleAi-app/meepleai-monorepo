/**
 * Language codes supported by the gamebook translate viewer.
 *
 * Aligned with BE PR #1787 allowlist (DEC-3 BE):
 * NTextCat detection filters to these 5 ISO 639-1 UPPERCASE codes.
 * The 5 radio options of LangOverrideModal mirror this set.
 *
 * Target lang is fixed to IT in v1 (Aaron CORE row K/L). Future i18n
 * may add a target picker via separate epic.
 */
export type SourceLangCode = 'EN' | 'FR' | 'DE' | 'ES' | 'IT';

/** Ordered presentation list for radio options (modal). */
export const LANG_CODES_ORDER: readonly SourceLangCode[] = ['EN', 'FR', 'DE', 'ES', 'IT'];

/** Italian human-readable labels for radio + badge display. */
export const LANG_LABELS_IT: Record<SourceLangCode, string> = {
  EN: 'Inglese',
  FR: 'Francese',
  DE: 'Tedesco',
  ES: 'Spagnolo',
  IT: 'Italiano',
};

/** Type guard for runtime parsing of BE-emitted lang strings. */
export function isSourceLangCode(value: unknown): value is SourceLangCode {
  return typeof value === 'string' && (LANG_CODES_ORDER as readonly string[]).includes(value);
}
