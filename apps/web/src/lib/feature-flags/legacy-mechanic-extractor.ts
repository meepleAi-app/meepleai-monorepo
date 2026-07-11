/**
 * Legacy (Variant C) Mechanic Extractor editor gate (ADR-051, #537 ME-M4.2).
 *
 * The Variant C manual editor at `/admin/knowledge-base/mechanic-extractor` is deprecated in favor of
 * the AI-first analyses flow (#528+). Its admin nav entry is HIDDEN by default and only shown when
 * `NEXT_PUBLIC_SHOW_LEGACY_MECHANIC_EXTRACTOR=true` (an env override for staging/debug). The route
 * itself still resolves (with the #536 deprecation banner) so existing bookmarks keep working; only
 * the menu entry is gated. Code removal is #538 (gated +6 months).
 *
 * Centralised here so the flag has one source of truth (mirrors `mechanic-validation.ts`).
 */
export function isLegacyMechanicExtractorEnabled(): boolean {
  return process.env.NEXT_PUBLIC_SHOW_LEGACY_MECHANIC_EXTRACTOR === 'true';
}
