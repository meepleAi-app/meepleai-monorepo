/**
 * GameDetailSpecsCard Storybook Stories (Issue #1463)
 *
 * Visual fixtures for Chromatic / story-coverage.
 * Covers: default full data, null cascade, empty state, custom title, long values truncation.
 * Dark theme variant gestito globalmente via `withThemeByClassName` decorator in
 * `apps/web/.storybook/preview.tsx` (toolbar toggle — no story dedicato necessario).
 */

import { GameDetailSpecsCard, type GameDetailSpecsItem } from './GameDetailSpecsCard';

import type { Meta, StoryObj } from '@storybook/react';

const fullItems: ReadonlyArray<GameDetailSpecsItem> = [
  { key: 'players', label: 'Giocatori', value: '1–5' },
  { key: 'duration', label: 'Durata', value: '70 min' },
  { key: 'age', label: 'Età', value: '14+' },
  { key: 'complexity', label: 'Complessità', value: '2.4 / 5' },
  { key: 'year', label: 'Anno', value: '2017' },
  { key: 'designer', label: 'Designer', value: 'Stefan Feld' },
  { key: 'publisher', label: 'Editore', value: 'Kosmos' },
  { key: 'rating', label: 'Rating BGG', value: '8.1' },
];

const nullCascadeItems: ReadonlyArray<GameDetailSpecsItem> = [
  { key: 'players', label: 'Giocatori', value: '1–5' },
  { key: 'duration', label: 'Durata', value: '—' },
  { key: 'age', label: 'Età', value: '—' },
  { key: 'complexity', label: 'Complessità', value: '—' },
  { key: 'year', label: 'Anno', value: '—' },
  { key: 'designer', label: 'Designer', value: '—' },
  { key: 'publisher', label: 'Editore', value: '—' },
  { key: 'rating', label: 'Rating BGG', value: '—' },
];

const longValueItems: ReadonlyArray<GameDetailSpecsItem> = [
  ...fullItems.slice(0, 5),
  {
    key: 'designer',
    label: 'Designer',
    value: 'Bruno Cathala-Antoine Bauza-Klaus Teuber',
  },
  {
    key: 'publisher',
    label: 'Editore',
    value: 'Hans im Glück Verlags-GmbH (Germany Edition)',
  },
  { key: 'rating', label: 'Rating BGG', value: '8.1' },
];

const meta = {
  title: 'Components/GameDetail/GameDetailSpecsCard',
  component: GameDetailSpecsCard,
  parameters: {
    layout: 'centered',
    docs: {
      description: {
        component:
          'Presentational grid of 8 game specs items. Renders one listitem per `items` entry with uppercase mono label + display-font value. Pure component — formatting via `buildSpecsItems(detail, t)` helper sibling. DS-15 compliant.',
      },
    },
    chromatic: {
      viewports: [375, 768, 1024],
    },
  },
  tags: ['autodocs'],
  decorators: [
    Story => (
      <div className="w-full max-w-md">
        <Story />
      </div>
    ),
  ],
} satisfies Meta<typeof GameDetailSpecsCard>;

export default meta;
type Story = StoryObj<typeof meta>;

// =============================================================================
// Stories
// =============================================================================

/** Default state — all 8 specs populated with realistic data. */
export const Default: Story = {
  args: {
    items: fullItems,
    title: 'Specifiche',
  },
};

/** Null cascade — most fields missing from BE, em-dash placeholders preserve grid layout. */
export const WithNullables: Story = {
  args: {
    items: nullCascadeItems,
    title: 'Specifiche',
  },
};

/** Edge state — empty items array still renders title + container (no listitems). */
export const Empty: Story = {
  args: {
    items: [],
    title: 'Specifiche',
  },
};

/** i18n override — caller passes a different localized title (e.g. EN locale or alternate UX label). */
export const CustomTitle: Story = {
  args: {
    items: fullItems,
    title: 'Stats',
  },
};

/** Long values truncation — `line-clamp-1` prevents overflow on designer/publisher cells. */
export const LongDesignerName: Story = {
  args: {
    items: longValueItems,
    title: 'Specifiche',
  },
};
