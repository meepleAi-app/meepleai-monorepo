/**
 * LandingFooter Storybook Stories
 *
 * Visual regression testing for footer with CTAs and legal links.
 */

import type { Meta, StoryObj } from '@storybook/react';
import { LandingFooter } from './LandingFooter';

const meta: Meta<typeof LandingFooter> = {
  title: 'Landing/LandingFooter',
  component: LandingFooter,
  parameters: {
    layout: 'fullscreen',
    chromatic: {
      viewports: [375, 768, 1024],
    },
  },
  tags: ['autodocs'],
};

export default meta;
type Story = StoryObj<typeof LandingFooter>;

/**
 * Default Landing Footer
 * CTA section + legal links
 */
export const Default: Story = {};

/**
 * Mobile View (375px)
 * Stacked CTA buttons and legal links
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
 * Desktop View (1024px)
 * Horizontal layout for CTAs and legal links
 */
export const Desktop: Story = {
  parameters: {
    viewport: {
      defaultViewport: 'desktop',
    },
    chromatic: {
      viewports: [1024],
    },
  },
};

/**
 * Dark Theme
 * Border and text contrast in footer
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
