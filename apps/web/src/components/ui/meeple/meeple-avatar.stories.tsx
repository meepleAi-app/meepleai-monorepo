import { MeepleAvatar } from './meeple-avatar';

import type { Meta, StoryObj } from '@storybook/react';

/**
 * MeepleAvatar displays an animated AI assistant character with 5 distinct states
 * to communicate AI activity and confidence levels.
 *
 * ## Design Reference
 * - Wireframes: `docs/04-frontend/wireframes-playful-boardroom.md` (AI Avatar States)
 * - Brainstorm: `docs/04-frontend/improvements/03-brainstorm-ideas.md` (#2.1)
 *
 * ## States
 * - **Idle**: Default resting state with subtle pulse
 * - **Thinking**: Animated dots showing AI processing
 * - **Confident**: Sparkles for high-confidence answers (≥85%)
 * - **Searching**: Rotating magnifying glass for manual searches
 * - **Uncertain**: Question mark for low-confidence answers (<70%)
 *
 * ## Accessibility
 * - ✅ ARIA labels for all states
 * - ✅ Respects `prefers-reduced-motion`
 * - ✅ Semantic `role="img"`
 */
const meta = {
  title: 'UI/MeepleAvatar',
  component: MeepleAvatar,
  parameters: {
    layout: 'centered',
    docs: {
      description: {
        component:
          'Animated AI assistant avatar representing different processing states and confidence levels.',
      },
    },
  },
  tags: ['autodocs'],
  argTypes: {
    state: {
      control: 'select',
      options: ['idle', 'thinking', 'confident', 'searching', 'uncertain'],
      description: 'Current AI state to display',
      table: {
        type: { summary: 'MeepleAvatarState' },
        defaultValue: { summary: 'idle' },
      },
    },
    size: {
      control: 'select',
      options: ['sm', 'md', 'lg'],
      description: 'Size variant: sm (32px), md (40px), lg (48px)',
      table: {
        type: { summary: 'MeepleAvatarSize' },
        defaultValue: { summary: 'md' },
      },
    },
    ariaLabel: {
      control: 'text',
      description: 'Custom ARIA label (auto-generated if not provided)',
    },
  },
} satisfies Meta<typeof MeepleAvatar>;

export default meta;
type Story = StoryObj<typeof meta>;

/**
 * Default idle state with subtle pulse animation.
 * Used when AI is ready but not actively processing.
 */
export const Idle: Story = {
  args: {
    state: 'idle',
    size: 'md',
  },
};

/**
 * Thinking state with animated dots.
 * Displayed during AI query processing and answer generation.
 */
export const Thinking: Story = {
  args: {
    state: 'thinking',
    size: 'md',
  },
};

/**
 * Confident state with sparkles.
 * Shows when AI confidence is high (≥85%) for the generated answer.
 */
export const Confident: Story = {
  args: {
    state: 'confident',
    size: 'md',
  },
};

/**
 * Searching state with rotating magnifying glass.
 * Indicates AI is searching through game manuals and knowledge base.
 */
export const Searching: Story = {
  args: {
    state: 'searching',
    size: 'md',
  },
};

/**
 * Uncertain state with question mark.
 * Displayed when AI confidence is low (<70%), warning user to verify.
 */
export const Uncertain: Story = {
  args: {
    state: 'uncertain',
    size: 'md',
  },
};

/**
 * Small size variant (32x32px).
 * Suitable for compact UI areas like message lists.
 */
export const SmallSize: Story = {
  args: {
    state: 'thinking',
    size: 'sm',
  },
};

/**
 * Medium size variant (40x40px) - Default.
 * Standard size for chat messages and AI responses.
 */
export const MediumSize: Story = {
  args: {
    state: 'confident',
    size: 'md',
  },
};

/**
 * Large size variant (48x48px).
 * Used for prominent UI areas like page headers or empty states.
 */
export const LargeSize: Story = {
  args: {
    state: 'searching',
    size: 'lg',
  },
};

/**
 * Showcase grid displaying all 5 states side-by-side.
 * Useful for visual regression testing and design review.
 */
export const AllStates: Story = {
  render: () => (
    <div className="grid grid-cols-5 gap-6 p-6 bg-muted/10 rounded-lg">
      <div className="flex flex-col items-center gap-2">
        <MeepleAvatar state="idle" size="lg" />
        <span className="text-sm font-medium">Idle</span>
        <span className="text-xs text-muted-foreground">Ready</span>
      </div>
      <div className="flex flex-col items-center gap-2">
        <MeepleAvatar state="thinking" size="lg" />
        <span className="text-sm font-medium">Thinking</span>
        <span className="text-xs text-muted-foreground">Processing</span>
      </div>
      <div className="flex flex-col items-center gap-2">
        <MeepleAvatar state="confident" size="lg" />
        <span className="text-sm font-medium">Confident</span>
        <span className="text-xs text-muted-foreground">≥85%</span>
      </div>
      <div className="flex flex-col items-center gap-2">
        <MeepleAvatar state="searching" size="lg" />
        <span className="text-sm font-medium">Searching</span>
        <span className="text-xs text-muted-foreground">Manuals</span>
      </div>
      <div className="flex flex-col items-center gap-2">
        <MeepleAvatar state="uncertain" size="lg" />
        <span className="text-sm font-medium">Uncertain</span>
        <span className="text-xs text-muted-foreground">&lt;70%</span>
      </div>
    </div>
  ),
  parameters: {
    docs: {
      description: {
        story: 'Visual comparison of all avatar states at large size.',
      },
    },
  },
};

/**
 * Size comparison showing all 3 size variants.
 */
export const AllSizes: Story = {
  render: () => (
    <div className="flex items-end gap-8 p-6 bg-muted/10 rounded-lg">
      <div className="flex flex-col items-center gap-2">
        <MeepleAvatar state="thinking" size="sm" />
        <span className="text-xs text-muted-foreground">Small (32px)</span>
      </div>
      <div className="flex flex-col items-center gap-2">
        <MeepleAvatar state="thinking" size="md" />
        <span className="text-xs text-muted-foreground">Medium (40px)</span>
      </div>
      <div className="flex flex-col items-center gap-2">
        <MeepleAvatar state="thinking" size="lg" />
        <span className="text-xs text-muted-foreground">Large (48px)</span>
      </div>
    </div>
  ),
  parameters: {
    docs: {
      description: {
        story: 'Comparison of all size variants with thinking state.',
      },
    },
  },
};

/**
 * Dark theme variant.
 * Avatar works well on dark backgrounds with good contrast.
 */
export const DarkTheme: Story = {
  args: {
    state: 'confident',
    size: 'lg',
  },
  parameters: {
    backgrounds: { default: 'dark' },
  },
  decorators: [
    Story => (
      <div className="dark p-8 bg-background">
        <Story />
      </div>
    ),
  ],
};

/**
 * Simulated chat message context.
 * Shows how avatar appears in actual chat UI.
 */
export const InChatContext: Story = {
  render: () => (
    <div className="max-w-2xl space-y-4">
      {/* AI Message - Confident */}
      <div className="flex gap-3 items-start">
        <MeepleAvatar state="confident" size="md" />
        <div className="flex-1 bg-muted p-4 rounded-lg">
          <p className="text-sm">
            Le risorse si piazzano sui territori adiacenti agli insediamenti...
          </p>
          <p className="text-xs text-muted-foreground mt-2">Confidence: 95%</p>
        </div>
      </div>

      {/* AI Message - Thinking */}
      <div className="flex gap-3 items-start">
        <MeepleAvatar state="thinking" size="md" />
        <div className="flex-1 bg-muted p-4 rounded-lg">
          <p className="text-sm text-muted-foreground italic">Thinking...</p>
        </div>
      </div>

      {/* AI Message - Uncertain */}
      <div className="flex gap-3 items-start">
        <MeepleAvatar state="uncertain" size="md" />
        <div className="flex-1 bg-muted p-4 rounded-lg">
          <p className="text-sm">
            I'm not entirely sure about this rule. Please check the manual...
          </p>
          <p className="text-xs text-muted-foreground mt-2">Confidence: 62%</p>
        </div>
      </div>
    </div>
  ),
  parameters: {
    docs: {
      description: {
        story: 'Avatar integrated into chat message layout.',
      },
    },
  },
};

/**
 * Accessibility test - reduced motion.
 * All animations should be disabled when user prefers reduced motion.
 */
export const ReducedMotion: Story = {
  render: () => (
    <div className="space-y-4">
      <p className="text-sm text-muted-foreground mb-4">
        Enable "Reduce motion" in your OS settings to test animation behavior.
      </p>
      <div className="grid grid-cols-3 gap-4">
        <div className="flex flex-col items-center gap-2">
          <MeepleAvatar state="thinking" size="md" />
          <span className="text-xs">Thinking</span>
        </div>
        <div className="flex flex-col items-center gap-2">
          <MeepleAvatar state="confident" size="md" />
          <span className="text-xs">Confident</span>
        </div>
        <div className="flex flex-col items-center gap-2">
          <MeepleAvatar state="searching" size="md" />
          <span className="text-xs">Searching</span>
        </div>
      </div>
    </div>
  ),
  parameters: {
    docs: {
      description: {
        story:
          'Test reduced motion accessibility - animations should stop when prefers-reduced-motion is enabled.',
      },
    },
    a11y: {
      config: {
        rules: [
          {
            id: 'color-contrast',
            enabled: true,
          },
        ],
      },
    },
  },
};
