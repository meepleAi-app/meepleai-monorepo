import { PageTransition } from './PageTransition';

import type { Meta, StoryObj } from '@storybook/react';

/* DISABLED - causes Maximum update depth
/**
 * PageTransition component for route change animations.
 *
 * ## Features
 * - **Variants**: Fade, slide, scale transitions
 * - **Route changes**: Smooth page transitions
 * - **Accessibility**: Respects motion preferences
 *
 * ## Use Cases
 * - App Router page transitions
 * - Section transitions
 * - View mode changes
 */
const meta = {
  title: 'Animations/PageTransition',
  component: PageTransition,
  parameters: {
    layout: 'centered',
    docs: {
      description: {
        component:
          'Page transition animation wrapper for smooth route changes. Supports fade, slide, and scale variants.',
      },
    },
  },
  tags: ['autodocs'],
  argTypes: {
    variant: {
      control: 'select',
      options: ['fade', 'slide', 'scale'],
      description: 'Transition animation type',
    },
  },
} satisfies Meta<typeof PageTransition>;

export default meta;
type Story = StoryObj<typeof meta>;

/**
 * Fade transition (default).
 */
export const Fade: Story = {
  render: () => (
    <PageTransition variant="fade">
      <div className="w-96 h-64 bg-card border rounded-lg p-6">
        <h2 className="text-2xl font-bold mb-4">Page Content</h2>
        <p className="text-muted-foreground">
          This content fades in smoothly when the page loads.
        </p>
      </div>
    </PageTransition>
  ),
};

/**
 * Slide transition.
 */
export const Slide: Story = {
  render: () => (
    <PageTransition variant="slide">
      <div className="w-96 h-64 bg-card border rounded-lg p-6">
        <h2 className="text-2xl font-bold mb-4">Slide In</h2>
        <p className="text-muted-foreground">
          Content slides in from left while fading.
        </p>
      </div>
    </PageTransition>
  ),
};

/**
 * Scale transition.
 */
export const Scale: Story = {
  render: () => (
    <PageTransition variant="scale">
      <div className="w-96 h-64 bg-card border rounded-lg p-6">
        <h2 className="text-2xl font-bold mb-4">Scale In</h2>
        <p className="text-muted-foreground">
          Content scales up from 95% while fading.
        </p>
      </div>
    </PageTransition>
  ),
};

/* DISABLED - Interactive story removed
 * Interactive demo with transitions.
 */
