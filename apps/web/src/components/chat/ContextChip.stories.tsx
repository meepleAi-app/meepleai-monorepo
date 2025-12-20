/**
 * ContextChip - Chromatic Visual Regression Stories
 *
 * @issue #1840 (PAGE-004)
 */

import { ContextChip } from './ContextChip';

import type { Meta, StoryObj } from '@storybook/react';

const meta: Meta<typeof ContextChip> = {
  title: 'Components/Chat/ContextChip',
  component: ContextChip,
  parameters: {
    layout: 'padded',
    chromatic: {
      viewports: [375, 768, 1024],
    },
  },
  tags: ['autodocs'],
};

export default meta;
type Story = StoryObj<typeof ContextChip>;

/**
 * 1. Default - Basic context
 */
export const Default: Story = {
  name: '1. Default (Catan)',
  args: {
    gameName: 'Catan',
    gameEmoji: '🎲',
    sources: [{ type: 'PDF', count: 1 }, { type: 'FAQ', count: 15 }, { type: 'Wiki' }],
  },
};

/**
 * 2. With Remove Button
 */
export const WithRemoveButton: Story = {
  name: '2. With Remove Button',
  args: {
    gameName: 'Wingspan',
    gameEmoji: '🦅',
    sources: [{ type: 'PDF', count: 2 }, { type: 'Wiki' }],
    onRemove: () => console.log('Context removed'),
  },
};

/**
 * 3. Multiple PDFs
 */
export const MultiplePDFs: Story = {
  name: '3. Multiple PDFs',
  args: {
    gameName: '7 Wonders',
    gameEmoji: '🏛️',
    sources: [
      { type: 'PDF', count: 5 },
      { type: 'FAQ', count: 23 },
    ],
  },
};

/**
 * 4. No Sources
 */
export const NoSources: Story = {
  name: '4. No Sources',
  args: {
    gameName: 'Azul',
    gameEmoji: '🎨',
    sources: [],
  },
};

/**
 * 5. Long Game Name
 */
export const LongGameName: Story = {
  name: '5. Long Game Name (Truncated)',
  args: {
    gameName: 'Twilight Imperium: Fourth Edition - Prophecy of Kings Expansion',
    gameEmoji: '🚀',
    sources: [{ type: 'PDF', count: 3 }, { type: 'FAQ', count: 42 }, { type: 'Wiki' }],
    onRemove: () => console.log('Remove'),
  },
};

/**
 * 6. Mobile View
 */
export const MobileView: Story = {
  name: '6. Mobile View (375px)',
  args: {
    gameName: 'Carcassonne',
    gameEmoji: '🏰',
    sources: [
      { type: 'PDF', count: 1 },
      { type: 'FAQ', count: 8 },
    ],
  },
  parameters: {
    viewport: {
      defaultViewport: 'mobile1',
    },
    chromatic: {
      viewports: [375],
    },
  },
};

/**
 * 7. Many Sources
 */
export const ManySources: Story = {
  name: '7. Many Sources (Wrapping)',
  args: {
    gameName: 'Gloomhaven',
    gameEmoji: '⚔️',
    sources: [
      { type: 'PDF', count: 8 },
      { type: 'FAQ', count: 156 },
      { type: 'Wiki' },
      { type: 'PDF', count: 2 },
    ],
  },
};
