/**
 * @mockup admin-mockups/design_files/librogame-runthrough-error-states.html
 *
 * ErrorStates story — DS-17 Phase D-2 Task 13 (sub-issue #2174).
 *
 * There is NO real standalone component for the error banner system as of DS-17
 * Phase D-2 (2026-06-22). Error states are distributed across hooks and error
 * boundaries inside the librogame play shell (GamebookPlayShell, OCR pipeline,
 * translation pipeline, chat stream handler) and are not exposed independently.
 * This story uses a Template K presentational mock `ErrorStatesMock` that
 * renders each error banner faithfully from the mockup spec, driven by a single
 * `variant` prop.
 *
 * The error banner system enforces 3 invariants for every state:
 *   (a) Plain-language explanation of what went wrong (Aaron-friendly copy)
 *   (b) Cost transparency: explicit "Non addebitato" or partial-cost note
 *   (c) ≥ 2 non-destructive recovery actions — NEVER a dead-end
 *   (d) Dogfood telemetry link for internal feedback loop
 *
 * 10 mockup states → 10 story frames (all Template K forward-refactor):
 *
 *   Frame01_StreamTimeout     — SSE stream stalled > 30s, warn banner inline in chat
 *   Frame02_OcrFail           — OCR confidence 0.27, photo illegible, danger banner
 *   Frame03_Llm503            — Provider 503, 3 retry strip + offline KB fallback
 *   Frame04_SegmentationFail  — OCR ok (0.88), no § pattern, manual § input
 *   Frame05_OcrLowConf        — Confidence 0.64, yellow highlight regions, warn banner
 *   Frame06_PhotoBlur         — Client-side pre-OCR blur reject, focus score 0.28
 *   Frame07_TranslationTimeout— AI translate aborted > 20s, manual edit fallback
 *   Frame08_SourceLangUnknown — Lang confidence 0.38, 5-option confirm dialog
 *   Frame09_NetworkMidOcr     — Connection lost mid-upload, IndexedDB queue
 *   Frame10_QuotaExhausted    — Daily token quota 100%, bridge to credits overlay
 *
 * State-driving strategy (STATIC — `variant` prop, no play functions):
 *   Each frame passes a different `variant` value to `ErrorStatesMock`.
 *   The mock renders the correct error banner content statically on first paint.
 *   The snapshot harness (`apps/web/e2e/storybook/librogame.snapshot.spec.ts`)
 *   does `goto + waitForTimeout(2000) + screenshot` — it never runs play.
 *
 * BGG guard: no BGG refs in this story or mock (#2123). Internal catalog
 * framing only (game "Eldoria", campaign "Sera con i ragazzi").
 *
 * Refs: spec docs/superpowers/specs/2026-06-22-ds-17-phase-d-2-librogame-storybook-migration-design.md,
 *       umbrella #2063, sub-issue #2174 (Phase D-2).
 */

import {
  ErrorStatesMock,
  type ErrorStatesMockVariant,
} from '@/__tests__/fixtures/mockup-pilots/librogame/_mocks/ErrorStatesMock';

import type { Meta, StoryObj } from '@storybook/react';

// ---------------------------------------------------------------------------
// Meta — component IS the mock (Template K: no real component exists)
// ---------------------------------------------------------------------------

const meta: Meta<typeof ErrorStatesMock> = {
  title: 'Pages/Librogame/Error States',
  component: ErrorStatesMock,
  parameters: {
    layout: 'fullscreen',
    docs: {
      description: {
        component:
          'Pixel-faithful matrix di librogame-runthrough-error-states.html 10 stati. ' +
          'Template K forward-refactor: nessun componente reale per il sistema error-banner. ' +
          "ErrorStatesMock renderizza ogni banner d'errore del play-shell (stream-timeout, " +
          'ocr-fail, llm-503, segmentation-fail, ocr-low-conf, photo-blur, translation-timeout, ' +
          'source-lang-unknown, network-mid-ocr, quota-exhausted). ' +
          'Invarianti: (a) linguaggio Aaron-friendly, (b) cost transparency, ' +
          '(c) ≥2 recovery actions non-distruttive, (d) dogfood telemetry link. ' +
          'Sostituire con i componenti reali quando estratti da GamebookPlayShell + pipeline hooks.',
      },
    },
  },
  args: {
    variant: 'stream-timeout' as ErrorStatesMockVariant,
    sessionName: 'Sera con i ragazzi',
  },
  argTypes: {
    variant: {
      control: { type: 'select' },
      options: [
        'stream-timeout',
        'ocr-fail',
        'llm-503',
        'segmentation-fail',
        'ocr-low-conf',
        'photo-blur',
        'translation-timeout',
        'source-lang-unknown',
        'network-mid-ocr',
        'quota-exhausted',
      ] satisfies ErrorStatesMockVariant[],
      description: 'Quale variante di errore renderizzare. Drives the banner content statically.',
    },
    sessionName: {
      control: 'text',
      description: 'Nome della sessione mostrato nel top bar.',
    },
  },
};
export default meta;

type Story = StoryObj<typeof ErrorStatesMock>;

// ── Frame 01 · Stream Timeout — SSE stalled > 30s, warn banner (mockup Stato A) ──

export const Frame01_StreamTimeout: Story = {
  name: '01 · Stream Timeout — SSE stream stalled 32s, warn banner inline chat (mockup Stato A)',
  args: {
    variant: 'stream-timeout',
  },
  parameters: {
    docs: {
      description: {
        story:
          'Mockup Stato A (stream-timeout): Aaron fa Q&A N2 in-game. SSE stream parte, ' +
          'prime 12 parole arrivano, poi silenzio > 30s. ' +
          'Banner warn-colored sopra le action button (NON modal blocking). ' +
          'Risposta parziale conservata e visibile nel chat bubble. ' +
          'Cost note: stream incompleto = 0 token al costo. ' +
          'Actions: Riprova · Cambia agente · Copia risposta parziale. ' +
          'Debug strip: req-id + model + ttfb + flush.',
      },
    },
  },
};

// ── Frame 02 · OCR Fail — confidence 0.27, photo illegible (mockup Stato B) ───

export const Frame02_OcrFail: Story = {
  name: '02 · OCR Fail — confidence 0.27, foto illeggibile, danger banner (mockup Stato B)',
  args: {
    variant: 'ocr-fail',
  },
  parameters: {
    docs: {
      description: {
        story:
          'Mockup Stato B (ocr-fail): foto mossa / buia / controluce, OCR conf 0.27 < 0.50 soglia. ' +
          'Distinto da low-confidence (0.5-0.7): qui pagina completamente illeggibile. ' +
          'Banner danger-colored. Foto preview blurred per context. ' +
          'Cost note: OCR fallito = 0 token traduzione. ' +
          'Actions: Rifotografa (primary danger) · Numero § manuale · Annulla. ' +
          'Debug strip: OCR conf + soglia + brightness.',
      },
    },
  },
};

// ── Frame 03 · LLM 503 — provider down, 3 retry + offline fallback (mockup Stato C) ──

export const Frame03_Llm503: Story = {
  name: '03 · LLM 503 — provider down, 3 retry strip + offline KB fallback (mockup Stato C)',
  args: {
    variant: 'llm-503',
  },
  parameters: {
    docs: {
      description: {
        story:
          'Mockup Stato C (llm-503): DeepSeek ritorna 503. ' +
          'Strategia: 3 retry exponential backoff (2s · 4s · 8s) → OpenRouter → fallback offline KB. ' +
          'Retry strip live con pip fail/try/todo. ' +
          'Cost note: retry falliti = 0 token. ' +
          'Actions: Annulla retry (primary danger) · Mostra status pubblico. ' +
          'Offline fallback card: glossario campagna + setup tutorial Press Start cached. ' +
          'Debug strip: err 503 + provider chain + incident INC-0234.',
      },
    },
  },
};

// ── Frame 04 · Segmentation Fail — OCR ok, no § detected (mockup Stato D) ───

export const Frame04_SegmentationFail: Story = {
  name: '04 · Segmentation Fail — OCR ok (0.88), nessun § rilevato, manual input (mockup Stato D)',
  args: {
    variant: 'segmentation-fail',
  },
  parameters: {
    docs: {
      description: {
        story:
          'Mockup Stato D (segmentation-fail): OCR confidence alta (0.88) ma il segmenter ' +
          'non trova pattern §\\d+. Tipico per pagine intro/glossario/indice senza § numerati. ' +
          'Banner warn-colored + prominent manual § input con prefisso "§". ' +
          'Cost note: solo OCR addebitato · traduzione ancora non partita. ' +
          'Alt links: È glossario/indice · È un range · Annulla. ' +
          'Debug strip: OCR conf + regex 0 match.',
      },
    },
  },
};

// ── Frame 05 · OCR Low Confidence — conf 0.64, yellow highlight (mockup Stato E) ──

export const Frame05_OcrLowConf: Story = {
  name: '05 · OCR Low Conf — confidence 0.64, regioni dubbie highlight giallo (mockup Stato E)',
  args: {
    variant: 'ocr-low-conf',
  },
  parameters: {
    docs: {
      description: {
        story:
          'Mockup Stato E (ocr-low-conf): confidence aggregata 0.64, soglia 0.70. ' +
          'Halfway tra OCR-fail e OCR-ok: alcune regioni dubbie. ' +
          'OCR page preview con parole incerte evidenziate in giallo (box-shadow warning). ' +
          'Confidence progress bar con marker soglia 0.70. ' +
          'Cost note: solo OCR addebitato · traduzione N3 non ancora partita. ' +
          'Actions: Ritocca foto (primary warn, consigliato) · Procedi comunque · Correggi manualmente.',
      },
    },
  },
};

// ── Frame 06 · Photo Blur — pre-OCR client-side reject, focus 0.28 (mockup Stato F) ──

export const Frame06_PhotoBlur: Story = {
  name: '06 · Photo Blur — pre-OCR client-side reject, focus 0.28 (mockup Stato F)',
  args: {
    variant: 'photo-blur',
  },
  parameters: {
    docs: {
      description: {
        story:
          'Mockup Stato F (photo-blur): autofocus score 0.28 < soglia 0.60 rilevato client-side. ' +
          "Reject in-camera prima dell'upload — zero roundtrip, zero token, zero costo. " +
          'Camera viewfinder blurred + dashed danger border + "FUORI FUOCO" pill. ' +
          'Shutter button disabilitato (opacity-40, aria-disabled). ' +
          'Focus meter con progress bar (fill 28%). ' +
          'Cost note: niente addebitato · reject client-side · 0 byte inviati, 0 token. ' +
          'Actions: Riprova · Attiva flash · Inserisci § manuale · Digita manualmente.',
      },
    },
  },
};

// ── Frame 07 · Translation Timeout — AI translate abort > 20s (mockup Stato G) ──

export const Frame07_TranslationTimeout: Story = {
  name: '07 · Translation Timeout — AI translate aborted > 20s, manual edit fallback (mockup Stato G)',
  args: {
    variant: 'translation-timeout',
  },
  parameters: {
    docs: {
      description: {
        story:
          'Mockup Stato G (translation-timeout): OCR completato con successo (testo pronto), ' +
          'ma la chiamata AI di traduzione abortita dopo 22s (soglia 20s AbortController). ' +
          'Distinto dallo Stato A (chat stream-timeout): qui niente stream parziale, è request/response. ' +
          'Testo OCR originale sempre disponibile come fallback non-distruttivo. ' +
          'Manual edit textarea con testo OCR pre-popsolato per traduzione manuale. ' +
          'Cost note: AbortController = 0 token traduzione. ' +
          'Actions: Riprova traduzione (primary warn) · Traduci a mano · Continua in lingua originale.',
      },
    },
  },
};

// ── Frame 08 · Source Lang Unknown — lang conf 0.38 (mockup Stato H) ───────

export const Frame08_SourceLangUnknown: Story = {
  name: '08 · Source Lang Unknown — lang confidence 0.38 < 0.50, 5-option dialog (mockup Stato H)',
  args: {
    variant: 'source-lang-unknown',
  },
  parameters: {
    docs: {
      description: {
        story:
          'Mockup Stato H (source-lang-unknown): LLM lang confidence 0.38 < soglia 0.50. ' +
          'Tipico in librogame multilingua, glossari fantasy con neologismi. ' +
          'Dialog blocking (NON inline banner) con radiogroup 5 opzioni (EN 38% · IT 24% · FR 11% · DE 9% · ES 7%). ' +
          'EN pre-selezionato come proposta auto-detect. ' +
          'Cost note: solo OCR addebitato · lang-detect free · traduzione N3 dopo conferma. ' +
          'Actions: Conferma "English" (primary danger) · Annulla traduzione.',
      },
    },
  },
};

// ── Frame 09 · Network Mid OCR — connection lost, IndexedDB queue (mockup Stato I) ──

export const Frame09_NetworkMidOcr: Story = {
  name: '09 · Network Mid OCR — connection lost mid-upload, IndexedDB queue (mockup Stato I)',
  args: {
    variant: 'network-mid-ocr',
  },
  parameters: {
    docs: {
      description: {
        story:
          'Mockup Stato I (network-mid-ocr): connessione persa durante upload foto (treno, cantina). ' +
          'Strategia: persist locale IndexedDB + queue + retry automatico on connectivity-change. ' +
          'Banner offline strip persistente top (NON modal blocking). ' +
          'Queue card con 1 foto in attesa (status: ⏳ attesa). ' +
          'Cost note: upload incompleto = 0 byte ricevuti server-side. ' +
          'Actions: Riprova subito · Vai a Glossario offline · Cancella coda. ' +
          'Debug strip: net::ERR_NETWORK_CHANGED + IndexedDB key.',
      },
    },
  },
};

// ── Frame 10 · Quota Exhausted — daily quota 100%, bridge to credits (mockup Stato J) ──

export const Frame10_QuotaExhausted: Story = {
  name: '10 · Quota Exhausted — token quota 100% mid-stream, bridge credits overlay (mockup Stato J)',
  args: {
    variant: 'quota-exhausted',
  },
  parameters: {
    docs: {
      description: {
        story:
          'Mockup Stato J (quota-exhausted): quota token termina mentre AI sta traducendo. ' +
          'Pattern "soft" danger-warn (non è errore tecnico, è limite di piano "Esploratore"). ' +
          'Quota meter 100% fill (danger red). Bridge a quota-credits overlay. ' +
          'OCR text preservato non-distruttivamente (Aaron NON perde il paragrafo). ' +
          'Cost note: solo OCR + traduzione parziale addebitati · mostrare testo = 0 token extra. ' +
          'Actions: Ricarica crediti (primary warn) · Leggi in lingua originale · Aspetta reset. ' +
          'Debug strip: quota 8470/8470 + parziale 42/~210 tokens.',
      },
    },
  },
};
