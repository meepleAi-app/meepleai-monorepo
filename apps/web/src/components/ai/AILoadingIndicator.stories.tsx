/**
 * AILoadingIndicator Stories
 * Issue #2418 (Sub-Issue 2401.2): Visual loading states for AI operations
 *
 * Stories covering:
 * - All loading variants (progress, skeleton, time estimation)
 * - Various time durations (fast, normal, slow operations)
 * - Cancellation functionality
 * - Timeout handling
 * - Skeleton variants (question, answer, card, compact)
 * - Responsive layouts
 * - Dark mode support
 * - Accessibility features (reduced motion)
 */

import { useEffect, useState } from 'react';

import type { Meta, StoryObj } from '@storybook/react';
import { fn, userEvent, within, expect, waitFor } from 'storybook/test';

import { AILoadingIndicator, type LoadingStage } from './AILoadingIndicator';

// ==================== Mock Data ====================

const defaultStages: LoadingStage[] = [
  { progress: 10, message: 'Initializing...', duration: 500 },
  { progress: 30, message: 'Analyzing input...', duration: 1000 },
  { progress: 50, message: 'Processing with AI...', duration: 2000 },
  { progress: 75, message: 'Generating response...', duration: 1500 },
  { progress: 90, message: 'Finalizing...', duration: 500 },
];

const quickStages: LoadingStage[] = [
  { progress: 25, message: 'Quick processing...', duration: 250 },
  { progress: 50, message: 'Almost there...', duration: 250 },
  { progress: 75, message: 'Finalizing...', duration: 250 },
];

const longStages: LoadingStage[] = [
  { progress: 10, message: 'Starting comprehensive analysis...', duration: 2000 },
  { progress: 25, message: 'Loading AI models...', duration: 3000 },
  { progress: 40, message: 'Processing document structure...', duration: 4000 },
  { progress: 55, message: 'Extracting key information...', duration: 3000 },
  { progress: 70, message: 'Generating detailed response...', duration: 5000 },
  { progress: 85, message: 'Validating output...', duration: 2000 },
  { progress: 95, message: 'Final review...', duration: 1000 },
];

// ==================== Meta ====================

const meta = {
  title: 'Components/AI/AILoadingIndicator',
  component: AILoadingIndicator,
  parameters: {
    layout: 'centered',
    docs: {
      description: {
        component:
          'Visual loading indicator for AI operations with progress tracking, skeleton loaders, estimated time remaining, and cancellation support.',
      },
    },
    chromatic: {
      viewports: [375, 768, 1024],
    },
  },
  tags: ['autodocs'],
  argTypes: {
    isLoading: {
      control: 'boolean',
      description: 'Whether the loading indicator is active',
    },
    progress: {
      control: { type: 'range', min: 0, max: 100, step: 1 },
      description: 'Manual progress value (0-100)',
    },
    estimatedTotalTime: {
      control: { type: 'number', min: 1000, max: 60000, step: 1000 },
      description: 'Estimated total operation time in ms',
    },
    stage: {
      control: 'text',
      description: 'Current stage message',
    },
    showCancelButton: {
      control: 'boolean',
      description: 'Whether to show the cancel button',
    },
    cancelLabel: {
      control: 'text',
      description: 'Label for the cancel button',
    },
    showSkeleton: {
      control: 'boolean',
      description: 'Whether to show skeleton loader',
    },
    skeletonVariant: {
      control: { type: 'select' },
      options: ['question', 'answer', 'card', 'compact'],
      description: 'Skeleton layout variant',
    },
    timeout: {
      control: { type: 'number', min: 5000, max: 120000, step: 5000 },
      description: 'Timeout duration in ms',
    },
    showTimeRemaining: {
      control: 'boolean',
      description: 'Whether to show estimated time remaining',
    },
  },
  args: {
    isLoading: true,
    estimatedTotalTime: 5000,
    showCancelButton: true,
    showSkeleton: true,
    skeletonVariant: 'question',
    showTimeRemaining: true,
    timeout: 30000,
    onCancel: fn(),
    onTimeout: fn(),
  },
  decorators: [
    Story => (
      <div className="w-full max-w-md p-4">
        <Story />
      </div>
    ),
  ],
} satisfies Meta<typeof AILoadingIndicator>;

export default meta;
type Story = StoryObj<typeof meta>;

// ==================== Basic States ====================

/**
 * Default loading state with all features
 */
export const Default: Story = {
  args: {},
  parameters: {
    docs: {
      description: {
        story:
          'Default loading indicator with auto-progress, skeleton loader, time remaining, and cancel button.',
      },
    },
  },
};

/**
 * Loading indicator without any content (isLoading: false)
 */
export const NotLoading: Story = {
  args: {
    isLoading: false,
  },
  parameters: {
    docs: {
      description: {
        story: 'When isLoading is false, the component renders nothing.',
      },
    },
  },
};

// ==================== Progress Variations ====================

/**
 * Manual progress control
 */
export const ManualProgress: Story = {
  args: {
    progress: 65,
    stage: 'Custom progress at 65%',
    estimatedTotalTime: undefined,
  },
  parameters: {
    docs: {
      description: {
        story: 'Progress controlled manually via the progress prop. Time estimation is disabled.',
      },
    },
  },
};

/**
 * Progress at different stages
 */
export const ProgressStages: Story = {
  render: () => {
    const [currentProgress, setCurrentProgress] = useState(0);
    const stages = ['Starting...', 'Processing...', 'Almost done...', 'Finalizing...'];
    const stageIndex = Math.floor(currentProgress / 25);

    useEffect(() => {
      const interval = setInterval(() => {
        setCurrentProgress(prev => {
          if (prev >= 100) {
            clearInterval(interval);
            return 100;
          }
          return prev + 5;
        });
      }, 500);
      return () => clearInterval(interval);
    }, []);

    return (
      <AILoadingIndicator
        isLoading={currentProgress < 100}
        progress={currentProgress}
        stage={stages[Math.min(stageIndex, stages.length - 1)]}
        showTimeRemaining={false}
      />
    );
  },
  parameters: {
    docs: {
      description: {
        story: 'Demonstrates progress advancing through different stages.',
      },
    },
  },
};

// ==================== Time Variations ====================

/**
 * Fast operation (1 second)
 */
export const FastOperation: Story = {
  args: {
    estimatedTotalTime: 1000,
    stages: quickStages,
  },
  parameters: {
    docs: {
      description: {
        story: 'Fast AI operation completing in ~1 second.',
      },
    },
  },
};

/**
 * Normal operation (5 seconds)
 */
export const NormalOperation: Story = {
  args: {
    estimatedTotalTime: 5000,
    stages: defaultStages,
  },
  parameters: {
    docs: {
      description: {
        story: 'Standard AI operation completing in ~5 seconds.',
      },
    },
  },
};

/**
 * Slow operation (20 seconds)
 */
export const SlowOperation: Story = {
  args: {
    estimatedTotalTime: 20000,
    stages: longStages,
  },
  parameters: {
    docs: {
      description: {
        story: 'Long-running AI operation completing in ~20 seconds.',
      },
    },
  },
};

/**
 * Very slow operation (60 seconds)
 */
export const VerySlowOperation: Story = {
  args: {
    estimatedTotalTime: 60000,
    stages: [
      { progress: 5, message: 'Starting extensive analysis...', duration: 5000 },
      { progress: 20, message: 'Loading comprehensive models...', duration: 10000 },
      { progress: 40, message: 'Deep document processing...', duration: 15000 },
      { progress: 60, message: 'Cross-referencing data...', duration: 15000 },
      { progress: 80, message: 'Generating detailed report...', duration: 10000 },
      { progress: 95, message: 'Final validation...', duration: 5000 },
    ],
  },
  parameters: {
    docs: {
      description: {
        story: 'Very long operation showing minute-based time remaining.',
      },
    },
  },
};

// ==================== Skeleton Variants ====================

/**
 * Question skeleton variant
 */
export const SkeletonQuestion: Story = {
  args: {
    skeletonVariant: 'question',
    stage: 'Generating question...',
  },
  parameters: {
    docs: {
      description: {
        story: 'Skeleton matching QuickQuestion component layout.',
      },
    },
  },
};

/**
 * Answer skeleton variant
 */
export const SkeletonAnswer: Story = {
  args: {
    skeletonVariant: 'answer',
    stage: 'Generating detailed answer...',
  },
  parameters: {
    docs: {
      description: {
        story: 'Skeleton matching answer/response layout with more content lines.',
      },
    },
  },
};

/**
 * Card skeleton variant
 */
export const SkeletonCard: Story = {
  args: {
    skeletonVariant: 'card',
    stage: 'Loading card content...',
  },
  parameters: {
    docs: {
      description: {
        story: 'Skeleton matching card component with avatar and content.',
      },
    },
  },
};

/**
 * Compact skeleton variant
 */
export const SkeletonCompact: Story = {
  args: {
    skeletonVariant: 'compact',
    stage: 'Quick loading...',
  },
  parameters: {
    docs: {
      description: {
        story: 'Minimal skeleton for inline or compact loading states.',
      },
    },
  },
};

/**
 * No skeleton (progress only)
 */
export const NoSkeleton: Story = {
  args: {
    showSkeleton: false,
    stage: 'Processing without skeleton...',
  },
  parameters: {
    docs: {
      description: {
        story: 'Loading indicator without skeleton, showing only progress bar.',
      },
    },
  },
};

// ==================== Cancellation ====================

/**
 * With cancel button
 */
export const WithCancelButton: Story = {
  args: {
    showCancelButton: true,
    onCancel: fn(),
  },
  parameters: {
    docs: {
      description: {
        story: 'Loading indicator with visible cancel button.',
      },
    },
  },
};

/**
 * Custom cancel label
 */
export const CustomCancelLabel: Story = {
  args: {
    showCancelButton: true,
    cancelLabel: 'Stop Generation',
    onCancel: fn(),
  },
  parameters: {
    docs: {
      description: {
        story: 'Cancel button with custom label text.',
      },
    },
  },
};

/**
 * Without cancel button
 */
export const WithoutCancelButton: Story = {
  args: {
    showCancelButton: false,
  },
  parameters: {
    docs: {
      description: {
        story: 'Loading indicator without cancel button (non-cancellable operation).',
      },
    },
  },
};

/**
 * Cancel button interaction
 */
export const CancelInteraction: Story = {
  args: {
    showCancelButton: true,
    onCancel: fn(),
  },
  parameters: {
    docs: {
      description: {
        story: 'Demonstrates cancel button click interaction.',
      },
    },
  },
  play: async ({ canvasElement, args }) => {
    const canvas = within(canvasElement);
    const cancelButton = await canvas.findByRole('button', { name: /cancel/i });
    await userEvent.click(cancelButton);
    await waitFor(() => {
      expect(args.onCancel).toHaveBeenCalled();
    });
  },
};

// ==================== Timeout Handling ====================

/**
 * Short timeout (5 seconds)
 */
export const ShortTimeout: Story = {
  args: {
    timeout: 5000,
    estimatedTotalTime: 10000,
    onTimeout: fn(),
  },
  parameters: {
    docs: {
      description: {
        story:
          'Operation with 5-second timeout. If operation takes longer, onTimeout is called.',
      },
    },
  },
};

/**
 * Timeout triggered
 */
export const TimeoutTriggered: Story = {
  render: () => {
    const [isLoading, setIsLoading] = useState(true);
    const [timedOut, setTimedOut] = useState(false);

    const handleTimeout = () => {
      setIsLoading(false);
      setTimedOut(true);
    };

    return (
      <div className="space-y-4">
        <AILoadingIndicator
          isLoading={isLoading}
          timeout={3000}
          estimatedTotalTime={10000}
          onTimeout={handleTimeout}
          stage="This operation will timeout..."
        />
        {timedOut && (
          <div className="p-4 bg-destructive/10 text-destructive rounded-lg">
            Operation timed out after 3 seconds
          </div>
        )}
      </div>
    );
  },
  parameters: {
    docs: {
      description: {
        story: 'Demonstrates timeout being triggered after 3 seconds.',
      },
    },
  },
};

// ==================== Time Remaining Display ====================

/**
 * With time remaining
 */
export const WithTimeRemaining: Story = {
  args: {
    showTimeRemaining: true,
    estimatedTotalTime: 10000,
  },
  parameters: {
    docs: {
      description: {
        story: 'Shows estimated time remaining during operation.',
      },
    },
  },
};

/**
 * Without time remaining
 */
export const WithoutTimeRemaining: Story = {
  args: {
    showTimeRemaining: false,
  },
  parameters: {
    docs: {
      description: {
        story: 'Hides the estimated time remaining display.',
      },
    },
  },
};

// ==================== Custom Stages ====================

/**
 * Custom stage messages
 */
export const CustomStages: Story = {
  args: {
    stages: [
      { progress: 15, message: 'Warming up neural networks...', duration: 1000 },
      { progress: 35, message: 'Consulting the oracle...', duration: 1500 },
      { progress: 55, message: 'Contemplating existence...', duration: 1500 },
      { progress: 75, message: 'Formulating wisdom...', duration: 1500 },
      { progress: 95, message: 'Polishing response...', duration: 500 },
    ],
  },
  parameters: {
    docs: {
      description: {
        story: 'Custom stage messages for themed or playful loading experience.',
      },
    },
  },
};

// ==================== Responsive ====================

/**
 * Mobile viewport
 */
export const Mobile: Story = {
  args: {},
  parameters: {
    viewport: {
      defaultViewport: 'mobile1',
    },
    docs: {
      description: {
        story: 'Mobile viewport layout (320px width).',
      },
    },
  },
};

/**
 * Tablet viewport
 */
export const Tablet: Story = {
  args: {},
  parameters: {
    viewport: {
      defaultViewport: 'tablet',
    },
    docs: {
      description: {
        story: 'Tablet viewport layout (768px width).',
      },
    },
  },
};

// ==================== Dark Mode ====================

/**
 * Dark mode appearance
 */
export const DarkMode: Story = {
  args: {},
  parameters: {
    backgrounds: { default: 'dark' },
    docs: {
      description: {
        story: 'Dark mode appearance.',
      },
    },
  },
  decorators: [
    Story => (
      <div className="dark w-full max-w-md p-4 bg-background">
        <Story />
      </div>
    ),
  ],
};

/**
 * Dark mode with all skeleton variants
 */
export const DarkModeSkeletons: Story = {
  render: () => (
    <div className="space-y-8">
      <div>
        <p className="text-sm text-muted-foreground mb-2">Question variant</p>
        <AILoadingIndicator
          isLoading
          skeletonVariant="question"
          stage="Question skeleton..."
        />
      </div>
      <div>
        <p className="text-sm text-muted-foreground mb-2">Answer variant</p>
        <AILoadingIndicator
          isLoading
          skeletonVariant="answer"
          stage="Answer skeleton..."
        />
      </div>
      <div>
        <p className="text-sm text-muted-foreground mb-2">Card variant</p>
        <AILoadingIndicator
          isLoading
          skeletonVariant="card"
          stage="Card skeleton..."
        />
      </div>
      <div>
        <p className="text-sm text-muted-foreground mb-2">Compact variant</p>
        <AILoadingIndicator
          isLoading
          skeletonVariant="compact"
          stage="Compact skeleton..."
        />
      </div>
    </div>
  ),
  parameters: {
    backgrounds: { default: 'dark' },
    docs: {
      description: {
        story: 'All skeleton variants in dark mode.',
      },
    },
  },
  decorators: [
    Story => (
      <div className="dark w-full max-w-md p-4 bg-background">
        <Story />
      </div>
    ),
  ],
};

// ==================== Accessibility ====================

/**
 * Reduced motion preference
 */
export const ReducedMotion: Story = {
  args: {},
  parameters: {
    docs: {
      description: {
        story:
          'Respects prefers-reduced-motion. When enabled, animations are disabled while maintaining visual feedback.',
      },
    },
    // Note: To test reduced motion, use system preferences or browser devtools
  },
};

/**
 * Screen reader announcements
 */
export const ScreenReaderTest: Story = {
  args: {
    ariaLabel: 'AI question generation in progress',
    stage: 'Generating your question...',
  },
  parameters: {
    docs: {
      description: {
        story:
          'Test story for verifying screen reader announcements. Uses aria-live regions for progress updates.',
      },
    },
    a11y: {
      config: {
        rules: [
          { id: 'color-contrast', enabled: true },
          { id: 'aria-live', enabled: true },
        ],
      },
    },
  },
};

/**
 * Keyboard navigation
 */
export const KeyboardNavigation: Story = {
  args: {
    showCancelButton: true,
  },
  parameters: {
    docs: {
      description: {
        story: 'Cancel button is focusable via keyboard navigation.',
      },
    },
  },
  play: async ({ canvasElement }) => {
    const canvas = within(canvasElement);
    const cancelButton = await canvas.findByRole('button', { name: /cancel/i });
    cancelButton.focus();
    await expect(document.activeElement).toBe(cancelButton);
  },
};

// ==================== Integration Examples ====================

/**
 * Integration with QuickQuestion workflow
 */
export const QuickQuestionWorkflow: Story = {
  render: () => {
    const [isGenerating, setIsGenerating] = useState(false);
    const [result, setResult] = useState<string | null>(null);

    const handleGenerate = () => {
      setIsGenerating(true);
      setResult(null);
      setTimeout(() => {
        setIsGenerating(false);
        setResult('What is the optimal strategy for early game resource collection in Catan?');
      }, 4000);
    };

    const handleCancel = () => {
      setIsGenerating(false);
    };

    return (
      <div className="space-y-4">
        <button
          onClick={handleGenerate}
          disabled={isGenerating}
          className="px-4 py-2 bg-primary text-primary-foreground rounded-md disabled:opacity-50"
        >
          {isGenerating ? 'Generating...' : 'Generate Question'}
        </button>

        {isGenerating && (
          <AILoadingIndicator
            isLoading
            estimatedTotalTime={4000}
            skeletonVariant="question"
            onCancel={handleCancel}
            stages={[
              { progress: 20, message: 'Analyzing game context...' },
              { progress: 50, message: 'Generating question...' },
              { progress: 80, message: 'Creating answer...' },
            ]}
          />
        )}

        {result && (
          <div className="p-4 bg-green-50 dark:bg-green-950/20 border border-green-200 dark:border-green-800 rounded-lg">
            <p className="text-green-700 dark:text-green-300">{result}</p>
          </div>
        )}
      </div>
    );
  },
  parameters: {
    docs: {
      description: {
        story: 'Complete workflow: trigger generation, show loading, display result.',
      },
    },
  },
};

/**
 * Multiple concurrent indicators
 */
export const ConcurrentOperations: Story = {
  render: () => (
    <div className="space-y-6">
      <div>
        <p className="text-sm font-medium mb-2">Operation 1: Fast</p>
        <AILoadingIndicator
          isLoading
          estimatedTotalTime={2000}
          skeletonVariant="compact"
          stage="Quick task..."
          showCancelButton={false}
        />
      </div>
      <div>
        <p className="text-sm font-medium mb-2">Operation 2: Medium</p>
        <AILoadingIndicator
          isLoading
          estimatedTotalTime={5000}
          skeletonVariant="question"
          stage="Standard task..."
        />
      </div>
      <div>
        <p className="text-sm font-medium mb-2">Operation 3: Slow</p>
        <AILoadingIndicator
          isLoading
          estimatedTotalTime={15000}
          skeletonVariant="answer"
          stage="Complex task..."
        />
      </div>
    </div>
  ),
  parameters: {
    docs: {
      description: {
        story: 'Multiple loading indicators running concurrently with different durations.',
      },
    },
  },
};
