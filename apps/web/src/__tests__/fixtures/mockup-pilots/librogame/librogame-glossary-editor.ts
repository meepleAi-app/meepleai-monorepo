/**
 * Fixtures + MSW handlers for GlossaryEditorModal story — DS-17 Phase D-2 (sub-issue #2174).
 *
 * State-driving strategy:
 *   GlossaryEditorModal manages local state via `useReducer`. To render each
 *   mockup state STATICALLY (snapshot harness never runs `play`), we use:
 *
 *   - Test-seam prop `_initialState?: Partial<ModalState>` added to the real
 *     component. It merges on top of `makeInitialState(entry)` at mount time,
 *     pre-seeding `itValue`, `status`, `errorMessage`, and `collidingEntry`.
 *
 *   - MSW PUT handler returning 500 for the `save-error` frame. MSW is active in
 *     Storybook via `@storybook/addon-msw`, so the frame renders its error
 *     banner on first paint because `status` is pre-seeded to `'error'` via
 *     `_initialState`.
 *
 *   - Template K mocks (`GlossaryEditorBulkImportMock`, `GlossaryEditorVariantsMock`)
 *     for `bulk-import` and `variants` states — these sub-screens exist in the
 *     mockup spec but are NOT implemented in the real component.
 *
 * 6 mockup states → 6 story frames:
 *
 *   Frame01_EditPristine   — entry pre-filled, itValue == initialIt (Salva disabled)
 *   Frame02_Edited         — _initialState.itValue != initialIt, diff-hint shown
 *   Frame03_SaveError      — _initialState.status='error', error banner shown
 *   Frame04_Collision      — _initialState.status='collision' + collidingEntry
 *   Frame05_BulkImport     — Template K mock (forward-refactor, not implemented)
 *   Frame06_Variants       — Template K mock (forward-refactor, not implemented)
 *
 * MSW required:
 *   - PUT /api/v1/gamebook/campaigns/:campaignId/glossary/:entryId → 500
 *     Used only for Frame03_SaveError (so MSW sees a failing mutation if the
 *     user were to click Retry). `_initialState` drives the visual state; MSW
 *     ensures `upsert.mutate` would indeed fail if called.
 *
 * @mockup admin-mockups/design_files/librogame-runthrough-glossary-editor.html
 * Refs: umbrella #2063, sub-issue #2174 (Phase D-2).
 */

import { http, HttpResponse } from 'msw';

import type { GamebookGlossaryEntry } from '@/lib/api/gamebook-glossary';
import type { ModalState } from '@/components/features/gamebook/GlossaryEditorModal';

const API_BASE = process.env.NEXT_PUBLIC_API_BASE || 'http://localhost:8080';

// ---------------------------------------------------------------------------
// Valid UUIDs (Zod .uuid() enforced on GamebookGlossaryEntrySchema.id)
// ---------------------------------------------------------------------------

export const MOCK_CAMPAIGN_ID_GE = '00000000-0000-0000-0000-000000000030';
export const MOCK_ENTRY_ID_GE = '00000000-0000-0000-0000-000000000031';
export const MOCK_COLLIDING_ENTRY_ID_GE = '00000000-0000-0000-0000-000000000032';

// ---------------------------------------------------------------------------
// Base glossary entry — "sentinel" from the mockup narrative
// ---------------------------------------------------------------------------

export const MOCK_GLOSSARY_ENTRY_GE: GamebookGlossaryEntry = {
  id: MOCK_ENTRY_ID_GE,
  termEn: 'sentinel',
  termIt: 'soldato di guardia',
  source: 'AutoBootstrap',
  updatedAt: '2026-06-22T20:00:00Z',
};

// ---------------------------------------------------------------------------
// _initialState overrides for static state driving
// ---------------------------------------------------------------------------

/** Frame02 — itValue dirtied to the edited translation. */
export const INITIAL_STATE_EDITED: Partial<ModalState> = {
  itValue: 'punto di osservazione strategica',
};

/** Frame03 — save-error banner pre-seeded. */
export const INITIAL_STATE_SAVE_ERROR: Partial<ModalState> = {
  itValue: 'punto di osservazione strategica',
  status: 'error',
  errorMessage: 'Internal Server Error',
};

/** Frame04 — collision banner pre-seeded with a conflicting entry. */
export const INITIAL_STATE_COLLISION: Partial<ModalState> = {
  itValue: 'punto di osservazione strategica',
  status: 'collision',
  collidingEntry: {
    id: MOCK_COLLIDING_ENTRY_ID_GE,
    termEn: 'ferrigni',
  },
};

// ---------------------------------------------------------------------------
// MSW handler — Frame03: PUT /glossary/:entryId → 500 (save fails)
// ---------------------------------------------------------------------------

export const mswSaveErrorHandler = [
  http.put(
    `${API_BASE}/api/v1/gamebook/campaigns/${MOCK_CAMPAIGN_ID_GE}/glossary/${MOCK_ENTRY_ID_GE}`,
    () => new HttpResponse(null, { status: 500, statusText: 'Internal Server Error' })
  ),
];
