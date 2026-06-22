/**
 * Fixtures + MSW handlers for GamebookPlayShell story — DS-17 Phase D-2 (sub-issue #2174).
 *
 * State-driving strategy:
 *   The real GamebookPlayShell is a form-based shell (paragraph progress + chat open).
 *   It has NO tabs, NO chat overlay, NO glossary sheet — these are aspirational mockup
 *   states not yet implemented in the real component.
 *
 *   Four frames are driven from what the real component CAN render statically:
 *
 *   Frame01_Story Tab     — campaign §289 loaded (single narrative book auto-selected),
 *                           paragraph history visible. MSW mocks GET /campaigns/{id} +
 *                           GET /gamebook/books. Maps to mockup state-01 story-tab.
 *
 *   Frame02_Encounter Tab — same campaign + 2 narrative books → BookPicker visible.
 *                           Maps to mockup state-02 encounter-tab (multi-book context).
 *
 *   Frame03_Chat Overlay  — same campaign + Zustand decorator seeds
 *                           useChatPanelStore.isOpen=true so the "open chat" button
 *                           shows the active-panel CSS context. Maps to mockup state-03
 *                           chat-overlay (chat active).
 *
 *   Frame04_Glossary Inline — campaign with 10-entry history list visible below the form.
 *                             Maps to mockup state-04 glossary-inline (rich context panel).
 *
 * MSW required:
 *   - GET /api/v1/gamebook/campaigns/{id}  → mswForLibrogamePlaySessionState (base)
 *   - GET /api/v1/gamebook/books?...       → embedded in mswForLibrogamePlaySessionState
 *   These prevent the skeleton from rendering (isLoading=true branch).
 *
 * @mockup admin-mockups/design_files/librogame-runthrough-play-session.html
 * Refs: umbrella #2063, sub-issue #2174 (Phase D-2).
 */

import { http, HttpResponse } from 'msw';

const API_BASE = process.env.NEXT_PUBLIC_API_BASE || 'http://localhost:8080';

// ---------------------------------------------------------------------------
// Constants
// ---------------------------------------------------------------------------

export const MOCK_CAMPAIGN_ID = 'campaign-eldoria-abc123';
export const MOCK_GAME_ID = 'game-nanolith-001';
export const MOCK_BOOK_ID_STORY = 'book-storybook-001';
export const MOCK_BOOK_ID_ENCOUNTER = 'book-encounter-001';

// ---------------------------------------------------------------------------
// Campaign DTO — §289 Cap 4 "La grotta dei Goblin"
// Shape matches GamebookCampaignSchema (gamebook-campaigns.ts).
// ---------------------------------------------------------------------------

export const MOCK_LIBROGAME_PLAY_SESSION_CAMPAIGN = {
  id: MOCK_CAMPAIGN_ID,
  gameRefId: MOCK_GAME_ID,
  gameRefKind: 0,
  ownerUserId: 'user-aaron-001',
  title: 'Con i ragazzi — Eldoria',
  currentParagraph: 289,
  history: [134, 189, 198, 214, 218, 271, 289],
  lastReadAt: '2026-06-22T22:14:00+00:00',
  createdAt: '2026-05-10T18:00:00+00:00',
  updatedAt: '2026-06-22T22:14:00+00:00',
} as const;

/** Variant with 10-entry history for Frame04 (history list renders below form). */
export const MOCK_LIBROGAME_PLAY_SESSION_CAMPAIGN_RICH_HISTORY = {
  ...MOCK_LIBROGAME_PLAY_SESSION_CAMPAIGN,
  history: [100, 112, 124, 134, 155, 170, 189, 198, 214, 289],
} as const;

// ---------------------------------------------------------------------------
// GameBook DTOs — shape matches GameBookDtoSchema (gamebook.ts).
// roles=4 = Narrative (GameBookRole.Narrative).
// ---------------------------------------------------------------------------

export const MOCK_GAME_BOOK_STORY = {
  id: MOCK_BOOK_ID_STORY,
  gameRefId: MOCK_GAME_ID,
  gameRefKind: 0,
  ownerUserId: null,
  displayName: 'Storybook',
  roles: 4, // Narrative
  paragraphScheme: 1,
  language: 'it',
  sequentialRead: false,
  kbSourceDocId: null,
  physicalOnly: false,
  createdAt: '2026-05-10T18:00:00Z',
} as const;

export const MOCK_GAME_BOOK_ENCOUNTER = {
  id: MOCK_BOOK_ID_ENCOUNTER,
  gameRefId: MOCK_GAME_ID,
  gameRefKind: 0,
  ownerUserId: null,
  displayName: 'Encounter Book',
  roles: 4, // Also Narrative so it appears in the multi-book BookPicker
  paragraphScheme: 1,
  language: 'it',
  sequentialRead: false,
  kbSourceDocId: null,
  physicalOnly: false,
  createdAt: '2026-05-10T18:00:00Z',
} as const;

// ---------------------------------------------------------------------------
// MSW base handler — single book (Frames 01 + 03 + 04)
// ---------------------------------------------------------------------------
// Mocks:
//   GET /api/v1/gamebook/campaigns/{id}          → campaign §289
//   GET /api/v1/gamebook/books?gameRefId=...      → [STORY book only]
//   PUT /api/v1/gamebook/campaigns/{id}/progress  → echo updated campaign

export const mswForLibrogamePlaySessionState = [
  http.get(`${API_BASE}/api/v1/gamebook/campaigns/:id`, () => {
    return HttpResponse.json(MOCK_LIBROGAME_PLAY_SESSION_CAMPAIGN, { status: 200 });
  }),
  http.get(`${API_BASE}/api/v1/gamebook/books`, () => {
    return HttpResponse.json([MOCK_GAME_BOOK_STORY], { status: 200 });
  }),
  http.put(`${API_BASE}/api/v1/gamebook/campaigns/:id/progress`, () => {
    return HttpResponse.json(MOCK_LIBROGAME_PLAY_SESSION_CAMPAIGN, { status: 200 });
  }),
];

// ---------------------------------------------------------------------------
// MSW multi-book handler (Frame 02 — two narrative books → BookPicker visible)
// ---------------------------------------------------------------------------

export const mswForLibrogamePlaySessionMultiBook = [
  http.get(`${API_BASE}/api/v1/gamebook/campaigns/:id`, () => {
    return HttpResponse.json(MOCK_LIBROGAME_PLAY_SESSION_CAMPAIGN, { status: 200 });
  }),
  http.get(`${API_BASE}/api/v1/gamebook/books`, () => {
    return HttpResponse.json([MOCK_GAME_BOOK_STORY, MOCK_GAME_BOOK_ENCOUNTER], { status: 200 });
  }),
  http.put(`${API_BASE}/api/v1/gamebook/campaigns/:id/progress`, () => {
    return HttpResponse.json(MOCK_LIBROGAME_PLAY_SESSION_CAMPAIGN, { status: 200 });
  }),
];

// ---------------------------------------------------------------------------
// MSW rich-history handler (Frame 04 — 10-entry history list)
// ---------------------------------------------------------------------------

export const mswForLibrogamePlaySessionRichHistory = [
  http.get(`${API_BASE}/api/v1/gamebook/campaigns/:id`, () => {
    return HttpResponse.json(MOCK_LIBROGAME_PLAY_SESSION_CAMPAIGN_RICH_HISTORY, { status: 200 });
  }),
  http.get(`${API_BASE}/api/v1/gamebook/books`, () => {
    return HttpResponse.json([MOCK_GAME_BOOK_STORY], { status: 200 });
  }),
  http.put(`${API_BASE}/api/v1/gamebook/campaigns/:id/progress`, () => {
    return HttpResponse.json(MOCK_LIBROGAME_PLAY_SESSION_CAMPAIGN_RICH_HISTORY, { status: 200 });
  }),
];
