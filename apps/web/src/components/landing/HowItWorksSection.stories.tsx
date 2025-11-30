/**
 * HowItWorksSection Storybook Stories
 *
 * Visual regression testing for the three-step process section.
 * Chromatic integration for flow visualization.
 */

import type { Meta, StoryObj } from '@storybook/react';
import { HowItWorksSection } from './HowItWorksSection';

const meta: Meta<typeof HowItWorksSection> = {
  title: 'Landing/HowItWorksSection',
  component: HowItWorksSection,
  parameters: {
    layout: 'fullscreen',
    chromatic: {
      viewports: [375, 768, 1024, 1440],
    },
  },
  tags: ['autodocs'],
};

export default meta;
type Story = StoryObj<typeof HowItWorksSection>;

/**
 * Default How It Works Section
 * Three-step numbered process with connecting arrows
 */
export const Default: Story = {};

/**
 * Mobile View (375px)
 * Vertical stacked steps without connecting arrows
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
 * Desktop View (1440px)
 * Horizontal flow with arrow connectors
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
 * Number badges and emoji contrast validation
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
