/**
 * @mockup admin-mockups/design_files/librogame-runthrough-glossary-editor.html
 *
 * GlossaryEditorModal argTypes matrix story — DS-17 Phase D-2 (sub-issue #2174).
 *
 * State-driving strategy (STATIC — test-seam props + MSW, no play functions):
 *   GlossaryEditorModal uses `useReducer` initialized lazily from the `entry` prop.
 *   To render each mockup state on first paint without any user interaction, we
 *   use:
 *
 *   1. Test-seam prop `_initialState?: Partial<ModalState>` added to the real
 *      component — merges over `makeInitialState(entry)` at mount time, allowing
 *      `itValue`, `status`, `errorMessage`, and `collidingEntry` to be pre-seeded.
 *
 *   2. MSW PUT handler returning 500 for Frame03 (save-error) — ensures the
 *      mutation would fail if the user were to click Retry in Storybook.
 *
 *   3. Template K mocks (`GlossaryEditorBulkImportMock`, `GlossaryEditorVariantsMock`)
 *      for frames 05 + 06 — these sub-screens are NOT implemented in the real
 *      component (require `GlossaryEntry.contexts[]` schema migration).
 *
 * 6 mockup states → 6 story frames:
 *   Frame01_EditPristine — entry pre-filled, itValue == initialIt (Salva disabled)
 *   Frame02_Edited       — itValue dirtied, diff-hint shown (mockup state-02)
 *   Frame03_SaveError    — status='error', error banner shown (mockup state-03)
 *   Frame04_Collision    — status='collision' + collidingEntry (mockup state-04)
 *   Frame05_BulkImport   — Template K forward-refactor mock (mockup state-06)
 *   Frame06_Variants     — Template K forward-refactor mock (mockup state-07)
 *
 * Merged / skipped:
 *   - state-01-desktop + state-04-desktop-conflict: desktop layout (`forceLayout="desktop"`)
 *     produces no separate FSM branch — same Frame01 / Frame04 with `forceLayout` arg.
 *   - state-05-conflict-dialog: same FSM branch as state-04 collision; merged into Frame04.
 *   - state-08-filter-strip: index-level UI, not GlossaryEditorModal scope.
 *
 * BGG guard: no BGG refs in this file (#2123).
 *
 * Refs: spec docs/superpowers/specs/2026-06-22-ds-17-phase-d-2-librogame-storybook-migration-design.md,
 *       umbrella #2063, sub-issue #2174 (Phase D-2).
 */

import {
  GlossaryEditorBulkImportMock,
  GlossaryEditorVariantsMock,
} from '@/__tests__/fixtures/mockup-pilots/librogame/_mocks/GlossaryEditorMock';
import {
  INITIAL_STATE_COLLISION,
  INITIAL_STATE_EDITED,
  INITIAL_STATE_SAVE_ERROR,
  MOCK_CAMPAIGN_ID_GE,
  MOCK_GLOSSARY_ENTRY_GE,
  mswSaveErrorHandler,
} from '@/__tests__/fixtures/mockup-pilots/librogame/librogame-glossary-editor';

import { GlossaryEditorModal } from './GlossaryEditorModal';

import type { Meta, StoryObj } from '@storybook/react';

// ---------------------------------------------------------------------------
// Meta
// ---------------------------------------------------------------------------

const meta: Meta<typeof GlossaryEditorModal> = {
  title: 'Pages/Librogame/Glossary Editor',
  component: GlossaryEditorModal,
  parameters: {
    layout: 'centered',
    docs: {
      description: {
        component:
          'Pixel-faithful matrix di librogame-runthrough-glossary-editor.html 6 stati. ' +
          'Usa test-seam prop _initialState per guidare ogni stato STATICAMENTE senza interaction. ' +
          'Frame05/06 sono Template K forward-refactor mocks (GlossaryEntry.contexts[] non implementato).',
      },
    },
  },
  args: {
    campaignId: MOCK_CAMPAIGN_ID_GE,
    entry: MOCK_GLOSSARY_ENTRY_GE,
    onClose: () => {},
    onSaved: undefined,
    forceLayout: 'mobile',
  },
  argTypes: {
    campaignId: {
      control: 'text',
      description: 'Campaign UUID — used in MSW handlers.',
    },
    entry: {
      control: 'object',
      description: 'Glossary entry being edited. null collapses the modal.',
    },
    forceLayout: {
      control: { type: 'select' },
      options: ['mobile', 'desktop'],
      description: 'Force layout for visual tests.',
    },
    _initialState: {
      control: 'object',
      description: '[Test-seam] Override reducer state fields for static story rendering.',
    },
  },
};
export default meta;

type Story = StoryObj<typeof GlossaryEditorModal>;

// ── Frame 01 · Edit Pristine — Salva disabled, no diff-hint (mockup state-01) ──

export const Frame01_EditPristine: Story = {
  name: '01 · Edit Pristine — Salva disabled, IT prefilled (mockup state-01)',
  args: {
    forceLayout: 'mobile',
  },
  parameters: {
    docs: {
      description: {
        story:
          'Mockup stato state-01-edit-pristine: modale aperta su "sentinel → soldato di guardia". ' +
          'Input IT prefillato con il valore corrente. ' +
          'Salva disabilitato (itValue === initialIt, isDirty=false). ' +
          'Nessun diff-hint. Layout mobile (full-width sheet).',
      },
    },
  },
};

// ── Frame 02 · Edited — diff-hint, Salva abilitato (mockup state-02) ─────────

export const Frame02_Edited: Story = {
  name: '02 · Edited — diff-hint, Salva abilitato (mockup state-02)',
  args: {
    forceLayout: 'mobile',
    _initialState: INITIAL_STATE_EDITED,
  },
  parameters: {
    docs: {
      description: {
        story:
          'Mockup stato state-02-edited: input IT sporco con "punto di osservazione strategica". ' +
          'Diff-hint mostra il valore precedente "soldato di guardia" barrato (line-through). ' +
          'Salva abilitato (isDirty=true). Layout mobile.',
      },
    },
  },
};

// ── Frame 03 · Save Error — error banner + Retry (mockup state-03) ────────────

export const Frame03_SaveError: Story = {
  name: '03 · Save Error — banner amber, Retry visibile (mockup state-03)',
  args: {
    forceLayout: 'mobile',
    _initialState: INITIAL_STATE_SAVE_ERROR,
  },
  parameters: {
    msw: { handlers: mswSaveErrorHandler },
    docs: {
      description: {
        story:
          'Mockup stato state-03-save-error: status=error pre-seeded via _initialState. ' +
          'Error banner (role=alert) visibile con pulsante Retry. ' +
          'MSW PUT → 500 assicura che un eventuale click Retry fallisca anche in Storybook. ' +
          'Layout mobile.',
      },
    },
  },
};

// ── Frame 04 · Collision — CollisionBanner + [Sovrascrivi] + [Cambia] ─────────

export const Frame04_Collision: Story = {
  name: '04 · Collision — CollisionBanner, [Sovrascrivi] + [Cambia traduzione] (mockup state-04)',
  args: {
    forceLayout: 'mobile',
    _initialState: INITIAL_STATE_COLLISION,
  },
  parameters: {
    docs: {
      description: {
        story:
          'Mockup stato state-04-collision: status=collision + collidingEntry pre-seeded. ' +
          'CollisionBanner (role=alert, data-testid=glossary-editor-collision) visibile. ' +
          'Pulsanti [Sovrascrivi] e [Cambia traduzione] presenti. ' +
          'Layout mobile (desktop-conflict state ha anche il pannello laterale — usa forceLayout="desktop").',
      },
    },
  },
};

// ── Frame 05 · Bulk Import — Template K forward-refactor (mockup state-06) ───

export const Frame05_BulkImport: Story = {
  name: '05 · Bulk Import Card — forward-refactor (mockup state-06)',
  render: () => (
    <GlossaryEditorBulkImportMock
      newTermCount={4}
      previewTerms={['ferrigni', 'runa', 'soglia', 'ardenel']}
    />
  ),
  parameters: {
    docs: {
      description: {
        story:
          'Mockup stato state-06-bulk-import-card: inline card "N nuove parole rilevate" + chips + 2 CTA. ' +
          'FORWARD-REFACTOR — non implementato nel vero GlossaryEditorModal. ' +
          'Richiede OCR post-processing pipeline + GlossaryEntry.contexts[] schema migration backend. ' +
          'Template K mock (GlossaryEditorBulkImportMock).',
      },
    },
  },
};

// ── Frame 06 · Variants — Template K forward-refactor (mockup state-07) ──────

export const Frame06_Variants: Story = {
  name: '06 · Variant Expansion — forward-refactor (mockup state-07)',
  render: () => (
    <GlossaryEditorVariantsMock
      termEn="sentinel"
      baseTermIt="soldato di guardia"
      variants={[
        { bookName: 'Press Start', definition: null, firstSeenRef: '§147' },
        {
          bookName: 'Rules Game',
          definition: 'punto di osservazione strategica',
          firstSeenRef: '§63',
        },
      ]}
    />
  ),
  parameters: {
    docs: {
      description: {
        story:
          'Mockup stato state-07-variant-expansion: glossary index row espanso con N varianti per libro. ' +
          'FORWARD-REFACTOR — non implementato nel vero GlossaryEditorModal. ' +
          'Richiede GlossaryEntry.contexts[] schema migration backend (oggi single-context). ' +
          'Template K mock (GlossaryEditorVariantsMock).',
      },
    },
  },
};
