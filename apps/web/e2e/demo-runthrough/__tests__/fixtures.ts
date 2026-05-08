/**
 * @demo-runthrough fixtures — credentials + sample data references for the
 * Nanolith caso d'uso end-to-end Playwright spec.
 *
 * These fixtures assume the local stack has been seeded via:
 *   make seed-nanolith-demo
 *
 * which creates:
 *   - account badsworm@gmail.com (superadmin) with password TestNanolith2026!
 *   - Nanolith game in collection
 *   - Nanolith Press Start + Rules KB indicizzato
 *   - Nanolith Tutor agent attivo
 *
 * Spec: docs/superpowers/specs/2026-05-07-libro-game-nanolith-demo-design.md
 *       docs/superpowers/plans/2026-05-08-nanolith-demo-runthrough-phase-a.md
 */

export const NANOLITH_DEMO_USER = {
  email: 'badsworm@gmail.com',
  password: 'TestNanolith2026!',
} as const;

export const NANOLITH_GAME_TITLE = 'Nanolith';
export const NANOLITH_AGENT_NAME = 'Nanolith Tutor';

export const SETUP_PROMPT_4_PLAYERS = 'come si imposta la partita per 4 giocatori?';

/**
 * Path (relative to apps/web/) to a sample storybook page PNG used to exercise
 * the photo upload + segment + translate flow.
 *
 * Phase A note: this is currently a 1×1 transparent PNG fallback (Option B in
 * Task 9 spec). The real OCR/segmentation flow requires a multi-page PDF page
 * extract (Option A) which depends on `pdftoppm` not available in the current
 * dev environment. The full G2.1 manual runthrough will substitute a real
 * camera capture or pre-extracted page; the @ci automated smoke can still
 * exercise the upload mechanics + ensure no JavaScript errors short-circuit
 * the FSM transition (uploading → segmenting → segments_ready).
 *
 * If/when a real fixture is added, replace the path target — no spec change
 * needed beyond updating expectations on segment count.
 */
export const STORYBOOK_FIXTURE_PATH = 'e2e/fixtures/storybook-page-sample.png';
