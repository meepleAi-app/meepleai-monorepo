/**
 * AdvancedFiltersDrawer Storybook Stories (Phase 3a #1606)
 *
 * Interactive fixture for each entity scope (game, agent, session, kb, chat).
 * Each story opens the drawer immediately so the filter sections are visible.
 * State is local to the story render function so Apply/Clear are interactive.
 */

import { useState } from 'react';

import { AdvancedFiltersDrawer } from './AdvancedFiltersDrawer';

import type { LibraryFilters } from './types';
import type { Meta, StoryObj } from '@storybook/react';

const meta = {
  title: 'Components/Library/AdvancedFiltersDrawer',
  component: AdvancedFiltersDrawer,
  parameters: {
    layout: 'centered',
    docs: {
      description: {
        component:
          'Slide-in drawer exposing entity-scoped filter controls for the Library Hybrid Hub. Each entity scope (game/agent/session/kb/chat) renders a distinct set of sections. DS-15 compliant — semantic tokens only.',
      },
    },
  },
  tags: ['autodocs'],
} satisfies Meta<typeof AdvancedFiltersDrawer>;

export default meta;
type Story = StoryObj<typeof meta>;

// ── Story factory ─────────────────────────────────────────────────────────────
function makeStory(entityScope: LibraryFilters['scope'], initial: LibraryFilters): Story {
  return {
    render: () => {
      const [open, setOpen] = useState(true);
      const [active, setActive] = useState<LibraryFilters>(initial);
      return (
        <AdvancedFiltersDrawer
          open={open}
          onOpenChange={setOpen}
          entityScope={entityScope}
          activeFilters={active}
          onApply={next => setActive(next)}
          onClear={() => setActive({ scope: entityScope } as LibraryFilters)}
        />
      );
    },
  };
}

// ── Stories ───────────────────────────────────────────────────────────────────

/** Game scope — empty filters, shows all 5 sections (state, withKb, rating, players, year). */
export const GameScope: Story = makeStory('game', { scope: 'game' });

/** Game scope — pre-populated filters (states + ratingMin + withKb active). */
export const GameScopeWithFilters: Story = makeStory('game', {
  scope: 'game',
  states: ['Owned'],
  ratingMin: 7,
  withKb: true,
});

/** Agent scope — shows 2 sections (types, activeOnly). */
export const AgentScope: Story = makeStory('agent', { scope: 'agent', activeOnly: true });

/** Session scope — shows 3 sections (statuses, sessionTypes, playerCount). */
export const SessionScope: Story = makeStory('session', { scope: 'session', playerCountMin: 4 });

/** Kb scope — shows 1 section (processingStates). */
export const KbScope: Story = makeStory('kb', { scope: 'kb', processingStates: ['Ready'] });

/** Chat scope — shows 1 section (messageCountMin slider). */
export const ChatScope: Story = makeStory('chat', { scope: 'chat', messageCountMin: 10 });
