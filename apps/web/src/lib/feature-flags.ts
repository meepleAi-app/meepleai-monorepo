/**
 * Feature flag helpers.
 *
 * All flags read from build-time `NEXT_PUBLIC_*` env vars.
 * Changing a flag requires a rebuild (not a restart).
 */

/**
 * Desktop UX redesign (Phase 1 — shell, Phase 2 — dashboard, Phase 3 — library hub, Phase 4 — chat panel).
 *
 * Controlled by `NEXT_PUBLIC_UX_REDESIGN=true`. Defaults to false.
 * See: `docs/superpowers/specs/2026-04-08-desktop-ux-redesign-design.md`
 */
export function isUxRedesignEnabled(): boolean {
  return process.env.NEXT_PUBLIC_UX_REDESIGN === 'true';
}
