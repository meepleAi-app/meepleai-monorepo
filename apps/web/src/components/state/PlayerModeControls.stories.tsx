/**
 * PlayerModeControls - Issue #2421
 *
 * UI controls for Player Mode AI suggestions.
 * Features:
 * - Suggest Move button
 * - Confidence meter
 * - Suggestion display with alternatives
 * - Apply/Ignore actions
 */

import { fn } from 'storybook/test';

import { PlayerModeControls } from './PlayerModeControls';

import type { Meta, StoryObj } from '@storybook/react';

// Mock game state for stories
const mockGameState = {
  players: [
    { id: '1', name: 'Alice', score: 15, resources: { wood: 3, stone: 2 } },
    { id: '2', name: 'Bob', score: 12, resources: { wood: 1, stone: 4 } },
  ],
  currentTurn: 1,
  phase: 'resource-gathering',
};

const meta = {
  title: 'State/PlayerModeControls',
  component: PlayerModeControls,
  parameters: {
    layout: 'padded',
    chromatic: {
      viewports: [768, 1024, 1280],
      delay: 1000, // Allow time for animations
    },
  },
  tags: ['autodocs'],
  decorators: [
    Story => (
      <div className="max-w-4xl mx-auto p-6 space-y-6">
        <div className="rounded-lg border bg-card p-4">
          <h2 className="text-lg font-semibold mb-2">Mock Game State</h2>
          <pre className="text-xs text-muted-foreground overflow-auto">
            {JSON.stringify(mockGameState, null, 2)}
          </pre>
        </div>
        <Story />
      </div>
    ),
  ],
  argTypes: {
    gameId: { control: 'text' },
    gameState: { control: 'object' },
    query: { control: 'text' },
    readonly: { control: 'boolean' },
    onSuggestionApplied: { action: 'suggestion applied' },
    onSuggestionIgnored: { action: 'suggestion ignored' },
  },
  args: {
    gameId: '123e4567-e89b-12d3-a456-426614174000',
    gameState: mockGameState,
    onSuggestionApplied: fn(),
    onSuggestionIgnored: fn(),
    readonly: false,
  },
} satisfies Meta<typeof PlayerModeControls>;

export default meta;
type Story = StoryObj<typeof meta>;

/**
 * NoSuggestion - Initial state with no AI suggestion
 *
 * Shows:
 * - Suggest Move button ready
 * - Empty state message
 * - No suggestion panel
 */
export const NoSuggestion: Story = {
  args: {
    query: '',
  },
};

/**
 * WithQuery - Initial state with user query context
 *
 * Shows:
 * - Pre-filled query context
 * - Ready to request suggestion
 */
export const WithQuery: Story = {
  args: {
    query: 'Should I focus on collecting wood or stone?',
  },
};

/**
 * Loading - AI is processing the suggestion request
 *
 * Note: This shows the loading button state.
 * Actual loading with hook requires backend integration.
 */
export const Loading: Story = {
  args: {
    query: 'What should I do next?',
  },
  parameters: {
    docs: {
      description: {
        story:
          'Loading state when AI is processing. In production, this triggers when suggestMove() is called.',
      },
    },
  },
};

/**
 * LowConfidence - AI suggestion with low confidence (<50%)
 *
 * Shows:
 * - Red confidence meter
 * - Suggestion with caveat
 * - "Basso" confidence label
 *
 * Note: This story demonstrates the UI. Actual data requires backend mock.
 */
export const LowConfidence: Story = {
  args: {
    query: 'Which resource should I prioritize?',
  },
  parameters: {
    docs: {
      description: {
        story:
          'Low confidence suggestion (< 50%). Red meter indicates uncertainty. Real data requires backend mock or API integration.',
      },
    },
  },
};

/**
 * MediumConfidence - AI suggestion with medium confidence (50-80%)
 *
 * Shows:
 * - Yellow confidence meter
 * - Balanced suggestion
 * - "Medio" confidence label
 *
 * Note: This story demonstrates the UI. Actual data requires backend mock.
 */
export const MediumConfidence: Story = {
  args: {
    query: 'What move should I make?',
  },
  parameters: {
    docs: {
      description: {
        story:
          'Medium confidence suggestion (50-80%). Yellow meter indicates moderate certainty. Real data requires backend mock or API integration.',
      },
    },
  },
};

/**
 * HighConfidence - AI suggestion with high confidence (>80%)
 *
 * Shows:
 * - Green confidence meter
 * - Strong recommendation
 * - "Alto" confidence label
 *
 * Note: This story demonstrates the UI. Actual data requires backend mock.
 */
export const HighConfidence: Story = {
  args: {
    query: 'Best move for my current position?',
  },
  parameters: {
    docs: {
      description: {
        story:
          'High confidence suggestion (> 80%). Green meter indicates strong certainty. Real data requires backend mock or API integration.',
      },
    },
  },
};

/**
 * WithAlternatives - Suggestion with alternative moves
 *
 * Shows:
 * - Primary suggestion
 * - 2-3 alternative moves
 * - Individual confidence per alternative
 *
 * Note: This story demonstrates the UI. Actual data requires backend mock.
 */
export const WithAlternatives: Story = {
  args: {
    query: 'What are my options?',
  },
  parameters: {
    docs: {
      description: {
        story:
          'Suggestion with alternatives. Shows multiple strategic options with individual confidence scores. Real data requires backend mock or API integration.',
      },
    },
  },
};

/**
 * WithStrategicContext - Suggestion with strategic advice
 *
 * Shows:
 * - Primary suggestion
 * - Strategic context panel
 * - High-level strategic advice
 *
 * Note: This story demonstrates the UI. Actual data requires backend mock.
 */
export const WithStrategicContext: Story = {
  args: {
    query: 'What should I focus on long-term?',
  },
  parameters: {
    docs: {
      description: {
        story:
          'Suggestion with strategic context. Provides both immediate action and long-term strategic advice. Real data requires backend mock or API integration.',
      },
    },
  },
};

/**
 * CompleteExample - Full suggestion with all features
 *
 * Shows:
 * - High confidence meter
 * - Primary suggestion with expected outcome
 * - Alternative moves
 * - Strategic context
 * - Action buttons
 *
 * Note: This story demonstrates the UI. Actual data requires backend mock.
 */
export const CompleteExample: Story = {
  args: {
    query: 'Full analysis please',
  },
  parameters: {
    docs: {
      description: {
        story:
          'Complete example showing all features: confidence meter, primary suggestion, alternatives, strategic context, and actions. Real data requires backend mock or API integration.',
      },
    },
  },
};

/**
 * Error - API error state
 *
 * Shows:
 * - Error card with message
 * - Red border highlighting error
 * - Destructive styling
 *
 * Note: This story demonstrates error UI. Actual errors occur on backend failure.
 */
export const ErrorState: Story = {
  args: {
    query: 'This will trigger an error',
  },
  parameters: {
    docs: {
      description: {
        story:
          'Error state when AI suggestion fails. Shows user-friendly error message. In production, errors occur when backend is unavailable or returns error response.',
      },
    },
  },
};

/**
 * ReadonlyMode - View-only mode with disabled interactions
 *
 * Shows:
 * - Disabled Suggest Move button
 * - Disabled Apply/Ignore buttons
 * - Suggestion visible but not actionable
 *
 * Note: Readonly mode prevents any user interaction.
 */
export const ReadonlyMode: Story = {
  args: {
    query: 'Readonly mode example',
    readonly: true,
  },
};

/**
 * EmptyGameState - Edge case with minimal game state
 *
 * Shows:
 * - Component handles empty/minimal state
 * - No crashes with missing data
 */
export const EmptyGameState: Story = {
  args: {
    gameState: {},
    query: 'Can AI handle empty state?',
  },
  parameters: {
    docs: {
      description: {
        story:
          'Edge case: Component behavior with empty/minimal game state. Tests robustness and error handling.',
      },
    },
  },
};

/**
 * LongQuery - User query with very long text
 *
 * Shows:
 * - UI handles long query text gracefully
 * - No layout breaks
 */
export const LongQuery: Story = {
  args: {
    query:
      'I have been playing this game for a while and I am wondering what the absolute best strategic move would be considering my current resources, my opponent positions, the phase of the game we are in, and the overall winning conditions that need to be met. Can you provide a comprehensive analysis with multiple options?',
  },
  parameters: {
    docs: {
      description: {
        story: 'Edge case: Long user query text. Tests UI layout and text handling.',
      },
    },
  },
};
