/**
 * @mockup admin-mockups/design_files/librogame-runthrough-library-search.html
 *
 * GamebookIndexView argTypes matrix story — DS-17 Phase D-2 (sub-issue #2174).
 *
 * Component choice:
 *   `GamebookIndexView` (client orchestrator) is imported directly rather than
 *   the server wrapper `page.tsx`. The server wrapper is a thin Suspense shell
 *   that simply mounts `<GamebookIndexView />` — importing it in Storybook
 *   would require server component support and yields no additional coverage.
 *   The FSM + `?fixture=` logic both live in `GamebookIndexView`.
 *
 * State-driving strategy (Template Mf — URL fixture override):
 *   All 6 FSM states are driven via `parameters.nextjs.navigation.query.fixture`.
 *   `STATE_OVERRIDE_ENABLED` is true in development / Storybook, so the
 *   `?fixture=<kind>` hatch short-circuits the real hooks and feeds the
 *   `gamebookIndexFixtures[kind]` data into the FSM deterministically.
 *
 *   `loading` and `error` cells short-circuit the FSM upstream of the fixture
 *   data path (GamebookIndexView.tsx:197-200) — no real query response is
 *   needed, and no MSW handler is required. The `?fixture=loading` /
 *   `?fixture=error` param is sufficient to force those cells.
 *
 * Stage axis (librogame-runthrough-library-search.html — 4 mockup states):
 *   The HTML mockup depicts the `/library` hub (4 states: default-grid,
 *   search-filtered, empty-no-match, filter-librogame). This story targets the
 *   `/gamebook` index FSM (6 cells), which is the upstream entry-point
 *   described by the mockup's "stato-01-default-grid" persona flow (Aaron
 *   navigates to /gamebook before entering /library/[gameId]/play).
 *
 *   Story FSM cells (one Frame per cell):
 *     01 default     — 4 gamebooks (ready/indexing/error mix), quota 12/50
 *     02 empty       — 0 gamebooks, empty-state CTA visible
 *     03 quota-soft  — 4 gamebooks, quota 47/50 (soft warning overlay)
 *     04 quota-hard  — 4 gamebooks, quota 50/50 (hard banner, hero CTA gated)
 *     05 loading     — 6 skeleton cards, GamebookHero present
 *     06 error       — error card with retry CTA
 *
 * Refs: spec docs/superpowers/specs/2026-06-22-ds-17-phase-d-2-librogame-storybook-migration-design.md,
 *       umbrella #2063, sub-issue #2174 (Phase D-2).
 */

import { GamebookIndexView } from './GamebookIndexView';

import type { Meta, StoryObj } from '@storybook/react';

const meta: Meta<typeof GamebookIndexView> = {
  title: 'Pages/Librogame/Library Search',
  component: GamebookIndexView,
  parameters: {
    layout: 'fullscreen',
    nextjs: {
      appDirectory: true,
      navigation: {
        pathname: '/gamebook',
        query: {},
      },
    },
    docs: {
      description: {
        component:
          'Pixel-faithful matrix di librogame-runthrough-library-search.html — `/gamebook` index ' +
          '6-cell FSM (loading | error | empty | default | quota-soft | quota-hard). ' +
          'Tutti gli stati sono pilotati via `?fixture=<kind>` (Template Mf). Nessun MSW handler ' +
          'necessario: il meccanismo `STATE_OVERRIDE_ENABLED` short-circuit i hook reali.',
      },
    },
  },
};
export default meta;

type Story = StoryObj<typeof GamebookIndexView>;

// ── Frame 01 · Default — 4 gamebooks, quota 12/50 ─────────────────────────────

export const Frame01_Default: Story = {
  name: '01 · Default — 4 gamebooks (ready/indexing/error mix), quota 12/50',
  parameters: {
    nextjs: {
      appDirectory: true,
      navigation: {
        pathname: '/gamebook',
        query: { fixture: 'default' },
      },
    },
    docs: {
      description: {
        story:
          'Stato principale. 4 gamebook: Nanolith (ready), Brass Birmingham (ready), ' +
          'Spirit Island (indexing), Wingspan (error). QuotaWidget variant=default, ' +
          '12/50 paragrafi usati. GamebookHero con KPI reali.',
      },
    },
  },
};

// ── Frame 02 · Empty — no gamebooks, empty-state CTA visible ──────────────────

export const Frame02_Empty: Story = {
  name: '02 · Empty — 0 gamebooks, CTA "Aggiungi il tuo primo manuale"',
  parameters: {
    nextjs: {
      appDirectory: true,
      navigation: {
        pathname: '/gamebook',
        query: { fixture: 'empty' },
      },
    },
    docs: {
      description: {
        story:
          'Stato vuoto. Nessun gamebook nella libreria. QuotaWidget variant=default ' +
          '(0/50 usati). EmptyGamebooks CTA visibile.',
      },
    },
  },
};

// ── Frame 03 · Quota Soft — 4 gamebooks, quota 47/50 ─────────────────────────

export const Frame03_QuotaSoft: Story = {
  name: '03 · Quota Soft — 4 gamebooks, quota 47/50 (soft warning overlay)',
  parameters: {
    nextjs: {
      appDirectory: true,
      navigation: {
        pathname: '/gamebook',
        query: { fixture: 'quota-soft' },
      },
    },
    docs: {
      description: {
        story:
          'Stato quota-soft (usage ≥ 90%). 4 gamebook. QuotaWidget variant=soft con ' +
          'avviso giallo. SoftWarningCredits overlay (toast mobile / modal desktop) ' +
          'visibile se non precedentemente dismissed.',
      },
    },
  },
};

// ── Frame 04 · Quota Hard — 4 gamebooks, quota 50/50 ─────────────────────────

export const Frame04_QuotaHard: Story = {
  name: '04 · Quota Hard — 4 gamebooks, quota 50/50 (hard banner, hero CTA gated)',
  parameters: {
    nextjs: {
      appDirectory: true,
      navigation: {
        pathname: '/gamebook',
        query: { fixture: 'quota-hard' },
      },
    },
    docs: {
      description: {
        story:
          'Stato quota-hard (100% usato). 4 gamebook. QuotaWidget variant=hard con ' +
          'banner rosso. Hero CTA reindirizza al checkout invece di /gamebook/upload ' +
          '(isHardLimit=true). CheckoutModal apribile dal banner.',
      },
    },
  },
};

// ── Frame 05 · Loading — 6 skeleton cards ─────────────────────────────────────

export const Frame05_Loading: Story = {
  name: '05 · Loading — 6 GamebookCardSkeleton + GamebookHero (KPI 0/0/0)',
  parameters: {
    nextjs: {
      appDirectory: true,
      navigation: {
        pathname: '/gamebook',
        query: { fixture: 'loading' },
      },
    },
    docs: {
      description: {
        story:
          'Stato loading. FSM short-circuit upstream (GamebookIndexView.tsx:197): ' +
          'nessun dato necessario. Renderizza 6 GamebookCardSkeleton animate-pulse ' +
          '+ GamebookHero con KPI azzerati. Aria role="status" + aria-live="polite".',
      },
    },
  },
};

// ── Frame 06 · Error — error card with retry CTA ──────────────────────────────

export const Frame06_Error: Story = {
  name: '06 · Error — errore rete, GamebookHero + error card + retry CTA',
  parameters: {
    nextjs: {
      appDirectory: true,
      navigation: {
        pathname: '/gamebook',
        query: { fixture: 'error' },
      },
    },
    docs: {
      description: {
        story:
          'Stato error. FSM short-circuit upstream (GamebookIndexView.tsx:198-200): ' +
          '`new Error("Fixture-forced error")` forced. Renderizza GamebookHero ' +
          '(KPI 0/0/0) + error card con testo + bottone "Riprova". ' +
          'Aria role="alert" su data-slot="gamebook-index-error".',
      },
    },
  },
};
