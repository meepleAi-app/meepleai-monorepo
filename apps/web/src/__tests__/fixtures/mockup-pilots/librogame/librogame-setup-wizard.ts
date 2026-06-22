/**
 * Fixtures + MSW handlers for CampaignSetupDrawer story — DS-17 Phase D-2 (sub-issue #2174).
 *
 * State-driving strategy:
 *   Frame01_Step1Name  — real component, controlled open=true, step 1 on mount.
 *   Frame02_Step2Players — play function: click "Avanti →" once (step 1→2).
 *   Frame03_Step3Confirm — play function: click "Avanti →" twice (step 1→2→3).
 *                          MSW handler mocks POST /api/v1/gamebook/campaigns so the
 *                          submit button renders without a network error on click.
 *   Frame04_ValidationErr — play function: clear the title input so titleError renders
 *                           inline and "Avanti →" is disabled.
 *
 * Portal note (vaul):
 *   DrawerContent renders into a vaul portal outside canvasElement.
 *   All play functions target `document.body`, not `canvasElement`.
 *
 * MSW handlers:
 *   mswForLibrogameSetupWizardState — story-level override for Frame03 POST mock.
 *
 * @mockup admin-mockups/design_files/librogame-runthrough-setup-wizard.html
 * Refs: umbrella #2063, sub-issue #2174 (Phase D-2).
 */

import { http, HttpResponse } from 'msw';

const API_BASE = process.env.NEXT_PUBLIC_API_BASE || 'http://localhost:8080';

// ── Mock campaign response ───────────────────────────────────────────────────
// Returned by the POST /api/v1/gamebook/campaigns handler for Frame03_Step3Confirm.
// Shape matches GamebookCampaignSchema (gamebook-campaigns.ts).
export const MOCK_CREATED_CAMPAIGN = {
  id: 'campaign-story-mock-001',
  gameRefId: 'game-nanolith-001',
  gameRefKind: 0,
  ownerUserId: 'user-aaron-001',
  title: 'Campagna con i ragazzi',
  currentParagraph: 0,
  history: [],
  lastReadAt: '2026-06-22T10:00:00+00:00',
  createdAt: '2026-06-22T10:00:00+00:00',
  updatedAt: '2026-06-22T10:00:00+00:00',
} as const;

// ── MSW handler — POST /api/v1/gamebook/campaigns ───────────────────────────
// Story-level handler used in Frame03_Step3Confirm parameters.msw.handlers.
// Returns a successful 201 so the submit button doesn't fail on click.
export const mswForLibrogameSetupWizardState = [
  http.post(`${API_BASE}/api/v1/gamebook/campaigns`, () => {
    return HttpResponse.json(MOCK_CREATED_CAMPAIGN, { status: 201 });
  }),
];
