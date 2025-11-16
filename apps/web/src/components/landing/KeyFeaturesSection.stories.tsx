import type { Meta, StoryObj } from '@storybook/react';
import KeyFeaturesSection from './KeyFeaturesSection';

/**
 * KeyFeaturesSection component for the landing page.
 * Displays four key features in a 2x2 grid:
 * - Semantic Search
 * - Multi-Game Support
 * - Source Citations
 * - RuleSpec Editor
 *
 * Uses framer-motion for staggered animations and react-intersection-observer.
 */
const meta = {
  title: 'Landing/KeyFeaturesSection',
  component: KeyFeaturesSection,
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
} satisfies Meta<typeof KeyFeaturesSection>;

export default meta;
type Story = StoryObj<typeof meta>;

/**
 * Default Key Features Section
 * Shows the four key features in a responsive grid
 */
export const Default: Story = {
  render: () => <KeyFeaturesSection />,
};

/**
 * Key Features in viewport
 * Demonstrates the staggered slide-in animation when scrolled into view
 */
export const InViewport: Story = {
  render: () => (
    <div className="min-h-screen bg-slate-950">
      <div className="h-screen flex items-center justify-center text-white">
        <p className="text-xl">Scroll down to see the Key Features Section animate</p>
      </div>
      <KeyFeaturesSection />
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
 * Key features stacked in a single column on mobile
 */
export const Mobile: Story = {
  render: () => <KeyFeaturesSection />,
  parameters: {
    viewport: {
      defaultViewport: 'mobile1',
    },
  },
};

/**
 * Tablet View
 * Key features in 2-column grid on tablet
 */
export const Tablet: Story = {
  render: () => <KeyFeaturesSection />,
  parameters: {
    viewport: {
      defaultViewport: 'tablet',
    },
  },
};

/**
 * Dark Mode (Default)
 * Key features section in dark mode theme
 */
export const DarkMode: Story = {
  render: () => <KeyFeaturesSection />,
  parameters: {
    backgrounds: {
      default: 'dark',
    },
  },
};

/**
 * Interaction States
 * Demonstrates hover effects on feature cards
 */
export const InteractionStates: Story = {
  render: () => (
    <div className="bg-slate-950 p-8">
      <div className="max-w-4xl mx-auto text-white mb-8">
        <h3 className="text-2xl font-bold mb-4">Hover over cards to see interaction effects</h3>
        <p className="text-slate-300">Cards change border color on hover for visual feedback</p>
      </div>
      <KeyFeaturesSection />
    </div>
  ),
};
