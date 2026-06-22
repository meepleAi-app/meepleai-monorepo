/**
 * Fixtures for EncounterCheatsheetView story — DS-17 Phase D-2 (sub-issue #2174).
 *
 * @mockup admin-mockups/design_files/librogame-runthrough-encounter-cheatsheet.html
 *
 * State-driving strategy (props-driven — no test-seam, no MSW):
 *   EncounterCheatsheetView is a pure component: `status` is a direct prop and all
 *   FSM branches (idle/parsing/rendered/error) are reachable by setting it.
 *   No internal useState, no hooks, no fetches inside the component.
 *
 *   | Frame             | status     | extra props              |
 *   |-------------------|------------|--------------------------|
 *   | Frame01_Idle      | 'idle'     | storyContext             |
 *   | Frame02_Parsing   | 'parsing'  | onCancel provided        |
 *   | Frame03_Rendered  | 'rendered' | cheatsheet fixture below |
 *   | Frame04_Error     | 'error'    | errorKind='parse-failed' |
 *
 * Refs: umbrella #2063, sub-issue #2174 (Phase D-2).
 */

import type {
  EncounterCheatsheet,
  EncounterCheatsheetLabels,
  EncounterStoryContext,
} from '@/components/features/gamebook/EncounterCheatsheetView';

// ---------------------------------------------------------------------------
// Labels (Italian, mirrors mockup text)
// ---------------------------------------------------------------------------

export const MOCK_ENCOUNTER_LABELS: EncounterCheatsheetLabels = {
  // entry (A)
  entryStoryMeta: 'incontro da Storybook',
  entryRefTitle: 'Encounter Book richiesto',
  entryRefHint: "Il paragrafo §147 richiede una verifica dell'Encounter Book.",
  entryCta: 'Apri Encounter Book',
  entryCtaHint: 'Scatta una foto della pagina incontro per analizzarla',
  // parsing (B)
  loadingTitle: 'Analisi in corso…',
  loadingHint: "Estrazione statistiche e opzioni dall'Encounter Book (budget 4.5 s)",
  cancel: 'Annulla',
  // rendered (C)
  optionsTitle: 'Opzioni di combattimento',
  conditionsWin: 'Vittoria',
  conditionsLoss: 'Sconfitta',
  parseConfidence: 'conf.',
  lowConfidenceHint: '⚠️ Confidenza bassa — verifica manuale consigliata prima di procedere.',
  ephemeralNote: 'Cheatsheet temporanea · valida solo per questa sessione',
  retake: 'Nuovo scatto',
  glossary: 'Glossario',
  resolve: 'Risolvi →',
  // error
  errorParseFailed:
    "Impossibile analizzare l'Encounter Book. Il modello non ha trovato statistiche valide. Riprova con un nuovo scatto.",
  errorNotFound: "Foto o segmento non trovato. Riprova dall'inizio.",
  errorGeneric: 'Si è verificato un errore imprevisto. Riprova.',
  retry: 'Riprova',
  // ConfidenceBadge SR labels
  confidence: {
    high: 'Confidenza alta',
    medium: 'Confidenza media',
    low: 'Confidenza bassa',
  },
};

// ---------------------------------------------------------------------------
// Story context (idle frame — §147 references encounter)
// ---------------------------------------------------------------------------

export const MOCK_ENCOUNTER_STORY_CONTEXT: EncounterStoryContext = {
  paragraphLabel: '§147',
  excerpt:
    'I Guardiani ti bloccano la via. "Nessuno passa senza il permesso del Consiglio." ' +
    "Prepara l'Encounter Book alla pagina §218 per conoscere le statistiche.",
};

// ---------------------------------------------------------------------------
// Full cheatsheet fixture (rendered frame — State C)
// ---------------------------------------------------------------------------

export const MOCK_ENCOUNTER_CHEATSHEET: EncounterCheatsheet = {
  enemies: [
    {
      name: 'Guardiano Nanolith',
      icon: '⚔️',
      paragraphMarker: '§218',
      hp: '12',
      atk: '+4',
      def: '3',
      mov: '2',
    },
    {
      name: 'Scudo Vivente',
      icon: '🛡️',
      paragraphMarker: null,
      hp: '8',
      atk: '+2',
      def: '5',
      mov: '1',
    },
  ],
  options: [
    {
      label: 'Attacco frontale',
      diceRoll: { sides: 6, count: 1, modifier: 2, threshold: 5 },
      outcome: 'Se riesce: Guardiano perde 4 HP',
    },
    {
      label: 'Scivola lateralmente',
      diceRoll: { sides: 6, count: 1, modifier: 0, threshold: 4 },
      outcome: 'Bypass Scudo Vivente',
    },
    {
      label: 'Parla con il Consiglio',
      diceRoll: null,
      outcome: 'Richiede Oggetto "Sigillo del Consiglio"',
    },
  ],
  conditions: {
    win: 'Sconfiggi entrambi i nemici o usa il Sigillo del Consiglio → vai a §153',
    loss: 'HP scendono a 0 → vai a §200 (cattura)',
  },
  confidence: {
    enemies: 0.88,
    options: 0.74,
    conditions: 0.91,
  },
};

// ---------------------------------------------------------------------------
// Low-confidence cheatsheet variant (triggers forced-manual warning §9.1)
// ---------------------------------------------------------------------------

export const MOCK_ENCOUNTER_CHEATSHEET_LOW_CONFIDENCE: EncounterCheatsheet = {
  ...MOCK_ENCOUNTER_CHEATSHEET,
  confidence: {
    enemies: 0.45,
    options: 0.52,
    conditions: 0.38,
  },
};
