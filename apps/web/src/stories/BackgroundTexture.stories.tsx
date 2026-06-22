/* eslint-disable local/no-hardcoded-color-utility -- text-white / button color on style-prop colored bg or decorative inline gradient; mockup .e-bg pattern. Will be re-evaluated in DS-15 finalization audit. */
/**
 * BackgroundTexture Component - Storybook Documentation
 *
 * Visual reference for MeepleAI background texture system.
 * Issue #2847: Background Texture System
 * Epic #2845: MeepleAI Design System Integration
 */

import { BackgroundTexture } from '@/components/ui/BackgroundTexture';

import type { Meta, StoryObj } from '@storybook/react';


const meta: Meta<typeof BackgroundTexture> = {
  title: 'Design System/Background Texture',
  component: BackgroundTexture,
  parameters: {
    layout: 'fullscreen',
    docs: {
      description: {
        component:
          'Subtle wood/paper texture overlay that creates warm board game atmosphere for MeepleAI admin dashboard.',
      },
    },
  },
  argTypes: {
    opacity: {
      control: { type: 'range', min: 0, max: 1, step: 0.1 },
      description: 'Overall texture visibility (0-1)',
      defaultValue: 0.6,
    },
    enabled: {
      control: 'boolean',
      description: 'Enable/disable texture overlay',
      defaultValue: true,
    },
    intensity: {
      control: { type: 'select', options: ['subtle', 'normal', 'strong'] },
      description: 'Texture pattern intensity',
      defaultValue: 'normal',
    },
  },
};

export default meta;
type Story = StoryObj<typeof BackgroundTexture>;

/**
 * Default texture configuration matching design proposal
 */
export const Default: Story = {
  args: {
    opacity: 0.6,
    intensity: 'normal',
    enabled: true,
  },
  render: (args) => (
    <div className="min-h-screen bg-meeple-warm-bg relative">
      <BackgroundTexture {...args} />
      <div className="relative z-10 p-8">
        <div className="max-w-4xl mx-auto space-y-6">
          <h1 className="text-4xl font-bold font-heading text-meeple-dark">
            🎲 MeepleAI Admin Dashboard
          </h1>
          <p className="text-lg font-body text-muted-foreground">
            Notice the subtle wood/paper texture in the background creating a warm, board game atmosphere.
          </p>

          {/* Sample Cards to show texture behind content */}
          <div className="grid grid-cols-1 md:grid-cols-2 gap-6 mt-8">
            <div className="bg-card border border-meeple-border rounded-2xl p-6 shadow-sm">
              <h3 className="text-xl font-semibold font-heading mb-2">Metric Card</h3>
              <div className="text-3xl font-bold font-heading text-meeple-dark">2,847</div>
              <p className="text-sm text-muted-foreground mt-2">The texture is visible behind this card</p>
            </div>

            <div className="bg-card border border-meeple-border rounded-2xl p-6 shadow-sm">
              <h3 className="text-xl font-semibold font-heading mb-2">Service Status</h3>
              <div className="flex items-center gap-2">
                <div className="w-3 h-3 rounded-full bg-green-500" />
                <span className="text-sm font-semibold text-green-600">Operational</span>
              </div>
              <p className="text-sm text-muted-foreground mt-2">Texture adds depth without distraction</p>
            </div>
          </div>
        </div>
      </div>
    </div>
  ),
};

/**
 * Subtle intensity - minimal texture for refined aesthetic
 */
export const SubtleIntensity: Story = {
  args: {
    opacity: 0.6,
    intensity: 'subtle',
    enabled: true,
  },
  render: (args) => (
    <div className="min-h-screen bg-meeple-warm-bg relative">
      <BackgroundTexture {...args} />
      <div className="relative z-10 p-8">
        <h2 className="text-2xl font-bold font-heading text-meeple-dark mb-4">
          Subtle Intensity
        </h2>
        <p className="text-muted-foreground font-body">
          Barely perceptible texture - ideal for high-density information displays.
        </p>
      </div>
    </div>
  ),
};

/**
 * Strong intensity - more pronounced texture for bold aesthetic
 */
export const StrongIntensity: Story = {
  args: {
    opacity: 0.6,
    intensity: 'strong',
    enabled: true,
  },
  render: (args) => (
    <div className="min-h-screen bg-meeple-warm-bg relative">
      <BackgroundTexture {...args} />
      <div className="relative z-10 p-8">
        <h2 className="text-2xl font-bold font-heading text-meeple-dark mb-4">
          Strong Intensity
        </h2>
        <p className="text-muted-foreground font-body">
          More visible texture - creates pronounced board game atmosphere.
        </p>
      </div>
    </div>
  ),
};

/**
 * Opacity variations show texture visibility control
 */
export const OpacityVariations: Story = {
  render: () => (
    <div className="grid grid-cols-1 md:grid-cols-3 gap-0">
      {[0.3, 0.6, 0.9].map((opacity) => (
        <div key={opacity} className="min-h-screen bg-meeple-warm-bg relative">
          <BackgroundTexture opacity={opacity} intensity="normal" />
          <div className="relative z-10 p-8 text-center">
            <h3 className="text-xl font-bold font-heading text-meeple-dark">
              Opacity: {opacity}
            </h3>
            <p className="text-sm text-muted-foreground mt-2 font-body">
              {opacity < 0.5 ? 'Very subtle' : opacity < 0.75 ? 'Balanced' : 'Pronounced'}
            </p>
          </div>
        </div>
      ))}
    </div>
  ),
};

/**
 * Disabled state - texture can be toggled off
 */
export const Disabled: Story = {
  args: {
    enabled: false,
  },
  render: (args) => (
    <div className="min-h-screen bg-meeple-warm-bg relative">
      <BackgroundTexture {...args} />
      <div className="relative z-10 p-8">
        <h2 className="text-2xl font-bold font-heading text-meeple-dark mb-4">
          Texture Disabled
        </h2>
        <p className="text-muted-foreground font-body">
          Flat background with no texture overlay - for comparison or user preference.
        </p>
      </div>
    </div>
  ),
};

/**
 * Interactive playground with controls
 */
export const Playground: Story = {
  args: {
    opacity: 0.6,
    intensity: 'normal',
    enabled: true,
  },
  render: (args) => (
    <div className="min-h-screen bg-meeple-warm-bg relative">
      <BackgroundTexture {...args} />
      <div className="relative z-10 p-8">
        <div className="max-w-2xl mx-auto">
          <h1 className="text-3xl font-bold font-heading text-meeple-dark mb-6">
            Background Texture Playground
          </h1>

          <div className="bg-card rounded-2xl border border-meeple-border p-6 shadow-sm space-y-4">
            <div>
              <h3 className="font-semibold mb-2 font-heading">Current Settings</h3>
              <ul className="text-sm text-muted-foreground space-y-1 font-body">
                <li>Opacity: {args.opacity}</li>
                <li>Intensity: {args.intensity}</li>
                <li>Enabled: {args.enabled ? 'Yes' : 'No'}</li>
              </ul>
            </div>

            <div className="border-t border-border pt-4">
              <p className="text-sm text-muted-foreground font-body">
                Use the controls panel to adjust texture parameters and observe the effect on the
                background. The texture should be visible but not distracting from content.
              </p>
            </div>
          </div>

          {/* Additional content to show texture at scale */}
          <div className="grid grid-cols-2 gap-4 mt-8">
            {Array.from({ length: 4 }, (_, i) => (
              <div key={i} className="bg-card rounded-xl border border-meeple-border p-4 shadow-sm">
                <div className="text-2xl font-bold font-heading text-meeple-dark">{1000 + i * 100}</div>
                <p className="text-xs text-muted-foreground mt-1 font-body">Sample Metric</p>
              </div>
            ))}
          </div>
        </div>
      </div>
    </div>
  ),
};

/**
 * Dark mode compatibility test
 */
export const DarkMode: Story = {
  parameters: {
    backgrounds: { default: 'dark' },
  },
  args: {
    opacity: 0.4,
    intensity: 'subtle',
    enabled: true,
  },
  render: (args) => (
    <div className="min-h-screen bg-card relative dark">
      <BackgroundTexture {...args} />
      <div className="relative z-10 p-8">
        <h2 className="text-2xl font-bold text-white mb-4 font-heading">
          Dark Mode Texture
        </h2>
        <p className="text-foreground font-body">
          Texture opacity reduced in dark mode for better contrast. Notice the subtle warmth.
        </p>
        <div className="bg-card border border-border rounded-xl p-6 mt-6">
          <div className="text-3xl font-bold text-white font-heading">567</div>
          <p className="text-sm text-muted-foreground mt-2 font-body">Active Users</p>
        </div>
      </div>
    </div>
  ),
};
