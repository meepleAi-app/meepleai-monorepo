/**
 * HeroSection Storybook Stories
 *
 * Visual regression testing for landing page hero section.
 * Chromatic integration for automated visual testing.
 */

import type { Meta, StoryObj } from '@storybook/react';
import { HeroSection } from './HeroSection';

const meta: Meta<typeof HeroSection> = {
  title: 'Landing/HeroSection',
  component: HeroSection,
  parameters: {
    layout: 'fullscreen',
    chromatic: {
      viewports: [375, 768, 1024, 1440],
    },
  },
  tags: ['autodocs'],
};

export default meta;
type Story = StoryObj<typeof HeroSection>;

/**
 * Default Hero Section
 * Mobile-first responsive design with MeepleAvatar and CTAs
 */
export const Default: Story = {};

/**
 * Mobile View (375px)
 * Single column layout with centered avatar
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
 * Two column layout starts appearing
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
 * Full two-column layout with large avatar
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
 * Verify color contrast and visual hierarchy in dark mode
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
