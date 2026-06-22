/**
 * GameDetailCommunityGate Storybook Stories (Issue #1465)
 *
 * Empty-state for the community variant of /games/[id], shown inside locked tabs.
 * Covers the default game gate (📘) and a discover/community variant (🌐).
 * Dark theme handled globally via the preview decorator.
 */

import { GameDetailCommunityGate } from './GameDetailCommunityGate';

import type { Meta, StoryObj } from '@storybook/react';

const meta = {
  title: 'Components/GameDetail/GameDetailCommunityGate',
  component: GameDetailCommunityGate,
  parameters: {
    layout: 'centered',
    docs: {
      description: {
        component:
          'Pure presentational empty-state for community-variant locked tabs. The caller (#1466) renders it inside locked tab content and wires onAdd to the add-to-library mutation. i18n caller-side. DS-15 compliant.',
      },
    },
    chromatic: { viewports: [375, 768, 1024] },
  },
  tags: ['autodocs'],
  decorators: [
    Story => (
      <div className="w-full max-w-md">
        <Story />
      </div>
    ),
  ],
} satisfies Meta<typeof GameDetailCommunityGate>;

export default meta;
type Story = StoryObj<typeof meta>;

/** Default — game book icon, add-to-library CTA. */
export const Default: Story = {
  args: {
    icon: '📘',
    title: 'Aggiungi alla tua libreria',
    description:
      'Aggiungi questo gioco per registrare le sessioni, sbloccare le statistiche e la classifica giocatori.',
    ctaLabel: '+ Aggiungi a libreria',
    onAdd: () => {},
  },
};

/** Discover/community variant — globe icon. */
export const DiscoverCommunity: Story = {
  args: {
    icon: '🌐',
    title: 'Gioco della community',
    description:
      'Questo gioco non è ancora nella tua libreria. Aggiungilo per accedere a tutte le funzioni della pagina.',
    ctaLabel: '+ Aggiungi a libreria',
    onAdd: () => {},
  },
};
