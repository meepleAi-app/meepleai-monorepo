import type { Meta, StoryObj } from '@storybook/react';
import { TypingIndicator } from './TypingIndicator';
import React from 'react';

/**
 * TypingIndicator component shows animated typing indicator for chat interfaces.
 * Shows 3 bouncing dots with staggered animation.
 * Respects user's reduced motion preferences.
 */
const meta = {
  title: 'Loading/TypingIndicator',
  component: TypingIndicator,
  parameters: {
    layout: 'centered',
  },
  tags: ['autodocs'],
  argTypes: {
    visible: {
      control: 'boolean',
      description: 'Whether the indicator is visible',
    },
    agentName: {
      control: 'text',
      description: 'Name of the agent that is typing (for accessibility)',
    },
  },
} satisfies Meta<typeof TypingIndicator>;

export default meta;
type Story = StoryObj<typeof meta>;

/**
 * Default typing indicator
 */
export const Default: Story = {
  args: {
    visible: true,
    agentName: 'AI Assistant',
  },
};

/**
 * Hidden typing indicator
 */
export const Hidden: Story = {
  args: {
    visible: false,
    agentName: 'AI Assistant',
  },
};

/**
 * Different agent name
 */
export const CustomAgent: Story = {
  args: {
    visible: true,
    agentName: 'Gloomhaven Expert',
  },
};

/**
 * Interactive toggle demo
 */
const InteractiveTypingComponent = () => {
  const [isTyping, setIsTyping] = React.useState(false);

  return (
    <div className="space-y-4">
      <button
        onClick={() => setIsTyping(!isTyping)}
        className="px-4 py-2 bg-blue-500 text-white rounded hover:bg-blue-600"
      >
        {isTyping ? 'Stop Typing' : 'Start Typing'}
      </button>
      <TypingIndicator visible={isTyping} agentName="AI Assistant" />
    </div>
  );
};

export const Interactive: Story = {
  render: () => <InteractiveTypingComponent />,
};

/**
 * In chat message context
 */
export const InChatContext: Story = {
  render: () => (
    <div className="max-w-md space-y-3">
      {/* Previous messages */}
      <div className="flex items-start space-x-3">
        <div className="w-8 h-8 bg-blue-500 rounded-full flex items-center justify-center text-white text-xs font-bold">
          AI
        </div>
        <div className="bg-slate-100 dark:bg-slate-800 rounded-2xl p-3 max-w-xs">
          <p className="text-sm">How can I help you?</p>
        </div>
      </div>

      <div className="flex items-start space-x-3 justify-end">
        <div className="bg-blue-500 rounded-2xl p-3 max-w-xs">
          <p className="text-sm text-white">Tell me about Wingspan</p>
        </div>
        <div className="w-8 h-8 bg-white border-2 border-blue-500 rounded-full flex items-center justify-center text-blue-500 text-xs font-bold">
          U
        </div>
      </div>

      {/* Typing indicator */}
      <div className="flex items-start space-x-3">
        <div className="w-8 h-8 bg-blue-500 rounded-full flex items-center justify-center text-white text-xs font-bold">
          AI
        </div>
        <TypingIndicator visible={true} agentName="AI Assistant" />
      </div>
    </div>
  ),
};

/**
 * Auto-appear and disappear simulation
 */
const AutoSimulationComponent = () => {
  const [isTyping, setIsTyping] = React.useState(true);
  const [showMessage, setShowMessage] = React.useState(false);

  React.useEffect(() => {
    // Simulate typing for 3 seconds, then show message
    const timer = setTimeout(() => {
      setIsTyping(false);
      setShowMessage(true);
    }, 3000);

    return () => clearTimeout(timer);
  }, []);

  return (
    <div className="max-w-md space-y-3">
      <div className="flex items-start space-x-3">
        <div className="w-8 h-8 bg-blue-500 rounded-full flex items-center justify-center text-white text-xs font-bold">
          AI
        </div>
        {isTyping ? (
          <TypingIndicator visible={true} agentName="AI Assistant" />
        ) : showMessage ? (
          <div className="bg-slate-100 dark:bg-slate-800 rounded-2xl p-3 max-w-xs">
            <p className="text-sm">Here is the information about Wingspan...</p>
          </div>
        ) : null}
      </div>
    </div>
  );
};

export const AutoSimulation: Story = {
  render: () => <AutoSimulationComponent />,
};

/**
 * Multiple agents typing
 */
export const MultipleAgents: Story = {
  render: () => (
    <div className="max-w-md space-y-3">
      <div className="flex items-start space-x-3">
        <div className="w-8 h-8 bg-blue-500 rounded-full flex items-center justify-center text-white text-xs font-bold">
          A1
        </div>
        <TypingIndicator visible={true} agentName="Rules Expert" />
      </div>

      <div className="flex items-start space-x-3">
        <div className="w-8 h-8 bg-purple-500 rounded-full flex items-center justify-center text-white text-xs font-bold">
          A2
        </div>
        <TypingIndicator visible={true} agentName="Strategy Guide" />
      </div>
    </div>
  ),
};

/**
 * Dark mode example
 */
export const DarkMode: Story = {
  parameters: {
    backgrounds: { default: 'dark' },
  },
  decorators: [
    (Story) => (
      <div className="dark">
        <Story />
      </div>
    ),
  ],
  args: {
    visible: true,
    agentName: 'AI Assistant',
  },
};

/**
 * With custom className
 */
export const CustomStyling: Story = {
  args: {
    visible: true,
    agentName: 'AI Assistant',
    className: 'border-2 border-blue-500',
  },
};