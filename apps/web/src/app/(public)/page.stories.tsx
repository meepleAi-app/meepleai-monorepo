/**
 * Landing Page Storybook Stories
 *
 * Full page composition for visual regression testing.
 * Tests complete landing page with all sections integrated.
 */

import LandingPage from './page';

import type { Meta, StoryObj } from '@storybook/react';

const meta: Meta<typeof LandingPage> = {
  title: 'Pages/LandingPage',
  component: LandingPage,
  parameters: {
    layout: 'fullscreen',
    chromatic: {
      viewports: [375, 768, 1440],
      delay: 300, // Allow animations to settle
    },
  },
  tags: ['autodocs'],
};

export default meta;
type Story = StoryObj<typeof LandingPage>;

/**
 * Complete Landing Page
 * Full page with all sections: Hero, Features, How It Works, Footer
 */
export const Default: Story = {};

/**
 * Mobile Experience (375px)
 * Complete mobile flow from hero to footer
 */
export const MobileFlow: Story = {
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
 * Tablet Experience (768px)
 * Mid-size responsive layout
 */
export const TabletFlow: Story = {
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
 * Desktop Experience (1440px)
 * Full desktop layout with all features
 */
export const DesktopFlow: Story = {
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
 * Dark Theme - Full Page
 * Complete dark mode experience
 */
export const DarkTheme: Story = {
  decorators: [
    Story => (
      <div className="dark">
        <Story />
      </div>
    ),
  ],
};
