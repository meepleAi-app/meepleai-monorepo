/**
 * Storybook stories for ViewModeSwitcher component
 */

import { useState } from 'react';

import { ViewModeSwitcher } from './view-mode-switcher';

import type { ViewMode } from '../entity-list-view.types';
import type { Meta, StoryObj } from '@storybook/react';

const meta: Meta<typeof ViewModeSwitcher> = {
  title: 'UI/Data Display/EntityListView/ViewModeSwitcher',
  component: ViewModeSwitcher,
  parameters: {
    layout: 'centered',
    docs: {
      description: {
        component:
          'Segmented control for switching between Grid, List, and Carousel view modes. Features keyboard navigation and accessibility support.',
      },
    },
  },
  tags: ['autodocs'],
  argTypes: {
    value: {
      control: 'radio',
      options: ['grid', 'list', 'carousel'],
      description: 'Current active view mode',
    },
    availableModes: {
      control: 'check',
      options: ['grid', 'list', 'carousel'],
      description: 'Available view modes to display',
    },
  },
};

export default meta;
type Story = StoryObj<typeof meta>;

// ============================================================================
// Stories
// ============================================================================

/**
 * Default with all 3 modes
 */
export const Default: Story = {
  args: {
    value: 'grid',
    onChange: (mode) => console.log('Selected:', mode),
    availableModes: ['grid', 'list', 'carousel'],
  },
};

/**
 * Interactive example with state management
 */
export const Interactive: Story = {
  render: (args) => {
    const [mode, setMode] = useState<ViewMode>('grid');

    return (
      <div className="space-y-4">
        <ViewModeSwitcher
          {...args}
          value={mode}
          onChange={setMode}
          availableModes={['grid', 'list', 'carousel']}
        />
        <p className="text-sm text-muted-foreground">
          Current mode: <strong>{mode}</strong>
        </p>
      </div>
    );
  },
};

/**
 * Grid mode active
 */
export const GridActive: Story = {
  args: {
    value: 'grid',
    onChange: (mode) => console.log('Selected:', mode),
    availableModes: ['grid', 'list', 'carousel'],
  },
};

/**
 * List mode active
 */
export const ListActive: Story = {
  args: {
    value: 'list',
    onChange: (mode) => console.log('Selected:', mode),
    availableModes: ['grid', 'list', 'carousel'],
  },
};

/**
 * Carousel mode active
 */
export const CarouselActive: Story = {
  args: {
    value: 'carousel',
    onChange: (mode) => console.log('Selected:', mode),
    availableModes: ['grid', 'list', 'carousel'],
  },
};

/**
 * Only 2 modes available
 */
export const TwoModes: Story = {
  args: {
    value: 'grid',
    onChange: (mode) => console.log('Selected:', mode),
    availableModes: ['grid', 'list'],
  },
};

/**
 * Single mode (Grid only)
 */
export const SingleMode: Story = {
  args: {
    value: 'grid',
    onChange: (mode) => console.log('Selected:', mode),
    availableModes: ['grid'],
  },
};

/**
 * Keyboard navigation demo
 */
export const KeyboardNavigation: Story = {
  render: (args) => {
    const [mode, setMode] = useState<ViewMode>('grid');

    return (
      <div className="space-y-4">
        <div className="p-4 bg-accent/20 rounded-lg">
          <p className="text-sm font-medium mb-2">Keyboard Controls:</p>
          <ul className="text-sm text-muted-foreground space-y-1">
            <li>→ or ↓: Next mode</li>
            <li>← or ↑: Previous mode</li>
            <li>Tab: Focus switcher</li>
            <li>Enter/Space: Select focused mode</li>
          </ul>
        </div>

        <ViewModeSwitcher
          {...args}
          value={mode}
          onChange={setMode}
          availableModes={['grid', 'list', 'carousel']}
        />

        <p className="text-sm text-muted-foreground">
          Current mode: <strong>{mode}</strong>
        </p>
      </div>
    );
  },
};

/**
 * Custom styling
 */
export const CustomStyling: Story = {
  args: {
    value: 'grid',
    onChange: (mode) => console.log('Selected:', mode),
    availableModes: ['grid', 'list', 'carousel'],
    className: 'border-2 border-primary/30 bg-primary/5',
  },
};

/**
 * Dark mode preview
 */
export const DarkMode: Story = {
  args: {
    ...Default.args,
  },
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
};
