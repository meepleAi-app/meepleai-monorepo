import type { Meta, StoryObj } from '@storybook/react';
import TestimonialsSection from './TestimonialsSection';

/**
 * TestimonialsSection component for the landing page.
 * Displays three testimonials from users in a responsive grid.
 * Each testimonial includes a quote, author name, role, and emoji avatar.
 *
 * Uses framer-motion for fade-in animations and react-intersection-observer.
 */
const meta = {
  title: 'Landing/TestimonialsSection',
  component: TestimonialsSection,
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
} satisfies Meta<typeof TestimonialsSection>;

export default meta;
type Story = StoryObj<typeof meta>;

/**
 * Default Testimonials Section
 * Shows three testimonials in a responsive grid
 */
export const Default: Story = {
  render: () => <TestimonialsSection />,
};

/**
 * Testimonials in viewport
 * Demonstrates the staggered fade-in animation when scrolled into view
 */
export const InViewport: Story = {
  render: () => (
    <div className="min-h-screen bg-slate-950">
      <div className="h-screen flex items-center justify-center text-white">
        <p className="text-xl">Scroll down to see the Testimonials Section animate</p>
      </div>
      <TestimonialsSection />
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
 * Testimonials stacked in a single column on mobile
 */
export const Mobile: Story = {
  render: () => <TestimonialsSection />,
  parameters: {
    viewport: {
      defaultViewport: 'mobile1',
    },
  },
};

/**
 * Tablet View
 * Testimonials in responsive grid on tablet
 */
export const Tablet: Story = {
  render: () => <TestimonialsSection />,
  parameters: {
    viewport: {
      defaultViewport: 'tablet',
    },
  },
};

/**
 * Dark Mode (Default)
 * Testimonials section in dark mode theme
 */
export const DarkMode: Story = {
  render: () => <TestimonialsSection />,
  parameters: {
    backgrounds: {
      default: 'dark',
    },
  },
};

/**
 * Social Proof Context
 * Shows testimonials in the context of a complete landing page flow
 */
export const SocialProofContext: Story = {
  render: () => (
    <div className="min-h-screen bg-slate-950">
      <div className="py-20 px-6 text-center text-white">
        <h1 className="text-5xl font-bold mb-4">See What Our Users Say</h1>
        <p className="text-xl text-slate-300 mb-12">
          Join thousands of board game enthusiasts using MeepleAI
        </p>
      </div>
      <TestimonialsSection />
      <div className="py-20 px-6 text-center text-white">
        <p className="text-lg text-slate-400">
          Ready to join them? Get started free today!
        </p>
      </div>
    </div>
  ),
};

/**
 * Testimonial Cards Detail
 * Close-up view showing the card design and layout
 */
export const CardsDetail: Story = {
  render: () => (
    <div className="bg-slate-950 p-12">
      <div className="max-w-4xl mx-auto text-white mb-8">
        <h3 className="text-2xl font-bold mb-4">Testimonial Card Design</h3>
        <p className="text-slate-300">
          Each card features an emoji avatar, quote, author name, and role with subtle styling
        </p>
      </div>
      <TestimonialsSection />
    </div>
  ),
};
