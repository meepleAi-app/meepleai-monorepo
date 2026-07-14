/**
 * @mockup admin-mockups/design_files/librogame-runthrough-resume-picker.html
 *
 * ResumeBooksList argTypes matrix story — DS-17 Phase D-2 (sub-issue #2174).
 *
 * State-driving strategy (STATIC — pure props + Template K mocks, no play functions):
 *   ResumeBooksList is a pure props-driven component (`progress: BookProgress[]`,
 *   `onResume`). It has no async hooks and no MSW requirements.
 *
 *   Five mockup states → five frames:
 *
 *   Frame01_FirstTime     — real component, progress: [] (empty-state branch).
 *                           Maps to mockup state-01 (EmptyFirstTime, data.length===0).
 *                           Note: the real empty state renders "Nessun libro in corso."
 *                           rather than the full onboarding illustration in the mockup —
 *                           this is an aspirational forward-refactor for the illustration
 *                           layer, but the component branch is correct.
 *
 *   Frame02_SingleResume  — real component, progress: [1 item] (non-stale campaign §289).
 *                           Maps to mockup state-02 (ResumeHero, data.length===1, !isStale).
 *
 *   Frame03_MultiCampaign — real component, progress: [2 items] ordered by last access.
 *                           Maps to mockup state-03 (MultiCampaignList, data.length>=2).
 *
 *   Frame04_StaleWarning  — Template K mock (ResumePickerStaleMock, forward-refactor).
 *                           The real component has no `isStale` prop.
 *                           Maps to mockup state-04 (StaleWarningCard, 60-day inactive).
 *
 *   Frame05_WithTutorial  — Template K mock (ResumePickerTutorialMock, forward-refactor).
 *                           The real component has no `tutorialAvailable` prop.
 *                           Maps to mockup state-05 (ResumeHeroWithTutorial,
 *                           DocumentCategory.QuickStart available).
 *
 * Snapshot harness (`apps/web/e2e/storybook/librogame.snapshot.spec.ts`) does
 * `goto + waitForTimeout(2000) + screenshot` — it does NOT run Storybook play
 * functions. Pure props + Template K mocks ensure each frame is correct on first paint.
 *
 * BGG guard: no BGG refs in this file (#2123).
 *
 * Refs: spec docs/superpowers/specs/2026-06-22-ds-17-phase-d-2-librogame-storybook-migration-design.md,
 *       umbrella #2063, sub-issue #2174 (Phase D-2).
 */

import { fn } from '@storybook/test';

import {
  ResumePickerStaleMock,
  ResumePickerTutorialMock,
} from '@/__tests__/fixtures/mockup-pilots/librogame/_mocks/ResumePickerMock';
import {
  FIXTURE_MULTI_CAMPAIGN,
  FIXTURE_SINGLE_RESUME,
} from '@/__tests__/fixtures/mockup-pilots/librogame/librogame-resume-picker';

import { ResumeBooksList } from './ResumeBooksList';

import type { Story } from '@storybook/react';
import type { Meta } from '@storybook/react';

// ---------------------------------------------------------------------------
// Module-scope spy for onResume (reused by all real-component frames).
// ---------------------------------------------------------------------------
const mockOnResume = fn();

// ---------------------------------------------------------------------------
// Meta
// ---------------------------------------------------------------------------

const meta: Meta<typeof ResumeBooksList> = {
  title: 'Pages/Librogame/Resume Picker',
  component: ResumeBooksList,
  parameters: {
    layout: 'padded',
    docs: {
      description: {
        component:
          'Pixel-faithful matrix di librogame-runthrough-resume-picker.html 5 stati. ' +
          'Frame01–03 usano il vero ResumeBooksList con props dirette (empty + 1-entry + 2-entry). ' +
          'Frame04 (stale-warning) e Frame05 (with-tutorial) sono Template K mocks ' +
          '(forward-refactor: isStale + tutorialAvailable non ancora props del componente reale).',
      },
    },
    // Frame01_FirstTime (progress: []) covers canonical `empty`, Frame02/03 cover
    // canonical `default` (populated list) — driven by array-length props, not
    // `mswForState`-style string literals — lint:storybook-states heuristic can't
    // see them (#2342 Task 4 bonifica).
    canonicalStates: ['default', 'empty'],
  },
  argTypes: {
    progress: {
      control: 'object',
      description:
        'Array di BookProgress. Lunghezza determina il branch: 0=empty, 1=single, N=multi.',
    },
    onResume: {
      action: 'onResume',
      description: 'Callback fired with bookId when "Riprendi" is clicked.',
    },
  },
  args: {
    progress: FIXTURE_SINGLE_RESUME,
    onResume: mockOnResume,
  },
};
export default meta;

// ── Frame 01 · First Time — empty state, progress: [] ────────────────────────

export const Frame01_FirstTime: Story<typeof ResumeBooksList> = {
  name: '01 · First Time — nessun libro in corso (empty state)',
  args: {
    progress: [],
    onResume: mockOnResume,
  },
  parameters: {
    docs: {
      description: {
        story:
          'Stato 01 mockup (EmptyFirstTime). progress.length===0 → branch empty del componente reale. ' +
          'Renderizza "Nessun libro in corso." (l\'illustrazione onboarding-grade del mockup è ' +
          'forward-refactor non ancora implementata nel componente).',
      },
    },
  },
};

// ── Frame 02 · Single Resume — 1 campaign, §289 ──────────────────────────────

export const Frame02_SingleResume: Story<typeof ResumeBooksList> = {
  name: '02 · Single Resume — 1 campagna attiva, §289',
  args: {
    progress: FIXTURE_SINGLE_RESUME,
    onResume: mockOnResume,
  },
  parameters: {
    docs: {
      description: {
        story:
          'Stato 02 mockup (ResumeHero). progress.length===1, campagna non-stale. ' +
          '"Campagna con i ragazzi" al §289 Cap 4. Bottone "Riprendi" presente.',
      },
    },
  },
};

// ── Frame 03 · Multi Campaign — 2 campaigns ──────────────────────────────────

export const Frame03_MultiCampaign: Story<typeof ResumeBooksList> = {
  name: '03 · Multi Campaign — 2 campagne attive, ordinate per ultimo accesso',
  args: {
    progress: FIXTURE_MULTI_CAMPAIGN,
    onResume: mockOnResume,
  },
  parameters: {
    docs: {
      description: {
        story:
          'Stato 03 mockup (MultiCampaignList). progress.length===2. ' +
          '"Sera con Sara" §47 (ieri) + "Campagna con i ragazzi" §289 (7 giorni fa). ' +
          'Ordinate per ultimo accesso (Sara prima). Entrambe con bottone "Riprendi".',
      },
    },
  },
};

// ── Frame 04 · Stale Warning — Template K mock (forward-refactor) ─────────────

export const Frame04_StaleWarning: Story<typeof ResumeBooksList> = {
  name: '04 · Stale Warning — campagna inattiva 60+ giorni (forward-refactor mock)',
  render: () => (
    <ResumePickerStaleMock
      campaignName="Campagna con i ragazzi"
      startedAt="5 marzo 2026"
      lastSessionAt="9 marzo 2026"
      lastLocation="§289"
      glossaryTerms={12}
      party={['Marco', 'Giulia', 'Luca', 'Aaron']}
      daysSinceLastSession={60}
    />
  ),
  parameters: {
    layout: 'fullscreen',
    docs: {
      description: {
        story:
          'Stato 04 mockup (StaleWarningCard). Template K mock forward-refactor: ' +
          'isStale non è ancora una prop del componente reale. ' +
          'Amber warning card per campagna inattiva > 60 giorni. ' +
          '3 CTAs: Riprendi comunque / Archivia + Nuova / Solo nuova.',
      },
    },
  },
};

// ── Frame 05 · With Tutorial — Template K mock (forward-refactor) ─────────────

export const Frame05_WithTutorial: Story<typeof ResumeBooksList> = {
  name: '05 · With Tutorial — tutorial QuickStart disponibile (forward-refactor mock)',
  render: () => (
    <ResumePickerTutorialMock
      campaignName="Campagna con i ragazzi"
      lastLocation="§289"
      chapterLabel="Capitolo 4 — La grotta dei Goblin"
      tutorialSteps={8}
      glossaryTerms={12}
      qaCount={47}
      party={['Marco', 'Giulia', 'Luca', 'Aaron']}
    />
  ),
  parameters: {
    layout: 'fullscreen',
    docs: {
      description: {
        story:
          'Stato 05 mockup (ResumeHeroWithTutorial). Template K mock forward-refactor: ' +
          'tutorialAvailable non è ancora una prop del componente reale. ' +
          'Variante di state-02 con badge "📚 Tutorial pronto" e CTA secondario Tutorial ' +
          'quando DocumentCategory.QuickStart è disponibile (#2025 audit B20).',
      },
    },
  },
};
