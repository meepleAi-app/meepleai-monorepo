/**
 * Fixtures + MSW handlers for TranslateViewer story — DS-17 Phase D-2 (sub-issue #2174).
 *
 * State-driving strategy:
 *   TranslateViewer has 6 real Phase states (idle / uploading / segmenting /
 *   segments_ready / translating / translated) driven by 5 async hooks. To render
 *   each state STATICALLY (snapshot harness never runs `play`), we use a combination
 *   of:
 *   - Test-seam props (`_initialPhase`, `_initialArtifact`, `_initialActiveSegment`,
 *     `_initialSseState`, `_initialModalOpen`) added to the real component.
 *   - MSW for `GET /api/v1/gamebook/books` so `useGameBooks` returns a book list
 *     instead of staying in loading state (required by every frame).
 *   - `ManualInputView` (real component, also MSW-driven) for the manual-input frame.
 *
 * 13 mockup states → 12 story frames (states J contrasto-aaa merged into Frame05
 *   since AAA contrast is pure CSS applied to the same `translated` phase, no
 *   separate component branch exists):
 *
 *   Frame01_Idle           — Phase=idle, idle camera CTA (mockup state A)
 *   Frame02_Segmenting     — Phase=segmenting, OCR loading (mockup state B)
 *   Frame03_SegmentsList   — Phase=segments_ready + artifact, segment picker (mockup state C)
 *   Frame04_Translating    — Phase=translating + partial text SSE (mockup state D)
 *   Frame05_Translated     — Phase=translated + complete translation + glossary terms (mockup states E + J)
 *   Frame06_LowConfidence  — Phase=segments_ready + lang tier=low, picker blocked (mockup state F)
 *   Frame07_Loading4step   — Phase=uploading, 4-step skeleton step 1 (mockup state G)
 *   Frame08_ReaderMode     — Phase=translated, data-reader-mode=true via decorator (mockup state H)
 *   Frame09_WakeLock       — Template K mock (forward-refactor): wake-lock badge (mockup state I)
 *   Frame10_LangBadgeHigh  — Phase=segments_ready + lang tier=high badge (mockup state K)
 *   Frame11_LangOverrideModal — Phase=segments_ready + modal open (mockup state L)
 *   Frame12_ManualInputMode — ManualInputView (real component, ?mode=manual route) (mockup state M)
 *
 * Merged/skipped:
 *   - State J (contrasto-aaa) merged into Frame05: AAA contrast is `--c-text-high-contrast`
 *     applied to TranslationPane regardless of phase, no separate FSM branch.
 *
 * MSW required:
 *   - GET /api/v1/gamebook/books → [MOCK_BOOK_STORY] (single book auto-selects, camera enabled)
 *   Every frame includes this handler.
 *
 * @mockup admin-mockups/design_files/librogame-runthrough-translate-viewer.html
 * Refs: umbrella #2063, sub-issue #2174 (Phase D-2).
 */

import { http, HttpResponse } from 'msw';

import type { GamebookPhotoArtifact, GamebookSegment } from '@/lib/api/gamebook-photos';

const API_BASE = process.env.NEXT_PUBLIC_API_BASE || 'http://localhost:8080';

// ---------------------------------------------------------------------------
// Constants — valid UUIDs (Zod .uuid() enforced on artifact/book DTOs)
// ---------------------------------------------------------------------------

export const MOCK_CAMPAIGN_ID_TV = '00000000-0000-0000-0000-000000000020';
export const MOCK_GAME_ID_TV = '00000000-0000-0000-0000-000000000021';
export const MOCK_BOOK_ID_STORY_TV = '00000000-0000-0000-0000-000000000022';
export const MOCK_PHOTO_ID_TV = '00000000-0000-0000-0000-000000000023';
export const MOCK_ARTIFACT_ID_TV = '00000000-0000-0000-0000-000000000024';

// ---------------------------------------------------------------------------
// GameBook DTO (single narrative book → auto-select, camera enabled)
// ---------------------------------------------------------------------------

export const MOCK_GAME_BOOK_STORY_TV = {
  id: MOCK_BOOK_ID_STORY_TV,
  gameRefId: MOCK_GAME_ID_TV,
  gameRefKind: 0,
  ownerUserId: null,
  displayName: 'Storybook — Nanolith',
  roles: 4, // Narrative
  paragraphScheme: 1,
  language: 'en',
  sequentialRead: false,
  kbSourceDocId: null,
  physicalOnly: false,
  createdAt: '2026-05-10T18:00:00Z',
} as const;

// ---------------------------------------------------------------------------
// Photo artifact with 3 segments (§146 / §147 / §148 from mockup narrative)
// ---------------------------------------------------------------------------

export const MOCK_SEGMENT_146: GamebookSegment = {
  paragraphNumber: 146,
  sourceText:
    '"The runes of Ardenel glow faintly as you approach the eastern gate. The Guardians\' ' +
    'ranks tighten. You must choose: fight, parley, or withdraw."',
  boundingBox: null,
};

export const MOCK_SEGMENT_147: GamebookSegment = {
  paragraphNumber: 147,
  sourceText:
    '"A sentinel steps forward, torch in hand. His armour bears the sigil of the Nanolith ' +
    'Council — three interlocked rings. \\"None shall pass without the Council seal.\\" ' +
    'Turn to §218 if you bear the seal, or fight at §148."',
  boundingBox: null,
};

export const MOCK_SEGMENT_148: GamebookSegment = {
  paragraphNumber: 148,
  sourceText:
    '"Combat begins. The sentinels draw blades. Roll 2d6: on 9+ you break through ' +
    'and advance to §155. On 8 or less, you are captured — turn to §200."',
  boundingBox: null,
};

export const MOCK_ARTIFACT_TV: GamebookPhotoArtifact = {
  id: MOCK_ARTIFACT_ID_TV,
  campaignId: MOCK_CAMPAIGN_ID_TV,
  status: 'Segmented',
  ocrFullText: null,
  segments: [MOCK_SEGMENT_146, MOCK_SEGMENT_147, MOCK_SEGMENT_148],
  failureReason: null,
  createdAt: '2026-06-22T20:00:00Z',
  expiresAt: '2026-06-23T20:00:00Z',
};

// ---------------------------------------------------------------------------
// Translation text fixture (§147 translated to Italian)
// ---------------------------------------------------------------------------

export const MOCK_TRANSLATION_PARTIAL =
  'Una sentinella si fa avanti, torcia in mano. La sua armatura porta il sigillo del';

export const MOCK_TRANSLATION_COMPLETE =
  'Una sentinella si fa avanti, torcia in mano. La sua armatura porta il sigillo del ' +
  'Consiglio di Nanolith — tre anelli intrecciati. "Nessuno passa senza il sigillo del Consiglio." ' +
  'Vai al §218 se porti il sigillo, oppure combatti al §148.';

export const MOCK_APPLIED_TERMS = ['Nanolith', 'sigillo del Consiglio', 'sentinella'];

// ---------------------------------------------------------------------------
// Base MSW handler — single narrative book for all frames
// ---------------------------------------------------------------------------

export const mswBooksHandler = [
  http.get(`${API_BASE}/api/v1/gamebook/books`, () => {
    return HttpResponse.json([MOCK_GAME_BOOK_STORY_TV], { status: 200 });
  }),
];

// ---------------------------------------------------------------------------
// Manual-input frame MSW handler (ManualInputView uses same /books endpoint)
// ---------------------------------------------------------------------------

export const mswManualInputHandlers = [
  http.get(`${API_BASE}/api/v1/gamebook/books`, () => {
    return HttpResponse.json([MOCK_GAME_BOOK_STORY_TV], { status: 200 });
  }),
  http.post(`${API_BASE}/api/v1/gamebook/campaigns/:campaignId/translate/text`, async () => {
    return new HttpResponse('data: {"delta":"Testo tradotto","isComplete":true}\n\n', {
      status: 200,
      headers: { 'Content-Type': 'text/event-stream' },
    });
  }),
];
