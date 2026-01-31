/**
 * CategoryPill - Issue #2372
 *
 * Displays a game category as a styled pill/chip.
 * Used in game cards, detail views, and list tables.
 */

import { CategoryPill, CategoryPillList } from './CategoryPill';

import type { Meta, StoryObj } from '@storybook/react';

const meta = {
  title: 'Admin/SharedGames/CategoryPill',
  component: CategoryPill,
  parameters: {
    layout: 'centered',
    chromatic: {
      viewports: [375, 768, 1024],
    },
  },
  tags: ['autodocs'],
} satisfies Meta<typeof CategoryPill>;

export default meta;
type Story = StoryObj<typeof meta>;

/**
 * Default category pill
 */
export const Default: Story = {
  args: {
    name: 'Strategia',
    size: 'default',
  },
};

/**
 * Small size category pill
 */
export const Small: Story = {
  args: {
    name: 'Strategia',
    size: 'sm',
  },
};

/**
 * Clickable category pill
 */
export const Clickable: Story = {
  args: {
    name: 'Strategia',
    onClick: () => alert('Categoria cliccata!'),
  },
};

/**
 * Removable category pill
 */
export const Removable: Story = {
  args: {
    name: 'Strategia',
    removable: true,
    onRemove: () => alert('Categoria rimossa!'),
  },
};

/**
 * Removable and clickable
 */
export const RemovableClickable: Story = {
  args: {
    name: 'Strategia',
    removable: true,
    onClick: () => alert('Categoria cliccata!'),
    onRemove: () => alert('Categoria rimossa!'),
  },
};

/**
 * Small removable
 */
export const SmallRemovable: Story = {
  args: {
    name: 'Strategia',
    size: 'sm',
    removable: true,
    onRemove: () => alert('Categoria rimossa!'),
  },
};

/**
 * Long category name
 */
export const LongName: Story = {
  args: {
    name: 'Gioco di Carte Collezionabili',
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
        <CategoryPill name="Strategia" />
        <CategoryPill name="Famiglia" />
        <CategoryPill name="Party" />
      </div>
      <div className="flex items-center gap-4">
        <span className="w-20 text-sm text-muted-foreground">Small:</span>
        <CategoryPill name="Strategia" size="sm" />
        <CategoryPill name="Famiglia" size="sm" />
        <CategoryPill name="Party" size="sm" />
      </div>
    </div>
  ),
};

// ==================== CategoryPillList Stories ====================

const mockCategories = [
  { id: '1', name: 'Strategia' },
  { id: '2', name: 'Famiglia' },
  { id: '3', name: 'Party' },
  { id: '4', name: 'Cooperativo' },
  { id: '5', name: 'Giochi di Carte' },
];

const _metaList = {
  title: 'Admin/SharedGames/CategoryPillList',
  component: CategoryPillList,
  parameters: {
    layout: 'centered',
    chromatic: {
      viewports: [375, 768, 1024],
    },
  },
  tags: ['autodocs'],
} satisfies Meta<typeof CategoryPillList>;

type StoryList = StoryObj<typeof _metaList>;

/**
 * Category pill list with default max visible (3)
 */
export const ListDefault: StoryList = {
  render: () => <CategoryPillList categories={mockCategories} />,
};

/**
 * Category pill list - all visible
 */
export const ListAllVisible: StoryList = {
  render: () => <CategoryPillList categories={mockCategories} maxVisible={10} />,
};

/**
 * Category pill list - small size
 */
export const ListSmall: StoryList = {
  render: () => <CategoryPillList categories={mockCategories} size="sm" />,
};

/**
 * Category pill list - clickable
 */
export const ListClickable: StoryList = {
  render: () => (
    <CategoryPillList
      categories={mockCategories}
      onCategoryClick={id => alert(`Categoria ID: ${id}`)}
    />
  ),
};

/**
 * Category pill list - removable
 */
export const ListRemovable: StoryList = {
  render: () => (
    <CategoryPillList
      categories={mockCategories}
      removable
      onRemove={id => alert(`Rimuovi categoria ID: ${id}`)}
    />
  ),
};

/**
 * Category pill list - few items (no hidden count)
 */
export const ListFewItems: StoryList = {
  render: () => <CategoryPillList categories={mockCategories.slice(0, 2)} maxVisible={3} />,
};

/**
 * Empty list
 */
export const ListEmpty: StoryList = {
  render: () => <CategoryPillList categories={[]} />,
};
