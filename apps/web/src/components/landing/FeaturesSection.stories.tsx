/**
 * FeaturesSection Storybook Stories
 *
 * Visual regression testing for features grid section.
 * Chromatic integration for responsive testing.
 */

import type { Meta, StoryObj } from '@storybook/react';
import { FeaturesSection } from './FeaturesSection';

const meta: Meta<typeof FeaturesSection> = {
  title: 'Landing/FeaturesSection',
  component: FeaturesSection,
  parameters: {
    layout: 'fullscreen',
    chromatic: {
      viewports: [375, 768, 1024, 1440],
    },
  },
  tags: ['autodocs'],
};

export default meta;
type Story = StoryObj<typeof FeaturesSection>;

/**
 * Default Features Section
 * Three-column grid with AI, Catalog, Mobile features
 */
export const Default: Story = {};

/**
 * Mobile View (375px)
 * Single column stacked layout
 */
export const Mobile: Story = {
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
 * Tablet View (768px)
 * Two-column grid layout
 */
export const Tablet: Story = {
  parameters: {
    viewport: {
      defaultViewport: 'tablet',
    },
    chromatic: {
      viewports: [768],
    },
  },
};

/**
 * Desktop View (1440px)
 * Three-column grid layout
 */
export const Desktop: Story = {
  parameters: {
    viewport: {
      defaultViewport: 'desktop',
    },
    chromatic: {
      viewports: [1440],
    },
  },
};

/**
 * Dark Theme
 * Card hover states and color contrast validation
 */
export const DarkTheme: Story = {
  decorators: [
    Story => (
      <div className="dark bg-background">
        <Story />
      </div>
    ),
  ],
};

/**
 * Hover State
 * Verify card hover animations and shadows
 */
export const HoverState: Story = {
  parameters: {
    pseudo: { hover: ['.group'] },
  },
};
