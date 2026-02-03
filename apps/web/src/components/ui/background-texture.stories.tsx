import { BackgroundTexture } from './BackgroundTexture';

import type { Meta, StoryObj } from '@storybook/react';

/**
 * BackgroundTexture - Warm board game aesthetic overlay.
 *
 * ## Custom MeepleAI Component
 * Subtle wood/paper texture for brand identity.
 *
 * ## Features
 * - **Dual-layer**: Gradients + radial warmth
 * - **Performance**: CSS-only, GPU accelerated
 * - **Intensity control**: Subtle, normal, strong
 * - **Non-intrusive**: Pointer events disabled
 *
 * ## Design
 * Creates warm board game atmosphere without interfering with content.
 */
const meta = {
  title: 'MeepleAI/BackgroundTexture',
  component: BackgroundTexture,
  parameters: {
    layout: 'fullscreen',
    docs: {
      description: {
        component:
          'Background texture overlay for MeepleAI warm board game aesthetic. Provides subtle wood/paper texture.',
      },
    },
  },
  tags: ['autodocs'],
  argTypes: {
    opacity: {
      control: { type: 'range', min: 0, max: 1, step: 0.1 },
      description: 'Overall opacity',
      table: {
        defaultValue: { summary: '0.6' },
      },
    },
    enabled: {
      control: 'boolean',
      description: 'Enable/disable texture',
      table: {
        defaultValue: { summary: 'true' },
      },
    },
    intensity: {
      control: 'select',
      options: ['subtle', 'normal', 'strong'],
      description: 'Texture intensity',
      table: {
        defaultValue: { summary: 'normal' },
      },
    },
  },
} satisfies Meta<typeof BackgroundTexture>;

export default meta;
type Story = StoryObj<typeof meta>;

/**
 * Default texture (normal intensity).
 */
export const Default: Story = {
  args: {
    opacity: 0.6,
    intensity: 'normal',
  },
  render: (args) => (
    <div className="min-h-screen bg-background p-8">
      <BackgroundTexture {...args} />
      <div className="relative z-10 max-w-2xl mx-auto">
        <h1 className="text-4xl font-bold mb-4">MeepleAI</h1>
        <p className="text-lg text-muted-foreground mb-6">
          Experience the warm board game aesthetic with subtle background texture.
        </p>
        <div className="grid grid-cols-2 gap-4">
          {Array.from({ length: 4 }).map((_, i) => (
            <div key={i} className="p-6 bg-card border rounded-lg">
              <h3 className="font-semibold mb-2">Feature {i + 1}</h3>
              <p className="text-sm text-muted-foreground">
                Content with texture overlay in background.
              </p>
            </div>
          ))}
        </div>
      </div>
    </div>
  ),
};

/**
 * Subtle intensity.
 */
export const Subtle: Story = {
  args: {
    intensity: 'subtle',
  },
  render: (args) => (
    <div className="min-h-screen bg-background p-8">
      <BackgroundTexture {...args} />
      <div className="relative z-10 max-w-xl mx-auto text-center">
        <h2 className="text-2xl font-bold mb-4">Subtle Texture</h2>
        <p className="text-muted-foreground">
          Very light texture overlay for minimal visual impact.
        </p>
      </div>
    </div>
  ),
};

/**
 * Strong intensity.
 */
export const Strong: Story = {
  args: {
    intensity: 'strong',
  },
  render: (args) => (
    <div className="min-h-screen bg-background p-8">
      <BackgroundTexture {...args} />
      <div className="relative z-10 max-w-xl mx-auto text-center">
        <h2 className="text-2xl font-bold mb-4">Strong Texture</h2>
        <p className="text-muted-foreground">
          More prominent texture for stronger visual identity.
        </p>
      </div>
    </div>
  ),
};

/**
 * Disabled (no texture).
 */
export const Disabled: Story = {
  args: {
    enabled: false,
  },
  render: (args) => (
    <div className="min-h-screen bg-background p-8">
      <BackgroundTexture {...args} />
      <div className="relative z-10 max-w-xl mx-auto text-center">
        <h2 className="text-2xl font-bold mb-4">No Texture</h2>
        <p className="text-muted-foreground">Clean background without texture overlay.</p>
      </div>
    </div>
  ),
};

/**
 * Dark theme with texture.
 */
export const DarkTheme: Story = {
  args: {
    opacity: 0.6,
    intensity: 'normal',
  },
  render: (args) => (
    <div className="dark min-h-screen bg-background p-8">
      <BackgroundTexture {...args} />
      <div className="relative z-10 max-w-xl mx-auto text-center">
        <h2 className="text-2xl font-bold mb-4">Dark Theme</h2>
        <p className="text-muted-foreground">
          Texture adapts to dark mode with warm overlay.
        </p>
      </div>
    </div>
  ),
  parameters: {
    backgrounds: { default: 'dark' },
  },
};
