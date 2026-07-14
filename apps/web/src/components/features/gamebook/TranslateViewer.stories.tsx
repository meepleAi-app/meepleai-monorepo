/**
 * @mockup admin-mockups/design_files/librogame-runthrough-translate-viewer.html
 *
 * TranslateViewer argTypes matrix story — DS-17 Phase D-2 (sub-issue #2174).
 *
 * State-driving strategy (STATIC — test-seam props + MSW, no play functions):
 *   TranslateViewer manages 5 async hooks (useGameBooks, usePhotoUpload,
 *   useSegmentPhoto, useTranslateSegmentSSE, useReaderMode). To render each
 *   mockup state on first paint without any user interaction, we use:
 *
 *   1. Test-seam props added to the real component:
 *      `_initialPhase`          → pre-seeds FSM phase
 *      `_initialArtifact`       → pre-seeds photo artifact (segments_ready+)
 *      `_initialActiveSegment`  → pre-seeds active segment (translating+)
 *      `_initialSseState`       → pre-seeds SSE text/confidence (translating/translated)
 *      `_initialModalOpen`      → pre-seeds lang-override modal open (Frame11)
 *
 *   2. MSW `GET /api/v1/gamebook/books` → single narrative book (auto-selects,
 *      camera button enabled). Required by every frame.
 *
 *   3. localStorage decorator for reader-mode (Frame08): sets
 *      `reader-mode-enabled=true` before story renders so useReaderMode
 *      picks it up via its useEffect.
 *
 *   4. ManualInputView (real component, Frame12): renders the ?mode=manual UI.
 *      Also MSW-driven for /books endpoint.
 *
 *   5. TranslateViewerWakeLockMock (Template K, Frame09): wake-lock badge is
 *      forward-refactor — no real component branch exists yet.
 *
 * 13 mockup states → 12 story frames:
 *   Frame01_Idle           — idle (mockup state A)
 *   Frame02_Segmenting     — segmenting / ocr loading (mockup state B)
 *   Frame03_SegmentsList   — segments_ready + 3 segments (mockup state C)
 *   Frame04_Translating    — translating + partial text (mockup state D)
 *   Frame05_Translated     — translated + complete text + glossary (mockup states E + J merged)
 *   Frame06_LowConfidence  — segments_ready + lang tier=low, picker blocked (mockup state F)
 *   Frame07_Loading4step   — uploading / 4-step skeleton step 1 (mockup state G)
 *   Frame08_ReaderMode     — translated + reader-mode=true (mockup state H)
 *   Frame09_WakeLock       — forward-refactor mock: wake-lock badge (mockup state I)
 *   Frame10_LangBadgeHigh  — segments_ready + lang tier=high badge (mockup state K)
 *   Frame11_LangOverrideModal — segments_ready + modal open (mockup state L)
 *   Frame12_ManualInputMode — ManualInputView real component (mockup state M)
 *
 * Merged/skipped log:
 *   - State J (contrasto-aaa) merged into Frame05: AAA contrast is `--c-text-high-contrast`
 *     CSS applied inside TranslationPane on every translated render, no separate FSM branch.
 *
 * BGG guard: no BGG refs in this file (#2123).
 *
 * Refs: spec docs/superpowers/specs/2026-06-22-ds-17-phase-d-2-librogame-storybook-migration-design.md,
 *       umbrella #2063, sub-issue #2174 (Phase D-2).
 */

import { TranslateViewerWakeLockMock } from '@/__tests__/fixtures/mockup-pilots/librogame/_mocks/TranslateViewerMock';
import {
  MOCK_ARTIFACT_TV,
  MOCK_APPLIED_TERMS,
  MOCK_CAMPAIGN_ID_TV,
  MOCK_GAME_ID_TV,
  MOCK_SEGMENT_147,
  MOCK_TRANSLATION_COMPLETE,
  MOCK_TRANSLATION_PARTIAL,
  mswBooksHandler,
  mswManualInputHandlers,
} from '@/__tests__/fixtures/mockup-pilots/librogame/librogame-translate-viewer';
import { ManualInputView } from '@/components/features/gamebook/ManualInputView';

import { TranslateViewer } from './TranslateViewer';

import type { Decorator } from '@storybook/react';
import type { Meta, StoryObj } from '@storybook/react';

// ---------------------------------------------------------------------------
// Decorator: seeds localStorage reader-mode-enabled=true before Frame08 renders.
// Necessary because useReaderMode reads localStorage in a useEffect (SSR-safe).
// ---------------------------------------------------------------------------
const readerModeDecorator: Decorator = Story => {
  if (typeof window !== 'undefined') {
    try {
      window.localStorage.setItem('reader-mode-enabled', 'true');
    } catch {
      // ignore quota / security errors in Storybook sandbox
    }
  }
  return <Story />;
};

// ---------------------------------------------------------------------------
// Decorator: clears reader-mode from localStorage so non-reader-mode frames
// always start with isReaderMode=false.
// ---------------------------------------------------------------------------
const readerModeClearDecorator: Decorator = Story => {
  if (typeof window !== 'undefined') {
    try {
      window.localStorage.removeItem('reader-mode-enabled');
    } catch {
      // ignore
    }
  }
  return <Story />;
};

// ---------------------------------------------------------------------------
// Meta
// ---------------------------------------------------------------------------

const meta: Meta<typeof TranslateViewer> = {
  title: 'Pages/Librogame/Translate Viewer',
  component: TranslateViewer,
  parameters: {
    layout: 'fullscreen',
    nextjs: {
      appDirectory: true,
      navigation: {
        pathname: `/library/${MOCK_GAME_ID_TV}/play/${MOCK_CAMPAIGN_ID_TV}/translate`,
        query: {},
      },
    },
    docs: {
      description: {
        component:
          'Pixel-faithful matrix di librogame-runthrough-translate-viewer.html 13 stati (12 frames). ' +
          'Usa test-seam props (_initialPhase/_initialArtifact/_initialActiveSegment/_initialSseState/_initialModalOpen) ' +
          'per guidare ogni stato FSM STATICAMENTE senza interaction. ' +
          'MSW fornisce GET /books per ogni frame. ' +
          'Frame08 usa un localStorage decorator per reader-mode. ' +
          'Frame09 è un Template K forward-refactor mock (wake-lock non implementato). ' +
          'Frame12 usa il vero ManualInputView (?mode=manual route).',
      },
    },
    // Frame01_Idle covers canonical `default`, Frame02_Segmenting covers canonical
    // `loading`, Frame04_Translating covers canonical `sse` (partial streaming text)
    // — all driven via `_initialPhase` test-seam prop values (idle/segmenting/
    // translating), not `mswForState`-style string literals — lint:storybook-states
    // heuristic can't see them (#2342 Task 4 bonifica).
    canonicalStates: ['default', 'loading', 'sse'],
  },
  decorators: [readerModeClearDecorator],
  args: {
    campaignId: MOCK_CAMPAIGN_ID_TV,
    gameRef: { id: MOCK_GAME_ID_TV, kind: 0 },
  },
  argTypes: {
    campaignId: {
      control: 'text',
      description: 'Campaign UUID — used in MSW handlers.',
    },
    gameRef: {
      control: 'object',
      description: 'GameRef {id, kind} for useGameBooks. kind=0 = Shared.',
    },
    _initialPhase: {
      control: { type: 'select' },
      options: ['idle', 'uploading', 'segmenting', 'segments_ready', 'translating', 'translated'],
      description: '[Test-seam] Pre-seed FSM phase — for stories only.',
    },
  },
};
export default meta;

type Story = StoryObj<typeof TranslateViewer>;

// ── Frame 01 · Idle — camera CTA, empty state (mockup state A) ───────────────

export const Frame01_Idle: Story = {
  name: '01 · Idle — camera CTA, empty state (mockup A)',
  args: {
    _initialPhase: 'idle',
  },
  parameters: {
    msw: { handlers: mswBooksHandler },
    docs: {
      description: {
        story:
          'Mockup stato A: camera-ready. Pulsante "Scatta o scegli foto" abilitato ' +
          '(singolo libro narrativo auto-selezionato via MSW). ' +
          'CTA "Digita manualmente" visibile (empty_state entry point). ' +
          'Kebab ⋮ visibile. Nessun artefatto, nessuna traduzione.',
      },
    },
  },
};

// ── Frame 02 · Segmenting — OCR loading (mockup state B) ─────────────────────

export const Frame02_Segmenting: Story = {
  name: '02 · Segmenting — OCR loading, abort visibile (mockup B)',
  args: {
    _initialPhase: 'segmenting',
  },
  parameters: {
    msw: { handlers: mswBooksHandler },
    docs: {
      description: {
        story:
          'Mockup stato B: segmenting (uiStep=ocr). ' +
          'LoadingSkeleton mostra "Sto leggendo il libro…" con 3 skeleton bar. ' +
          'AbortButton visibile (uiStep=ocr è abortable). ' +
          'Bottone camera mostra "In corso…" disabled.',
      },
    },
  },
};

// ── Frame 03 · Segments List — 3 segments (mockup state C) ────────────────────

export const Frame03_SegmentsList: Story = {
  name: '03 · Segments List — §146/§147/§148, picker abilitato (mockup C)',
  args: {
    _initialPhase: 'segments_ready',
    _initialArtifact: MOCK_ARTIFACT_TV,
  },
  parameters: {
    msw: { handlers: mswBooksHandler },
    docs: {
      description: {
        story:
          'Mockup stato C: segments-list. SegmentPicker mostra i 3 paragrafi ' +
          '§146/§147/§148 con testo EN e bottone "Traduci". ' +
          'Nessun lang badge (tier=none, nessun SSE ancora). ' +
          'Picker abilitato (segmentBlockedByLang=false).',
      },
    },
  },
};

// ── Frame 04 · Translating — partial SSE text + progress (mockup state D) ─────

export const Frame04_Translating: Story = {
  name: '04 · Translating — SSE parziale §147, abort visibile (mockup D)',
  args: {
    _initialPhase: 'translating',
    _initialArtifact: MOCK_ARTIFACT_TV,
    _initialActiveSegment: MOCK_SEGMENT_147,
    _initialSseState: {
      partialText: MOCK_TRANSLATION_PARTIAL,
      isComplete: false,
      appliedTerms: [],
    },
  },
  parameters: {
    msw: { handlers: mswBooksHandler },
    docs: {
      description: {
        story:
          'Mockup stato D: translating. TranslationPane mostra testo parziale ' +
          '"Una sentinella si fa avanti…" con cursor animato (CSS). ' +
          'LoadingSkeleton visibile (uiStep=translating). ' +
          'AbortButton visibile. Titolo §147 sopra il testo.',
      },
    },
  },
};

// ── Frame 05 · Translated — complete text + glossary terms (mockup states E + J) ─

export const Frame05_Translated: Story = {
  name: '05 · Translated — §147 completo, glossary terms, AAA contrast (mockup E+J)',
  args: {
    _initialPhase: 'translated',
    _initialArtifact: MOCK_ARTIFACT_TV,
    _initialActiveSegment: MOCK_SEGMENT_147,
    _initialSseState: {
      partialText: MOCK_TRANSLATION_COMPLETE,
      isComplete: true,
      appliedTerms: MOCK_APPLIED_TERMS,
      detectedSourceLang: 'EN',
      langDetectionConfidence: 0.93,
    },
  },
  parameters: {
    msw: { handlers: mswBooksHandler },
    docs: {
      description: {
        story:
          'Mockup stati E + J (merged): translated + contrasto-aaa. ' +
          'TranslationPane mostra la traduzione completa di §147 con ✓ completata. ' +
          'Termini glossario applicati: Nanolith, sigillo del Consiglio, sentinella. ' +
          'Testo usa --c-text-high-contrast (≥7:1 AAA contrast). ' +
          'LangBadge tier=high "Sorgente: EN" visibile. ' +
          'SegmentPicker ancora visibile (phase=translated shows the picker). ' +
          'Stato J (contrasto-aaa) merged: stessa fase, stessa CSS, nessun ramo FSM separato.',
      },
    },
  },
};

// ── Frame 06 · Low Confidence — lang tier=low, picker blocked (mockup state F) ─

export const Frame06_LowConfidence: Story = {
  name: '06 · Low Confidence — lang tier=low, picker bloccato (mockup F)',
  args: {
    _initialPhase: 'segments_ready',
    _initialArtifact: MOCK_ARTIFACT_TV,
    _initialSseState: {
      partialText: '',
      isComplete: false,
      appliedTerms: [],
      detectedSourceLang: null,
      langDetectionConfidence: 0.31,
    },
  },
  parameters: {
    msw: { handlers: mswBooksHandler },
    docs: {
      description: {
        story:
          'Mockup stato F: low-confidence. ' +
          'LangBadge tier=low mostra "Sorgente richiesta" (rose). ' +
          'SegmentPicker bloccato: hint "Conferma la sorgente per tradurre." visibile. ' +
          'Modal auto-open per tier=low è soppresso tenendo isComplete:false — ' +
          'il useEffect (TranslateViewer.tsx:212-224) esce early alla prima guard ' +
          '`if (!sse.isComplete || !artifact?.id) return` prima di aprire LangOverrideModal. ' +
          'Il badge e il picker-blocked si attivano da phase=segments_ready + tier=low, ' +
          'indipendentemente da isComplete.',
      },
    },
  },
};

// ── Frame 07 · Loading 4-step — uploading step 1 (mockup state G) ─────────────

export const Frame07_Loading4step: Story = {
  name: '07 · Loading 4-step — uploading step 1 (mockup G)',
  args: {
    _initialPhase: 'uploading',
  },
  parameters: {
    msw: { handlers: mswBooksHandler },
    docs: {
      description: {
        story:
          'Mockup stato G: loading-4step, step 1 "Caricamento foto…". ' +
          'LoadingSkeleton visibile con uiStep=uploading. ' +
          'AbortButton NON visibile (uploading è non-abortable per DEC-3). ' +
          'Bottone camera disabilitato (phase=uploading → isBusy=true).',
      },
    },
  },
};

// ── Frame 08 · Reader Mode — translated + reader-mode=true (mockup state H) ───

export const Frame08_ReaderMode: Story = {
  name: '08 · Reader Mode — traduzione §147, reader-mode=true (mockup H)',
  decorators: [readerModeDecorator],
  args: {
    _initialPhase: 'translated',
    _initialArtifact: MOCK_ARTIFACT_TV,
    _initialActiveSegment: MOCK_SEGMENT_147,
    _initialSseState: {
      partialText: MOCK_TRANSLATION_COMPLETE,
      isComplete: true,
      appliedTerms: MOCK_APPLIED_TERMS,
      detectedSourceLang: 'EN',
      langDetectionConfidence: 0.93,
    },
  },
  parameters: {
    msw: { handlers: mswBooksHandler },
    docs: {
      description: {
        story:
          'Mockup stato H: reader-mode attivo. ' +
          'localStorage decorator pre-setta reader-mode-enabled=true prima del render. ' +
          'data-reader-mode="true" sul wrapper root (useReaderMode lo legge via useEffect). ' +
          'CSS reader-mode-content appllicato a LoadingSkeleton e TranslationPane. ' +
          'ReaderModeToggle mostra stato "attivo" (toggle button aria-label).',
      },
    },
  },
};

// ── Frame 09 · Wake Lock — forward-refactor mock (mockup state I) ─────────────

export const Frame09_WakeLock: Story = {
  name: '09 · Wake Lock — badge sticky (forward-refactor mock, mockup I)',
  render: () => <TranslateViewerWakeLockMock status="active" />,
  parameters: {
    docs: {
      description: {
        story:
          'Mockup stato I: wake-lock badge. ' +
          'Template K (forward-refactor): Wake Lock API non è implementata nel vero ' +
          'TranslateViewer — questo è un mock presentazionale del badge "☀️ Schermo attivo". ' +
          'Varianti: active (verde) / disabled (amber) / not-supported (muted). ' +
          'Sostituire con il vero WakeLockBadge primitive quando implementato.',
      },
    },
  },
};

// ── Frame 10 · Lang Badge High — segments_ready + high confidence badge (mockup state K) ─

export const Frame10_LangBadgeHigh: Story = {
  name: '10 · Lang Badge High — "Sorgente: EN" tier=high (mockup K)',
  args: {
    _initialPhase: 'segments_ready',
    _initialArtifact: MOCK_ARTIFACT_TV,
    _initialSseState: {
      partialText: '',
      isComplete: true,
      appliedTerms: [],
      detectedSourceLang: 'EN',
      langDetectionConfidence: 0.93,
    },
  },
  parameters: {
    msw: { handlers: mswBooksHandler },
    docs: {
      description: {
        story:
          'Mockup stato K: lang-detection-badge, variant tier=high. ' +
          'LangBadge mostra "Sorgente: EN" (green emerald badge). ' +
          'Picker abilitato (segmentBlockedByLang=false per tier=high). ' +
          'Varianti confidence >0.8=high / 0.5–0.8=medium / <0.5=low — questo mostra high (0.93).',
      },
    },
  },
};

// ── Frame 11 · Lang Override Modal — modal open (mockup state L) ─────────────

export const Frame11_LangOverrideModal: Story = {
  name: '11 · Lang Override Modal — 5 lingue EN/FR/DE/ES/IT, EN preselezionato (mockup L)',
  args: {
    _initialPhase: 'segments_ready',
    _initialArtifact: MOCK_ARTIFACT_TV,
    _initialSseState: {
      partialText: '',
      isComplete: true,
      appliedTerms: [],
      detectedSourceLang: 'EN',
      langDetectionConfidence: 0.93,
    },
    _initialModalOpen: true,
  },
  parameters: {
    msw: { handlers: mswBooksHandler },
    docs: {
      description: {
        story:
          'Mockup stato L: lang-override-modal aperto. ' +
          '_initialModalOpen=true pre-setta modalState.open=true (mode=manual). ' +
          'LangOverrideModal mostra radio group 5 lingue EN·FR·DE·ES·IT. ' +
          'EN preselezionato (detectedSourceLang=EN, confirmedLang=null). ' +
          'CTA "Conferma e ritraduci" abilitata. ' +
          'Dismissable=true → bottone "Chiudi" visibile.',
      },
    },
  },
};

// ── Frame 12 · Manual Input Mode — ManualInputView real component (mockup state M) ─

export const Frame12_ManualInputMode: Story = {
  name: '12 · Manual Input Mode — ManualInputView (?mode=manual) (mockup M)',
  render: () => (
    <ManualInputView campaignId={MOCK_CAMPAIGN_ID_TV} gameRef={{ id: MOCK_GAME_ID_TV, kind: 0 }} />
  ),
  parameters: {
    msw: { handlers: mswManualInputHandlers },
    nextjs: {
      appDirectory: true,
      navigation: {
        pathname: `/library/${MOCK_GAME_ID_TV}/play/${MOCK_CAMPAIGN_ID_TV}/translate`,
        query: { mode: 'manual' },
      },
    },
    docs: {
      description: {
        story:
          'Mockup stato M: manual-input-mode. ' +
          'Usa il vero ManualInputView (route ?mode=manual, vedi _content.tsx). ' +
          'Header "Inserimento manuale" + badge MANUAL + dropdown lingua IT. ' +
          'Book chip "📖 Storybook — Nanolith 🔒" (MSW /books). ' +
          'Textarea multiline max 2000 char + counter. ' +
          'CTA "Traduci →" (disabled: textarea vuota). ' +
          'Triple-entry: questo è il fallback per photo-blur o ?mode=manual diretto.',
      },
    },
  },
};
