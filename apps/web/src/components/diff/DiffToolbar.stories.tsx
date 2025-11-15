import type { Meta, StoryObj } from '@storybook/react';
import { DiffToolbar } from './DiffToolbar';
import { DiffStatistics as DiffStats } from '@/lib/diffProcessor';
import { fn } from '@storybook/test';

/**
 * DiffToolbar - Toolbar with statistics, search, and navigation controls.
 * Positioned above the diff view.
 * Uses Tailwind classes consistent with shadcn UI.
 */
const meta = {
  title: 'Diff/DiffToolbar',
  component: DiffToolbar,
  parameters: {
    layout: 'padded',
  },
  tags: ['autodocs'],
  argTypes: {
    showNavigation: {
      control: 'boolean',
      description: 'Show search and navigation controls',
    },
    compact: {
      control: 'boolean',
      description: 'Compact mode with reduced padding',
    },
  },
  args: {
    onSearchChange: fn(),
    onNavigatePrev: fn(),
    onNavigateNext: fn(),
  },
} satisfies Meta<typeof DiffToolbar>;

export default meta;
type Story = StoryObj<typeof meta>;

// Mock statistics data
const mockStats: DiffStats = {
  totalLines: 150,
  addedLines: 25,
  removedLines: 18,
  unchangedLines: 107,
};

/**
 * Default toolbar with statistics only
 */
export const Default: Story = {
  args: {
    statistics: mockStats,
    searchQuery: '',
    currentChangeIndex: 0,
    totalChanges: 0,
    showNavigation: false,
    compact: false,
  },
};

/**
 * Toolbar with search and navigation
 */
export const WithNavigation: Story = {
  args: {
    statistics: mockStats,
    searchQuery: '',
    currentChangeIndex: 0,
    totalChanges: 10,
    showNavigation: true,
    compact: false,
  },
};

/**
 * Toolbar with active search
 */
export const WithSearch: Story = {
  args: {
    statistics: mockStats,
    searchQuery: 'react',
    currentChangeIndex: 2,
    totalChanges: 5,
    showNavigation: true,
    compact: false,
  },
};

/**
 * Compact mode
 */
export const Compact: Story = {
  args: {
    statistics: mockStats,
    searchQuery: 'version',
    currentChangeIndex: 1,
    totalChanges: 3,
    showNavigation: true,
    compact: true,
  },
};

/**
 * Small diff (few changes)
 */
export const SmallDiff: Story = {
  args: {
    statistics: {
      totalLines: 20,
      addedLines: 3,
      removedLines: 2,
      unchangedLines: 15,
    },
    searchQuery: '',
    currentChangeIndex: 0,
    totalChanges: 2,
    showNavigation: true,
    compact: false,
  },
};

/**
 * Large diff (many changes)
 */
export const LargeDiff: Story = {
  args: {
    statistics: {
      totalLines: 1250,
      addedLines: 342,
      removedLines: 289,
      unchangedLines: 619,
    },
    searchQuery: 'import',
    currentChangeIndex: 15,
    totalChanges: 48,
    showNavigation: true,
    compact: false,
  },
};

/**
 * No matches found
 */
export const NoMatches: Story = {
  args: {
    statistics: mockStats,
    searchQuery: 'nonexistent',
    currentChangeIndex: 0,
    totalChanges: 0,
    showNavigation: true,
    compact: false,
  },
};

/**
 * At first match
 */
export const FirstMatch: Story = {
  args: {
    statistics: mockStats,
    searchQuery: 'react',
    currentChangeIndex: 0,
    totalChanges: 5,
    showNavigation: true,
    compact: false,
  },
};

/**
 * At last match
 */
export const LastMatch: Story = {
  args: {
    statistics: mockStats,
    searchQuery: 'react',
    currentChangeIndex: 4,
    totalChanges: 5,
    showNavigation: true,
    compact: false,
  },
};

/**
 * Interactive search demo
 */
export const Interactive: Story = {
  render: () => {
    const [searchQuery, setSearchQuery] = React.useState('');
    const [currentIndex, setCurrentIndex] = React.useState(0);
    const totalChanges = 5;

    return (
      <DiffToolbar
        statistics={mockStats}
        searchQuery={searchQuery}
        onSearchChange={setSearchQuery}
        currentChangeIndex={currentIndex}
        totalChanges={searchQuery ? totalChanges : 0}
        onNavigatePrev={() => setCurrentIndex(Math.max(0, currentIndex - 1))}
        onNavigateNext={() => setCurrentIndex(Math.min(totalChanges - 1, currentIndex + 1))}
        showNavigation={true}
        compact={false}
      />
    );
  },
};

/**
 * Full diff view context
 */
export const InContext: Story = {
  render: () => (
    <div className="border rounded">
      <DiffToolbar
        statistics={mockStats}
        searchQuery="version"
        onSearchChange={() => {}}
        currentChangeIndex={1}
        totalChanges={3}
        onNavigatePrev={() => {}}
        onNavigateNext={() => {}}
        showNavigation={true}
        compact={false}
      />
      <div className="p-4 bg-slate-50">
        <div className="text-sm text-slate-600">Diff content would appear here</div>
      </div>
    </div>
  ),
};

/**
 * Dark mode
 */
export const DarkMode: Story = {
  parameters: {
    backgrounds: { default: 'dark' },
  },
  decorators: [
    (Story) => (
      <div className="dark">
        <Story />
      </div>
    ),
  ],
  args: {
    statistics: mockStats,
    searchQuery: 'react',
    currentChangeIndex: 2,
    totalChanges: 5,
    showNavigation: true,
    compact: false,
  },
};

/**
 * Responsive layout (mobile)
 */
export const Mobile: Story = {
  parameters: {
    viewport: {
      defaultViewport: 'mobile1',
    },
  },
  args: {
    statistics: mockStats,
    searchQuery: 'import',
    currentChangeIndex: 3,
    totalChanges: 8,
    showNavigation: true,
    compact: true,
  },
};

// Import React for interactive stories
import React from 'react';
