import type { Meta, StoryObj } from '@storybook/react';
import { MessageAnimator } from './MessageAnimator';

/**
 * MessageAnimator component for animating chat messages with direction-specific slide-in effects.
 * - 'left': AI messages sliding from left (x: -20)
 * - 'right': User messages sliding from right (x: 20)
 * Respects user's reduced motion preferences.
 */
const meta = {
  title: 'Loading/MessageAnimator',
  component: MessageAnimator,
  parameters: {
    layout: 'padded',
  },
  tags: ['autodocs'],
  argTypes: {
    direction: {
      control: 'select',
      options: ['left', 'right'],
      description: 'Direction of slide animation',
    },
    delay: {
      control: { type: 'number', min: 0, max: 2, step: 0.1 },
      description: 'Animation delay in seconds',
    },
    id: {
      control: 'text',
      description: 'Unique message identifier',
    },
  },
} satisfies Meta<typeof MessageAnimator>;

export default meta;
type Story = StoryObj<typeof meta>;

/**
 * AI message sliding from left
 */
export const AIMessage: Story = {
  args: {
    direction: 'left',
    id: 'msg-ai-1',
    delay: 0,
    children: (
      <div className="bg-slate-100 dark:bg-slate-800 rounded-2xl p-4 max-w-md">
        <div className="flex items-start space-x-3">
          <div className="w-8 h-8 bg-blue-500 rounded-full flex items-center justify-center text-white font-bold">
            AI
          </div>
          <div>
            <p className="text-sm">Hello! How can I help you understand the rules of your board game today?</p>
          </div>
        </div>
      </div>
    ),
  },
};

/**
 * User message sliding from right
 */
export const UserMessage: Story = {
  args: {
    direction: 'right',
    id: 'msg-user-1',
    delay: 0,
    children: (
      <div className="bg-blue-500 rounded-2xl p-4 max-w-md ml-auto">
        <div className="flex items-start space-x-3">
          <div className="flex-1">
            <p className="text-sm text-white">How do I set up Gloomhaven for the first scenario?</p>
          </div>
          <div className="w-8 h-8 bg-white rounded-full flex items-center justify-center text-blue-500 font-bold">
            U
          </div>
        </div>
      </div>
    ),
  },
};

/**
 * Message with delay
 */
export const WithDelay: Story = {
  args: {
    direction: 'left',
    id: 'msg-delayed',
    delay: 0.5,
    children: (
      <div className="bg-slate-100 dark:bg-slate-800 rounded-2xl p-4 max-w-md">
        <p className="text-sm">This message appears after a 0.5s delay</p>
      </div>
    ),
  },
};

/**
 * Conversation thread example
 */
export const ConversationThread: Story = {
  render: () => (
    <div className="space-y-3 max-w-2xl">
      <MessageAnimator direction="left" id="conv-1" delay={0}>
        <div className="bg-slate-100 dark:bg-slate-800 rounded-2xl p-4 max-w-md">
          <div className="flex items-start space-x-3">
            <div className="w-8 h-8 bg-blue-500 rounded-full flex items-center justify-center text-white text-xs font-bold">
              AI
            </div>
            <p className="text-sm">Welcome! What game are you playing?</p>
          </div>
        </div>
      </MessageAnimator>

      <MessageAnimator direction="right" id="conv-2" delay={0.2}>
        <div className="bg-blue-500 rounded-2xl p-4 max-w-md ml-auto">
          <div className="flex items-start space-x-3 justify-end">
            <p className="text-sm text-white">I'm playing Wingspan</p>
            <div className="w-8 h-8 bg-white rounded-full flex items-center justify-center text-blue-500 text-xs font-bold">
              U
            </div>
          </div>
        </div>
      </MessageAnimator>

      <MessageAnimator direction="left" id="conv-3" delay={0.4}>
        <div className="bg-slate-100 dark:bg-slate-800 rounded-2xl p-4 max-w-md">
          <div className="flex items-start space-x-3">
            <div className="w-8 h-8 bg-blue-500 rounded-full flex items-center justify-center text-white text-xs font-bold">
              AI
            </div>
            <p className="text-sm">Great choice! What would you like to know about Wingspan?</p>
          </div>
        </div>
      </MessageAnimator>

      <MessageAnimator direction="right" id="conv-4" delay={0.6}>
        <div className="bg-blue-500 rounded-2xl p-4 max-w-md ml-auto">
          <div className="flex items-start space-x-3 justify-end">
            <p className="text-sm text-white">How do bird powers work?</p>
            <div className="w-8 h-8 bg-white rounded-full flex items-center justify-center text-blue-500 text-xs font-bold">
              U
            </div>
          </div>
        </div>
      </MessageAnimator>
    </div>
  ),
};

/**
 * Long message content
 */
export const LongMessage: Story = {
  args: {
    direction: 'left',
    id: 'msg-long',
    delay: 0,
    children: (
      <div className="bg-slate-100 dark:bg-slate-800 rounded-2xl p-4 max-w-2xl">
        <div className="flex items-start space-x-3">
          <div className="w-8 h-8 bg-blue-500 rounded-full flex items-center justify-center text-white text-xs font-bold">
            AI
          </div>
          <div className="space-y-2 text-sm">
            <p>Here's how to set up Gloomhaven for the first scenario:</p>
            <ol className="list-decimal list-inside space-y-1">
              <li>Place the scenario map tiles according to the scenario book</li>
              <li>Set up monsters as indicated on the map</li>
              <li>Each player chooses a character and takes their character mat</li>
              <li>Shuffle your ability deck and draw your hand</li>
              <li>Set your health and experience tokens to starting values</li>
            </ol>
            <p>Would you like more details on any of these steps?</p>
          </div>
        </div>
      </div>
    ),
  },
};

/**
 * Staggered messages demonstrating delays
 */
export const StaggeredMessages: Story = {
  render: () => {
    const messages = [
      { direction: 'left' as const, delay: 0, text: 'First message' },
      { direction: 'left' as const, delay: 0.2, text: 'Second message' },
      { direction: 'left' as const, delay: 0.4, text: 'Third message' },
      { direction: 'left' as const, delay: 0.6, text: 'Fourth message' },
    ];

    return (
      <div className="space-y-2 max-w-md">
        {messages.map((msg, idx) => (
          <MessageAnimator
            key={`stagger-${idx}`}
            direction={msg.direction}
            id={`stagger-${idx}`}
            delay={msg.delay}
          >
            <div className="bg-slate-100 dark:bg-slate-800 rounded-2xl p-3">
              <p className="text-sm">{msg.text}</p>
            </div>
          </MessageAnimator>
        ))}
      </div>
    );
  },
};

/**
 * Minimal message example
 */
export const Minimal: Story = {
  args: {
    direction: 'left',
    id: 'msg-minimal',
    delay: 0,
    children: (
      <div className="bg-slate-200 dark:bg-slate-700 rounded p-2">
        <p className="text-xs">Quick message</p>
      </div>
    ),
  },
};
