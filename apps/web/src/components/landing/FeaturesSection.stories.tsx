import type { Meta, StoryObj } from '@storybook/react';
import FeaturesSection from './FeaturesSection';

/**
 * FeaturesSection component for the landing page.
 * Displays the "How It Works" section with three steps: Upload, Ask, Play.
 * Uses framer-motion for animations and react-intersection-observer for viewport detection.
 */
const meta = {
  title: 'Landing/FeaturesSection',
  component: FeaturesSection,
  parameters: {
    layout: 'fullscreen',
    backgrounds: {
      default: 'dark',
      values: [
        { name: 'dark', value: '#0f172a' },
      ],
    },
  },
  tags: ['autodocs'],
} satisfies Meta<typeof FeaturesSection>;

export default meta;
type Story = StoryObj<typeof meta>;

/**
 * Default Features Section
 * Shows the three-step process: Upload → Ask → Play
 */
export const Default: Story = {
  render: () => <FeaturesSection />,
};

/**
 * Features Section in viewport
 * Demonstrates the animation when scrolled into view
 */
export const InViewport: Story = {
  render: () => (
    <div className="min-h-screen bg-slate-950">
      <div className="h-screen flex items-center justify-center text-white">
        <p className="text-xl">Scroll down to see the Features Section animate</p>
      </div>
      <FeaturesSection />
    </div>
  ),
  parameters: {
    viewport: {
      defaultViewport: 'responsive',
    },
  },
};

/**
 * Mobile View
 * Features section on mobile devices (single column layout)
 */
export const Mobile: Story = {
  render: () => <FeaturesSection />,
  parameters: {
    viewport: {
      defaultViewport: 'mobile1',
    },
  },
};

/**
 * Tablet View
 * Features section on tablet devices
 */
export const Tablet: Story = {
  render: () => <FeaturesSection />,
  parameters: {
    viewport: {
      defaultViewport: 'tablet',
    },
  },
};

/**
 * Dark Mode (Default)
 * Features section in dark mode theme
 */
export const DarkMode: Story = {
  render: () => <FeaturesSection />,
  parameters: {
    backgrounds: {
      default: 'dark',
    },
  },
};
