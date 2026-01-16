/**
 * QuickQuestionGenerator Stories
 * Issue #2417 (Sub-Issue 2401.1): UI for AI-powered QuickQuestion generation
 *
 * Stories covering:
 * - All generation states (idle, loading, success, error)
 * - Form validation (min/max length)
 * - Various prompt scenarios
 * - Responsive layouts
 * - Dark mode support
 * - Accessibility features
 */

import type { Meta, StoryObj } from '@storybook/react';
import { fn, userEvent, within, expect } from 'storybook/test';

import {
  QuickQuestionGenerator,
  type GeneratedQuestion,
} from './QuickQuestionGenerator';

// ==================== Mock Data ====================

const mockGeneratedQuestion: GeneratedQuestion = {
  id: 'q-123',
  question: 'What is the optimal strategy for resource trading in the mid-game phase of Catan?',
  answer:
    'In the mid-game, focus on trading for resources that give you the most flexibility. Prioritize wheat and ore if going for development cards, or brick and wood if expanding settlements.',
  category: 'Strategy',
  confidence: 0.92,
};

const mockSuccessHandler = async (prompt: string): Promise<GeneratedQuestion> => {
  // Simulate API delay
  await new Promise(resolve => setTimeout(resolve, 2500));
  return {
    ...mockGeneratedQuestion,
    question: `Generated question based on: "${prompt.substring(0, 50)}..."`,
  };
};

const mockErrorHandler = async (): Promise<GeneratedQuestion> => {
  await new Promise(resolve => setTimeout(resolve, 1500));
  throw new Error('Failed to generate question. The AI service is temporarily unavailable.');
};

const mockNetworkErrorHandler = async (): Promise<GeneratedQuestion> => {
  await new Promise(resolve => setTimeout(resolve, 1000));
  throw new Error('Network error. Please check your internet connection and try again.');
};

const mockRateLimitHandler = async (): Promise<GeneratedQuestion> => {
  await new Promise(resolve => setTimeout(resolve, 800));
  throw new Error('Rate limit exceeded. Please wait a moment before generating another question.');
};

// ==================== Meta ====================

const meta = {
  title: 'Components/AI/QuickQuestionGenerator',
  component: QuickQuestionGenerator,
  parameters: {
    layout: 'centered',
    docs: {
      description: {
        component:
          'AI-powered question generator with prompt input, validation, loading states, and error handling.',
      },
    },
    chromatic: {
      viewports: [375, 768, 1024],
    },
  },
  tags: ['autodocs'],
  argTypes: {
    minPromptLength: {
      control: { type: 'number', min: 1, max: 100 },
      description: 'Minimum prompt length required',
    },
    maxPromptLength: {
      control: { type: 'number', min: 100, max: 2000 },
      description: 'Maximum prompt length allowed',
    },
    disabled: {
      control: 'boolean',
      description: 'Disables the entire form',
    },
    initialPrompt: {
      control: 'text',
      description: 'Initial prompt value',
    },
    placeholder: {
      control: 'text',
      description: 'Placeholder text for textarea',
    },
  },
  args: {
    onGenerate: fn(mockSuccessHandler),
    onSuccess: fn(),
    onError: fn(),
    minPromptLength: 10,
    maxPromptLength: 500,
  },
  decorators: [
    Story => (
      <div className="w-full max-w-2xl p-4">
        <Story />
      </div>
    ),
  ],
} satisfies Meta<typeof QuickQuestionGenerator>;

export default meta;
type Story = StoryObj<typeof meta>;

// ==================== Basic States ====================

/**
 * Default idle state with empty prompt
 */
export const Default: Story = {
  args: {},
  parameters: {
    docs: {
      description: {
        story: 'Initial state with empty prompt, ready for user input.',
      },
    },
  },
};

/**
 * With pre-filled prompt text
 */
export const WithInitialPrompt: Story = {
  args: {
    initialPrompt:
      'Generate a strategy question about resource management in Settlers of Catan, focusing on early game development.',
  },
  parameters: {
    docs: {
      description: {
        story: 'Pre-filled with a valid prompt, ready to generate.',
      },
    },
  },
};

/**
 * Disabled state - form cannot be interacted with
 */
export const Disabled: Story = {
  args: {
    disabled: true,
    initialPrompt: 'This form is disabled and cannot be edited.',
  },
  parameters: {
    docs: {
      description: {
        story: 'Disabled state where all inputs and buttons are non-interactive.',
      },
    },
  },
};

// ==================== Loading States ====================

/**
 * Simulates loading state with progress indicator
 */
export const Loading: Story = {
  args: {
    initialPrompt: 'A complex strategy question about late-game tactics in Catan',
    onGenerate: async () => {
      // Never resolves to keep it in loading state
      await new Promise(() => {});
      return mockGeneratedQuestion;
    },
  },
  parameters: {
    docs: {
      description: {
        story:
          'Shows the loading state with progress indicator and status messages during generation.',
      },
    },
  },
  play: async ({ canvasElement }) => {
    const canvas = within(canvasElement);
    const submitButton = canvas.getByRole('button', { name: /generate question/i });
    await userEvent.click(submitButton);
  },
};

// ==================== Success States ====================

/**
 * Shows successful generation result
 */
export const Success: Story = {
  args: {
    initialPrompt: 'Generate a question about trading strategies',
    onGenerate: mockSuccessHandler,
  },
  parameters: {
    docs: {
      description: {
        story: 'Successful generation showing the question result with confidence score.',
      },
    },
  },
  play: async ({ canvasElement }) => {
    const canvas = within(canvasElement);
    const submitButton = canvas.getByRole('button', { name: /generate question/i });
    await userEvent.click(submitButton);
    // Wait for success state
    await expect(
      await canvas.findByText(/question generated/i, {}, { timeout: 5000 })
    ).toBeInTheDocument();
  },
};

/**
 * High confidence result
 */
export const HighConfidenceResult: Story = {
  args: {
    initialPrompt: 'Create a rules clarification question about robber placement',
    onGenerate: async () => {
      await new Promise(resolve => setTimeout(resolve, 1500));
      return { ...mockGeneratedQuestion, confidence: 0.98 };
    },
  },
  parameters: {
    docs: {
      description: {
        story: 'Generated question with high confidence score (98%).',
      },
    },
  },
  play: async ({ canvasElement }) => {
    const canvas = within(canvasElement);
    const submitButton = canvas.getByRole('button', { name: /generate question/i });
    await userEvent.click(submitButton);
    await expect(
      await canvas.findByText(/confidence: 98%/i, {}, { timeout: 5000 })
    ).toBeInTheDocument();
  },
};

// ==================== Error States ====================

/**
 * General API error
 */
export const Error: Story = {
  args: {
    initialPrompt: 'Generate a question that will fail',
    onGenerate: mockErrorHandler,
  },
  parameters: {
    docs: {
      description: {
        story: 'Error state when the AI service fails to generate a question.',
      },
    },
  },
  play: async ({ canvasElement }) => {
    const canvas = within(canvasElement);
    const submitButton = canvas.getByRole('button', { name: /generate question/i });
    await userEvent.click(submitButton);
    await expect(
      await canvas.findByText(/generation failed/i, {}, { timeout: 3000 })
    ).toBeInTheDocument();
  },
};

/**
 * Network error scenario
 */
export const NetworkError: Story = {
  args: {
    initialPrompt: 'This will simulate a network error',
    onGenerate: mockNetworkErrorHandler,
  },
  parameters: {
    docs: {
      description: {
        story: 'Network connectivity error with appropriate error message.',
      },
    },
  },
  play: async ({ canvasElement }) => {
    const canvas = within(canvasElement);
    const submitButton = canvas.getByRole('button', { name: /generate question/i });
    await userEvent.click(submitButton);
    await expect(
      await canvas.findByText(/network error/i, {}, { timeout: 3000 })
    ).toBeInTheDocument();
  },
};

/**
 * Rate limit exceeded
 */
export const RateLimitError: Story = {
  args: {
    initialPrompt: 'Too many requests scenario',
    onGenerate: mockRateLimitHandler,
  },
  parameters: {
    docs: {
      description: {
        story: 'Rate limit error when too many requests are made.',
      },
    },
  },
  play: async ({ canvasElement }) => {
    const canvas = within(canvasElement);
    const submitButton = canvas.getByRole('button', { name: /generate question/i });
    await userEvent.click(submitButton);
    await expect(
      await canvas.findByText(/rate limit/i, {}, { timeout: 3000 })
    ).toBeInTheDocument();
  },
};

// ==================== Validation States ====================

/**
 * Prompt too short
 */
export const PromptTooShort: Story = {
  args: {
    initialPrompt: 'Short',
    minPromptLength: 10,
  },
  parameters: {
    docs: {
      description: {
        story: 'Shows validation when prompt is below minimum length.',
      },
    },
  },
  play: async ({ canvasElement }) => {
    const canvas = within(canvasElement);
    const textarea = canvas.getByRole('textbox');
    await userEvent.clear(textarea);
    await userEvent.type(textarea, 'Short');
    const submitButton = canvas.getByRole('button', { name: /generate question/i });
    await userEvent.click(submitButton);
  },
};

/**
 * Prompt approaching max length
 */
export const PromptNearMaxLength: Story = {
  args: {
    initialPrompt:
      'This is a very long prompt that is approaching the maximum allowed length. It contains detailed instructions about what kind of question should be generated, including specific game scenarios, player counts, and strategic situations. The prompt continues with more context about the game state and what aspects of gameplay should be tested.',
    maxPromptLength: 300,
  },
  parameters: {
    docs: {
      description: {
        story: 'Prompt approaching the maximum character limit.',
      },
    },
  },
};

/**
 * Prompt exceeds max length
 */
export const PromptTooLong: Story = {
  args: {
    initialPrompt: 'A'.repeat(510),
    maxPromptLength: 500,
  },
  parameters: {
    docs: {
      description: {
        story: 'Shows validation when prompt exceeds maximum length.',
      },
    },
  },
};

/**
 * Empty prompt submission attempt
 */
export const EmptyPrompt: Story = {
  args: {
    initialPrompt: '',
  },
  parameters: {
    docs: {
      description: {
        story: 'Empty prompt state - Generate button should be disabled.',
      },
    },
  },
};

// ==================== Custom Configuration ====================

/**
 * Custom validation limits
 */
export const CustomValidationLimits: Story = {
  args: {
    minPromptLength: 20,
    maxPromptLength: 200,
    initialPrompt: 'A short prompt for testing.',
  },
  parameters: {
    docs: {
      description: {
        story: 'Custom min/max length validation (20-200 characters).',
      },
    },
  },
};

/**
 * Custom placeholder text
 */
export const CustomPlaceholder: Story = {
  args: {
    placeholder: 'Enter your custom question prompt here...',
  },
  parameters: {
    docs: {
      description: {
        story: 'Custom placeholder text in the textarea.',
      },
    },
  },
};

// ==================== Responsive ====================

/**
 * Mobile viewport
 */
export const Mobile: Story = {
  args: {
    initialPrompt: 'Generate a beginner-friendly question',
  },
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
  args: {
    initialPrompt: 'Generate a medium complexity strategy question',
  },
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
  args: {
    initialPrompt: 'Dark mode question generation',
  },
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
      <div className="dark w-full max-w-2xl p-4">
        <Story />
      </div>
    ),
  ],
};

/**
 * Dark mode with loading state
 */
export const DarkModeLoading: Story = {
  args: {
    initialPrompt: 'Loading in dark mode',
    onGenerate: async () => {
      await new Promise(() => {});
      return mockGeneratedQuestion;
    },
  },
  parameters: {
    backgrounds: { default: 'dark' },
    docs: {
      description: {
        story: 'Dark mode loading state.',
      },
    },
  },
  decorators: [
    Story => (
      <div className="dark w-full max-w-2xl p-4">
        <Story />
      </div>
    ),
  ],
  play: async ({ canvasElement }) => {
    const canvas = within(canvasElement);
    const submitButton = canvas.getByRole('button', { name: /generate question/i });
    await userEvent.click(submitButton);
  },
};

/**
 * Dark mode with error
 */
export const DarkModeError: Story = {
  args: {
    initialPrompt: 'Error in dark mode',
    onGenerate: mockErrorHandler,
  },
  parameters: {
    backgrounds: { default: 'dark' },
    docs: {
      description: {
        story: 'Dark mode error state.',
      },
    },
  },
  decorators: [
    Story => (
      <div className="dark w-full max-w-2xl p-4">
        <Story />
      </div>
    ),
  ],
  play: async ({ canvasElement }) => {
    const canvas = within(canvasElement);
    const submitButton = canvas.getByRole('button', { name: /generate question/i });
    await userEvent.click(submitButton);
    await expect(
      await canvas.findByText(/generation failed/i, {}, { timeout: 3000 })
    ).toBeInTheDocument();
  },
};

/**
 * Dark mode with success
 */
export const DarkModeSuccess: Story = {
  args: {
    initialPrompt: 'Success in dark mode',
    onGenerate: async () => {
      await new Promise(resolve => setTimeout(resolve, 1500));
      return mockGeneratedQuestion;
    },
  },
  parameters: {
    backgrounds: { default: 'dark' },
    docs: {
      description: {
        story: 'Dark mode success state.',
      },
    },
  },
  decorators: [
    Story => (
      <div className="dark w-full max-w-2xl p-4">
        <Story />
      </div>
    ),
  ],
  play: async ({ canvasElement }) => {
    const canvas = within(canvasElement);
    const submitButton = canvas.getByRole('button', { name: /generate question/i });
    await userEvent.click(submitButton);
    await expect(
      await canvas.findByText(/question generated/i, {}, { timeout: 5000 })
    ).toBeInTheDocument();
  },
};

// ==================== Edge Cases ====================

/**
 * Whitespace-only prompt
 */
export const WhitespacePrompt: Story = {
  args: {
    initialPrompt: '          ',
  },
  parameters: {
    docs: {
      description: {
        story: 'Whitespace-only prompt should fail validation.',
      },
    },
  },
};

/**
 * Very long error message
 */
export const LongErrorMessage: Story = {
  args: {
    initialPrompt: 'Trigger long error',
    onGenerate: async () => {
      await new Promise(resolve => setTimeout(resolve, 500));
      throw new Error(
        'This is a very long error message that provides detailed information about what went wrong during the question generation process. It includes technical details, suggestions for resolution, and contact information for support if the issue persists.'
      );
    },
  },
  parameters: {
    docs: {
      description: {
        story: 'Long error message display.',
      },
    },
  },
  play: async ({ canvasElement }) => {
    const canvas = within(canvasElement);
    const submitButton = canvas.getByRole('button', { name: /generate question/i });
    await userEvent.click(submitButton);
    await expect(
      await canvas.findByText(/generation failed/i, {}, { timeout: 3000 })
    ).toBeInTheDocument();
  },
};

/**
 * Fast generation (instant response)
 */
export const FastGeneration: Story = {
  args: {
    initialPrompt: 'Quick generation test',
    onGenerate: async () => {
      await new Promise(resolve => setTimeout(resolve, 200));
      return mockGeneratedQuestion;
    },
  },
  parameters: {
    docs: {
      description: {
        story: 'Very fast generation response.',
      },
    },
  },
  play: async ({ canvasElement }) => {
    const canvas = within(canvasElement);
    const submitButton = canvas.getByRole('button', { name: /generate question/i });
    await userEvent.click(submitButton);
    await expect(
      await canvas.findByText(/question generated/i, {}, { timeout: 2000 })
    ).toBeInTheDocument();
  },
};

// ==================== Accessibility ====================

/**
 * Focused state for keyboard navigation
 */
export const KeyboardFocused: Story = {
  args: {
    initialPrompt: 'Keyboard navigation test',
  },
  parameters: {
    docs: {
      description: {
        story: 'Demonstrates keyboard focus states for accessibility.',
      },
    },
  },
  play: async ({ canvasElement }) => {
    const canvas = within(canvasElement);
    const textarea = canvas.getByRole('textbox');
    await textarea.focus();
  },
};

/**
 * Screen reader announcement test
 */
export const ScreenReaderTest: Story = {
  args: {
    initialPrompt: 'Screen reader friendly content',
  },
  parameters: {
    docs: {
      description: {
        story:
          'Test story for verifying screen reader announcements and ARIA attributes. Progress and status changes are announced via aria-live regions.',
      },
    },
    a11y: {
      config: {
        rules: [
          { id: 'color-contrast', enabled: true },
          { id: 'label', enabled: true },
        ],
      },
    },
  },
};
