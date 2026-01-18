/**
 * AgentModeSelector Stories - Issue #2413
 *
 * Visual testing for AgentModeSelector component.
 * Covers: all modes, disabled state, responsive layout, accessibility.
 */

import { useState } from 'react';

import { AgentModeSelector } from './AgentModeSelector';

import type { AgentMode } from './AgentModeSelector';
import type { Meta, StoryObj } from '@storybook/react';

const meta: Meta<typeof AgentModeSelector> = {
  title: 'Agent/AgentModeSelector',
  component: AgentModeSelector,
  parameters: {
    layout: 'centered',
  },
  decorators: [
    Story => (
      <div className="min-w-[800px] p-8">
        <Story />
      </div>
    ),
  ],
  tags: ['autodocs'],
};

export default meta;
type Story = StoryObj<typeof AgentModeSelector>;

/**
 * Default state with Rules Clarifier selected
 */
export const Default: Story = {
  args: {
    value: 'RulesClarifier',
    onChange: () => {},
  },
};

/**
 * Strategy Advisor mode selected
 */
export const StrategyAdvisorSelected: Story = {
  args: {
    value: 'StrategyAdvisor',
    onChange: () => {},
  },
};

/**
 * Setup Assistant mode selected
 */
export const SetupAssistantSelected: Story = {
  args: {
    value: 'SetupAssistant',
    onChange: () => {},
  },
};

/**
 * Disabled state - all modes non-interactive
 */
export const Disabled: Story = {
  args: {
    value: 'RulesClarifier',
    onChange: () => {},
    disabled: true,
  },
};

/**
 * Interactive example with state management
 */
export const Interactive: Story = {
  render: () => {
    const [mode, setMode] = useState<AgentMode>('RulesClarifier');

    return (
      <div className="space-y-4">
        <AgentModeSelector value={mode} onChange={setMode} />
        <div className="text-sm text-muted-foreground text-center">
          Current mode: <strong>{mode}</strong>
        </div>
      </div>
    );
  },
};

/**
 * Mobile responsive view (narrow viewport)
 */
export const MobileView: Story = {
  args: {
    value: 'RulesClarifier',
    onChange: () => {},
  },
  parameters: {
    viewport: {
      defaultViewport: 'mobile1',
    },
  },
  decorators: [
    Story => (
      <div className="max-w-[375px] p-4">
        <Story />
      </div>
    ),
  ],
};

/**
 * Dark mode variant
 */
export const DarkMode: Story = {
  args: {
    value: 'StrategyAdvisor',
    onChange: () => {},
  },
  parameters: {
    backgrounds: { default: 'dark' },
  },
  decorators: [
    Story => (
      <div className="dark min-w-[800px] p-8 bg-background text-foreground">
        <Story />
      </div>
    ),
  ],
};
