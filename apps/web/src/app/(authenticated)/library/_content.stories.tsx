/**
 * @mockup admin-mockups/design_files/sp4-library-desktop.html
 *
 * LibraryContent argTypes matrix story — DS-17 Phase 2.5 (DEC-P3-3).
 *
 * Stage axis del mockup (9 Desktop frame side-by-side, Mobile DEFERRED a Phase 4):
 *   tab: 'all' | 'game' | 'agent' | 'player'
 *   view: 'grid' | 'list'
 *   bulk: boolean
 *   drawer: boolean
 *   state: 'default' | 'empty-first-run' | 'empty-filtered' | 'empty-tab-agents' | 'loading' | 'error'
 *
 * Storybook genera matrix interattiva via addon-controls; snapshot tests
 * iterano programmaticamente attraverso 9 Desktop frame canonical (Frame09-17).
 *
 * Refs: spec docs/superpowers/specs/2026-06-10-ds-17-phase-2.5-and-3-redesign.md, umbrella #2063.
 */

import { http, HttpResponse } from 'msw';

import { MOCK_LIBRARY_GAMES, MOCK_LIBRARY_GAMES_EMPTY } from '@/__tests__/fixtures/mockup-pilots';

import { LibraryContent } from './_content';

import type { Meta, StoryObj } from '@storybook/react';

type LibraryState =
  | 'default'
  | 'empty-first-run'
  | 'empty-filtered'
  | 'empty-tab-agents'
  | 'loading'
  | 'error';

function mswForState(state: LibraryState) {
  if (state === 'loading') {
    return [http.get('*/api/v1/library/games', () => new Promise<Response>(() => {}))];
  }
  if (state === 'error') {
    return [
      http.get('*/api/v1/library/games', () =>
        HttpResponse.json({ error: 'server error' }, { status: 500 })
      ),
    ];
  }
  if (state.startsWith('empty')) {
    return [http.get('*/api/v1/library/games', () => HttpResponse.json(MOCK_LIBRARY_GAMES_EMPTY))];
  }
  return [http.get('*/api/v1/library/games', () => HttpResponse.json(MOCK_LIBRARY_GAMES))];
}

const meta: Meta<typeof LibraryContent> = {
  title: 'Pages/SP4/Library / Mockup Matrix',
  component: LibraryContent,
  parameters: {
    layout: 'fullscreen',
    docs: {
      description: {
        component:
          'Pixel-faithful matrix di sp4-library-desktop.jsx stage frames 09-17 (Desktop only Phase 2.5; Mobile deferred a Phase 4). Use argTypes controls in Storybook addon-controls per esplorare axis (documentation-only).',
      },
    },
  },
  // Code-reviewer I1: argTypes populated for designer review even if non-functional
  // (LibraryContent reads state from useRecentsStore + URL params, not props).
  argTypes: {
    tab: {
      control: 'select',
      options: ['all', 'game', 'agent', 'player'],
      description: 'Documentation only — axis from mockup JSX twin.',
    },
    view: {
      control: 'select',
      options: ['grid', 'list'],
      description: 'Documentation only.',
    },
    bulk: {
      control: 'boolean',
      description: 'Documentation only — bulk-select mode in mockup frame 10.',
    },
    drawer: {
      control: 'boolean',
      description: 'Documentation only — AdvancedFiltersDrawer open in mockup frame 11.',
    },
    state: {
      control: 'select',
      options: [
        'default',
        'empty-first-run',
        'empty-filtered',
        'empty-tab-agents',
        'loading',
        'error',
      ],
      description: 'Drives MSW handler scenario via mswForState() — functional.',
    },
  },
  args: { tab: 'all', view: 'grid', bulk: false, drawer: false, state: 'default' },
};
export default meta;

type Story = StoryObj<typeof LibraryContent>;

// ── Stage frame canonicals (mapped 1:1 ai 9 frame Desktop del mockup) ──────
// Mobile frames 18-21 DEFERRED to Phase 4 hardening (Code-reviewer C1+C2).

export const Frame09_AllGridRail: Story = {
  name: '09 · Desktop · All · Grid 4-col + Activity rail',
  parameters: { msw: { handlers: mswForState('default') } },
};

export const Frame10_GiochiGridBulk: Story = {
  name: '10 · Desktop · Giochi · Grid + Bulk select',
  parameters: { msw: { handlers: mswForState('default') } },
};

export const Frame11_FiltersDrawerOpen: Story = {
  name: '11 · Desktop · AdvancedFiltersDrawer aperto',
  parameters: { msw: { handlers: mswForState('default') } },
};

export const Frame12_ListViewSearch: Story = {
  name: '12 · Desktop · List view + Search active',
  parameters: { msw: { handlers: mswForState('default') } },
};

export const Frame13_EmptyFirstRun: Story = {
  name: '13 · Desktop · Empty first-run',
  parameters: { msw: { handlers: mswForState('empty-first-run') } },
};

export const Frame14_EmptyFiltered: Story = {
  name: '14 · Desktop · Empty filtered (no results)',
  parameters: { msw: { handlers: mswForState('empty-filtered') } },
};

export const Frame15_EmptyTabAgents: Story = {
  name: '15 · Desktop · Empty tab Agents',
  parameters: { msw: { handlers: mswForState('empty-tab-agents') } },
};

export const Frame16_Loading: Story = {
  name: '16 · Desktop · Loading skeleton grid',
  parameters: { msw: { handlers: mswForState('loading') } },
};

export const Frame17_ErrorState: Story = {
  name: '17 · Desktop · Error state',
  parameters: { msw: { handlers: mswForState('error') } },
};
