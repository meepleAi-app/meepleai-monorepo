/**
 * @mockup admin-mockups/design_files/librogame-runthrough-game-onboarding.html
 *
 * GameOnboarding story — DS-17 Phase D-2 Task 10 (sub-issue #2174).
 *
 * There is NO real component for the prereq-stepper onboarding flow (confirmed
 * during planning). This story uses a Template K presentational mock
 * `GameOnboardingMock` that renders the full stepper UI from the mockup,
 * driven by a single `state` prop.
 *
 * The stepper guides the user through three prerequisite steps before the
 * "Avvia libro game" CTA is unlocked:
 *   1. PDF upload (regolamento + Press Start)
 *   2. KB indexing (chunks + embeddings + pgvector)
 *   3. Tutor agent creation (linked to the KB)
 *
 * 4 mockup states → 4 story frames (all Template K forward-refactor):
 *
 *   Frame01_PrereqMissing  — 0/3 step done; Step 1 active, "Carica PDF" CTA
 *   Frame02_PdfUploading   — Step 1 active; 2 PDF uploads in progress (1 done, 1 at 62%)
 *   Frame03_KbIndexing     — Step 1 done; Step 2 active with sub-steps + ETA ~70 s
 *   Frame04_Ready          — all 3 done; "Avvia libro game" primary CTA unlocked
 *
 * State-driving strategy (STATIC — `state` prop, no play functions):
 *   Each frame passes a different `state` value to `GameOnboardingMock`.
 *   The mock renders the correct stepper content statically on first paint.
 *   The snapshot harness (`apps/web/e2e/storybook/librogame.snapshot.spec.ts`)
 *   does `goto + waitForTimeout(2000) + screenshot` — it never runs play.
 *
 * BGG guard: no BGG refs in this story or mock (#2123). The mock uses the
 * internal catalog framing (no BoardGameGeek assets).
 *
 * Refs: spec docs/superpowers/specs/2026-06-22-ds-17-phase-d-2-librogame-storybook-migration-design.md,
 *       umbrella #2063, sub-issue #2174 (Phase D-2).
 */

import {
  GameOnboardingMock,
  type GameOnboardingMockState,
} from '@/__tests__/fixtures/mockup-pilots/librogame/_mocks/GameOnboardingMock';

import type { Meta, StoryObj } from '@storybook/react';

// ---------------------------------------------------------------------------
// Meta — component IS the mock (Template K: no real component exists)
// ---------------------------------------------------------------------------

const meta: Meta<typeof GameOnboardingMock> = {
  title: 'Pages/Librogame/Game Onboarding',
  component: GameOnboardingMock,
  parameters: {
    layout: 'fullscreen',
    docs: {
      description: {
        component:
          'Pixel-faithful matrix di librogame-runthrough-game-onboarding.html 4 stati. ' +
          'Template K forward-refactor: nessun componente reale per il prereq-stepper. ' +
          'GameOnboardingMock guida attraverso 3 step (PDF upload → KB indexing → Agente Tutor) ' +
          'prima di sbloccare la CTA "Avvia libro game". ' +
          'Sostituire con il vero GameOnboardingPanel quando implementato.',
      },
    },
    // fidelity.json documents the canonical mapping explicitly: default=prereq-missing,
    // loading=pdf-uploading+kb-indexing, empty=ready. State names are mockup-stage
    // literals (`'prereq-missing'`, `'pdf-uploading'`, etc.), not canonical literals —
    // lint:storybook-states heuristic can't see them (#2342 Task 4 bonifica).
    canonicalStates: ['default', 'loading', 'empty'],
  },
  args: {
    state: 'prereq-missing' as GameOnboardingMockState,
    gameTitle: 'Eldoria',
    gameMeta: 'Mythic Games · 2024',
    onUploadPdf: undefined,
    onStartSession: undefined,
  },
  argTypes: {
    state: {
      control: { type: 'select' },
      options: [
        'prereq-missing',
        'pdf-uploading',
        'kb-indexing',
        'ready',
      ] satisfies GameOnboardingMockState[],
      description: 'Quale stato del mockup renderizzare. Drives the stepper content statically.',
    },
    gameTitle: {
      control: 'text',
      description: 'Titolo del libro game mostrato nel hero cover.',
    },
    gameMeta: {
      control: 'text',
      description: 'Publisher + anno mostrati sotto il titolo.',
    },
    onUploadPdf: {
      action: 'onUploadPdf',
      description: 'Callback per la CTA "Carica PDF" (stato prereq-missing).',
    },
    onStartSession: {
      action: 'onStartSession',
      description: 'Callback per il pulsante primario "Avvia libro game" (stato ready).',
    },
  },
};
export default meta;

type Story = StoryObj<typeof GameOnboardingMock>;

// ── Frame 01 · Prereq Missing — 0/3 step done, step 1 active (mockup Stato A) ──

export const Frame01_PrereqMissing: Story = {
  name: '01 · Prereq Missing — 0/3 step, "Carica PDF" attivo (mockup Stato A)',
  args: {
    state: 'prereq-missing',
  },
  parameters: {
    docs: {
      description: {
        story:
          'Mockup Stato A (prereq-missing): Aaron arriva su /library/{gameId} di Eldoria. ' +
          'Zero PDF caricati, KB vuota, nessun agente associato. ' +
          'Il panel stepper sostituisce la CTA "Avvia libro game". ' +
          'Step 1 (PDF) attivo con CTA "Carica PDF →". ' +
          'Step 2 (KB) e Step 3 (Agente) in stato todo. ' +
          'Counter: 0 / 3.',
      },
    },
  },
};

// ── Frame 02 · PDF Uploading — upload concorrente 2 PDF (mockup Stato B) ─────

export const Frame02_PdfUploading: Story = {
  name: '02 · PDF Uploading — 2 PDF concorrenti, Rules al 62% (mockup Stato B)',
  args: {
    state: 'pdf-uploading',
  },
  parameters: {
    docs: {
      description: {
        story:
          'Mockup Stato B (pdf-uploading): Aaron ha cliccato "Carica PDF". ' +
          'Upload concorrente di 2 PDF: ' +
          '"Eldoria Press Start ENG.pdf" (36 MB · completato ✓) e ' +
          '"Eldoria Rules ENG.pdf" (101 MB · 62%). ' +
          "EXIF GPS rimosso client-side prima dell'upload. " +
          'Step 1 attivo · Step 2 (KB) e Step 3 (Agente) todo in attesa. ' +
          'Counter: 0 / 3 · in corso.',
      },
    },
  },
};

// ── Frame 03 · KB Indexing — embedding e5-base 43%, ETA ~70 s (mockup Stato C) ──

export const Frame03_KbIndexing: Story = {
  name: '03 · KB Indexing — embedding 43%, pgvector in attesa, ETA ~70 s (mockup Stato C)',
  args: {
    state: 'kb-indexing',
  },
  parameters: {
    docs: {
      description: {
        story:
          'Mockup Stato C (kb-indexing): PDF completati (137 MB), background job KB in corso. ' +
          'Sub-step breakdown: Parsing PDF ✓ (272 pag) → Chunking ✓ (1.842 chunks) → ' +
          'Embedding e5-base ⏳ (792 / 1.842, 43%) → pgvector HNSW (in attesa). ' +
          'ETA ~70 s · job in background, puoi chiudere la pagina. ' +
          'Step 1 done · Step 2 attivo · Step 3 (Agente) todo. ' +
          'Counter: 1 / 3 · in corso.',
      },
    },
  },
};

// ── Frame 04 · Ready — tutti e 3 i step done, CTA session attiva (mockup Stato D) ──

export const Frame04_Ready: Story = {
  name: '04 · Ready — 3/3 step ok, "Avvia libro game" CTA attivo (mockup Stato D)',
  args: {
    state: 'ready',
  },
  parameters: {
    docs: {
      description: {
        story:
          'Mockup Stato D (ready): tutti i prerequisiti completati. ' +
          '137 MB PDF indicizzati (1.842 chunks), agente "Arbitro Eldoria" creato e linkato. ' +
          'CTA primary session-colored "🎲 Avvia libro game" in cima al panel. ' +
          'Ready meta-chips: "✓ 137 MB indicizzati", "✓ 1.842 chunks", "✓ Arbitro Eldoria". ' +
          'Stepper mostra 3/3 step completati sotto la CTA. ' +
          'Counter: 3 / 3.',
      },
    },
  },
};
