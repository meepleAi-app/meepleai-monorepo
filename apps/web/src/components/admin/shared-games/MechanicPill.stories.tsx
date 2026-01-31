/**
 * MechanicPill - Issue #2372
 *
 * Displays a game mechanic as a styled pill/chip.
 * Used in game cards, detail views, and list tables.
 */

import { MechanicPill, MechanicPillList } from './MechanicPill';

import type { Meta, StoryObj } from '@storybook/react';

const meta = {
  title: 'Admin/SharedGames/MechanicPill',
  component: MechanicPill,
  parameters: {
    layout: 'centered',
    chromatic: {
      viewports: [375, 768, 1024],
    },
  },
  tags: ['autodocs'],
} satisfies Meta<typeof MechanicPill>;

export default meta;
type Story = StoryObj<typeof meta>;

/**
 * Default mechanic pill
 */
export const Default: Story = {
  args: {
    name: 'Deck Building',
    size: 'default',
  },
};

/**
 * Small size mechanic pill
 */
export const Small: Story = {
  args: {
    name: 'Deck Building',
    size: 'sm',
  },
};

/**
 * Clickable mechanic pill
 */
export const Clickable: Story = {
  args: {
    name: 'Deck Building',
    onClick: () => alert('Meccanica cliccata!'),
  },
};

/**
 * Removable mechanic pill
 */
export const Removable: Story = {
  args: {
    name: 'Deck Building',
    removable: true,
    onRemove: () => alert('Meccanica rimossa!'),
  },
};

/**
 * Removable and clickable
 */
export const RemovableClickable: Story = {
  args: {
    name: 'Deck Building',
    removable: true,
    onClick: () => alert('Meccanica cliccata!'),
    onRemove: () => alert('Meccanica rimossa!'),
  },
};

/**
 * Small removable
 */
export const SmallRemovable: Story = {
  args: {
    name: 'Deck Building',
    size: 'sm',
    removable: true,
    onRemove: () => alert('Meccanica rimossa!'),
  },
};

/**
 * Long mechanic name
 */
export const LongName: Story = {
  args: {
    name: 'Worker Placement with Resource Management',
  },
};

/**
 * Size comparison
 */
export const SizeComparison: Story = {
  render: () => (
    <div className="space-y-4">
      <div className="flex items-center gap-4">
        <span className="w-20 text-sm text-muted-foreground">Default:</span>
        <MechanicPill name="Deck Building" />
        <MechanicPill name="Worker Placement" />
        <MechanicPill name="Drafting" />
      </div>
      <div className="flex items-center gap-4">
        <span className="w-20 text-sm text-muted-foreground">Small:</span>
        <MechanicPill name="Deck Building" size="sm" />
        <MechanicPill name="Worker Placement" size="sm" />
        <MechanicPill name="Drafting" size="sm" />
      </div>
    </div>
  ),
};

// ==================== MechanicPillList Stories ====================

const mockMechanics = [
  { id: '1', name: 'Deck Building' },
  { id: '2', name: 'Worker Placement' },
  { id: '3', name: 'Drafting' },
  { id: '4', name: 'Tile Placement' },
  { id: '5', name: 'Area Control' },
];

const _metaList = {
  title: 'Admin/SharedGames/MechanicPillList',
  component: MechanicPillList,
  parameters: {
    layout: 'centered',
    chromatic: {
      viewports: [375, 768, 1024],
    },
  },
  tags: ['autodocs'],
} satisfies Meta<typeof MechanicPillList>;

type StoryList = StoryObj<typeof _metaList>;

/**
 * Mechanic pill list with default max visible (3)
 */
export const ListDefault: StoryList = {
  render: () => <MechanicPillList mechanics={mockMechanics} />,
};

/**
 * Mechanic pill list - all visible
 */
export const ListAllVisible: StoryList = {
  render: () => <MechanicPillList mechanics={mockMechanics} maxVisible={10} />,
};

/**
 * Mechanic pill list - small size
 */
export const ListSmall: StoryList = {
  render: () => <MechanicPillList mechanics={mockMechanics} size="sm" />,
};

/**
 * Mechanic pill list - clickable
 */
export const ListClickable: StoryList = {
  render: () => (
    <MechanicPillList
      mechanics={mockMechanics}
      onMechanicClick={id => alert(`Meccanica ID: ${id}`)}
    />
  ),
};

/**
 * Mechanic pill list - removable
 */
export const ListRemovable: StoryList = {
  render: () => (
    <MechanicPillList
      mechanics={mockMechanics}
      removable
      onRemove={id => alert(`Rimuovi meccanica ID: ${id}`)}
    />
  ),
};

/**
 * Mechanic pill list - few items (no hidden count)
 */
export const ListFewItems: StoryList = {
  render: () => <MechanicPillList mechanics={mockMechanics.slice(0, 2)} maxVisible={3} />,
};

/**
 * Empty list
 */
export const ListEmpty: StoryList = {
  render: () => <MechanicPillList mechanics={[]} />,
};
