/**
 * AgentConfigPanel Storybook Stories - Issue #2414 (Sub-Issue 2398.2)
 *
 * Visual testing for agent configuration panel across all modes.
 * Tests dynamic form adaptation, validation, and localStorage persistence.
 */

import { useState } from 'react';

import { AgentConfigPanel, type AgentConfig, type AgentMode } from './AgentConfigPanel';

import type { Meta, StoryObj } from '@storybook/react';

const meta: Meta<typeof AgentConfigPanel> = {
  title: 'Admin/Agents/AgentConfigPanel',
  component: AgentConfigPanel,
  parameters: {
    layout: 'padded',
    docs: {
      description: {
        component:
          'Dynamic configuration panel for agent modes. Adapts form fields based on selected mode (Chat, Player, Ledger). ' +
          'Features confidence threshold slider, mode-specific settings, advanced options toggle, and localStorage persistence.',
      },
    },
  },
  tags: ['autodocs'],
  argTypes: {
    mode: {
      control: 'select',
      options: ['Chat', 'Player', 'Ledger'],
      description: 'Agent mode determines available configuration options',
    },
    config: {
      control: 'object',
      description: 'Current configuration (mode-specific)',
    },
    onConfigChange: {
      action: 'config-changed',
      description: 'Callback when configuration changes',
    },
    disabled: {
      control: 'boolean',
      description: 'Disable all form inputs',
    },
    className: {
      control: 'text',
      description: 'Optional CSS classes',
    },
  },
};

export default meta;
type Story = StoryObj<typeof meta>;

/**
 * Interactive wrapper for state management
 */
function InteractiveWrapper(props: {
  mode: AgentMode;
  initialConfig?: AgentConfig;
  disabled?: boolean;
  children: (
    config: AgentConfig | undefined,
    setConfig: (config: AgentConfig) => void
  ) => React.ReactNode;
}) {
  const [config, setConfig] = useState<AgentConfig | undefined>(props.initialConfig);
  return <>{props.children(config, setConfig)}</>;
}

// ============================================================================
// Chat Mode Stories
// ============================================================================

/**
 * Chat Mode: Default configuration
 */
export const ChatModeDefault: Story = {
  args: {
    mode: 'Chat',
    disabled: false,
  },
  render: args => (
    <InteractiveWrapper mode="Chat">
      {(config, setConfig) => (
        <AgentConfigPanel
          {...args}
          mode="Chat"
          config={config}
          onConfigChange={c => {
            setConfig(c);
            args.onConfigChange?.(c);
          }}
        />
      )}
    </InteractiveWrapper>
  ),
};

/**
 * Chat Mode: High confidence threshold
 */
export const ChatModeHighConfidence: Story = {
  args: {
    mode: 'Chat',
    config: {
      mode: 'Chat',
      confidenceThreshold: 0.95,
      temperature: 0.7,
      maxTokens: 2048,
      useContextWindow: true,
      enableRuleLookup: true,
      maxHistoryMessages: 10,
    },
    disabled: false,
  },
  render: args => (
    <InteractiveWrapper mode="Chat" initialConfig={args.config}>
      {(config, setConfig) => (
        <AgentConfigPanel
          {...args}
          mode="Chat"
          config={config}
          onConfigChange={c => {
            setConfig(c);
            args.onConfigChange?.(c);
          }}
        />
      )}
    </InteractiveWrapper>
  ),
};

/**
 * Chat Mode: Minimal history, no rule lookup
 */
export const ChatModeMinimal: Story = {
  args: {
    mode: 'Chat',
    config: {
      mode: 'Chat',
      confidenceThreshold: 0.5,
      temperature: 1.0,
      maxTokens: 512,
      useContextWindow: false,
      enableRuleLookup: false,
      maxHistoryMessages: 3,
    },
    disabled: false,
  },
  render: args => (
    <InteractiveWrapper mode="Chat" initialConfig={args.config}>
      {(config, setConfig) => (
        <AgentConfigPanel
          {...args}
          mode="Chat"
          config={config}
          onConfigChange={c => {
            setConfig(c);
            args.onConfigChange?.(c);
          }}
        />
      )}
    </InteractiveWrapper>
  ),
};

// ============================================================================
// Player Mode Stories
// ============================================================================

/**
 * Player Mode: Default configuration
 */
export const PlayerModeDefault: Story = {
  args: {
    mode: 'Player',
    disabled: false,
  },
  render: args => (
    <InteractiveWrapper mode="Player">
      {(config, setConfig) => (
        <AgentConfigPanel
          {...args}
          mode="Player"
          config={config}
          onConfigChange={c => {
            setConfig(c);
            args.onConfigChange?.(c);
          }}
        />
      )}
    </InteractiveWrapper>
  ),
};

/**
 * Player Mode: Deep analysis with alternatives
 */
export const PlayerModeDeepAnalysis: Story = {
  args: {
    mode: 'Player',
    config: {
      mode: 'Player',
      confidenceThreshold: 0.9,
      temperature: 0.3,
      maxTokens: 2048,
      useContextWindow: true,
      suggestionDepth: 7,
      evaluateRisks: true,
      showAlternatives: true,
    },
    disabled: false,
  },
  render: args => (
    <InteractiveWrapper mode="Player" initialConfig={args.config}>
      {(config, setConfig) => (
        <AgentConfigPanel
          {...args}
          mode="Player"
          config={config}
          onConfigChange={c => {
            setConfig(c);
            args.onConfigChange?.(c);
          }}
        />
      )}
    </InteractiveWrapper>
  ),
};

/**
 * Player Mode: Quick suggestions, no risk eval
 */
export const PlayerModeQuick: Story = {
  args: {
    mode: 'Player',
    config: {
      mode: 'Player',
      confidenceThreshold: 0.6,
      temperature: 0.8,
      maxTokens: 512,
      useContextWindow: true,
      suggestionDepth: 1,
      evaluateRisks: false,
      showAlternatives: false,
    },
    disabled: false,
  },
  render: args => (
    <InteractiveWrapper mode="Player" initialConfig={args.config}>
      {(config, setConfig) => (
        <AgentConfigPanel
          {...args}
          mode="Player"
          config={config}
          onConfigChange={c => {
            setConfig(c);
            args.onConfigChange?.(c);
          }}
        />
      )}
    </InteractiveWrapper>
  ),
};

// ============================================================================
// Ledger Mode Stories
// ============================================================================

/**
 * Ledger Mode: Default configuration
 */
export const LedgerModeDefault: Story = {
  args: {
    mode: 'Ledger',
    disabled: false,
  },
  render: args => (
    <InteractiveWrapper mode="Ledger">
      {(config, setConfig) => (
        <AgentConfigPanel
          {...args}
          mode="Ledger"
          config={config}
          onConfigChange={c => {
            setConfig(c);
            args.onConfigChange?.(c);
          }}
        />
      )}
    </InteractiveWrapper>
  ),
};

/**
 * Ledger Mode: Full tracking with time travel
 */
export const LedgerModeFullTracking: Story = {
  args: {
    mode: 'Ledger',
    config: {
      mode: 'Ledger',
      confidenceThreshold: 0.95,
      temperature: 0.2,
      maxTokens: 8192,
      useContextWindow: true,
      trackPlayerActions: true,
      trackResourceChanges: true,
      enableTimeTravel: true,
    },
    disabled: false,
  },
  render: args => (
    <InteractiveWrapper mode="Ledger" initialConfig={args.config}>
      {(config, setConfig) => (
        <AgentConfigPanel
          {...args}
          mode="Ledger"
          config={config}
          onConfigChange={c => {
            setConfig(c);
            args.onConfigChange?.(c);
          }}
        />
      )}
    </InteractiveWrapper>
  ),
};

/**
 * Ledger Mode: Actions only, no resources
 */
export const LedgerModeActionsOnly: Story = {
  args: {
    mode: 'Ledger',
    config: {
      mode: 'Ledger',
      confidenceThreshold: 0.8,
      temperature: 0.5,
      maxTokens: 2048,
      useContextWindow: true,
      trackPlayerActions: true,
      trackResourceChanges: false,
      enableTimeTravel: false,
    },
    disabled: false,
  },
  render: args => (
    <InteractiveWrapper mode="Ledger" initialConfig={args.config}>
      {(config, setConfig) => (
        <AgentConfigPanel
          {...args}
          mode="Ledger"
          config={config}
          onConfigChange={c => {
            setConfig(c);
            args.onConfigChange?.(c);
          }}
        />
      )}
    </InteractiveWrapper>
  ),
};

// ============================================================================
// State Stories
// ============================================================================

/**
 * Disabled state (all inputs locked)
 */
export const DisabledState: Story = {
  args: {
    mode: 'Chat',
    config: {
      mode: 'Chat',
      confidenceThreshold: 0.8,
      temperature: 0.7,
      maxTokens: 2048,
      useContextWindow: true,
      enableRuleLookup: true,
      maxHistoryMessages: 10,
    },
    disabled: true,
  },
  render: args => (
    <InteractiveWrapper mode="Chat" initialConfig={args.config}>
      {(config, setConfig) => (
        <AgentConfigPanel
          {...args}
          mode="Chat"
          config={config}
          onConfigChange={c => {
            setConfig(c);
            args.onConfigChange?.(c);
          }}
        />
      )}
    </InteractiveWrapper>
  ),
};

// ============================================================================
// Responsive Stories
// ============================================================================

/**
 * Mobile viewport (responsive test)
 */
export const Mobile: Story = {
  args: {
    mode: 'Player',
    config: {
      mode: 'Player',
      confidenceThreshold: 0.8,
      temperature: 0.5,
      maxTokens: 1024,
      useContextWindow: true,
      suggestionDepth: 3,
      evaluateRisks: true,
      showAlternatives: true,
    },
    disabled: false,
  },
  parameters: {
    viewport: {
      defaultViewport: 'mobile1',
    },
  },
  render: args => (
    <InteractiveWrapper mode="Player" initialConfig={args.config}>
      {(config, setConfig) => (
        <AgentConfigPanel
          {...args}
          mode="Player"
          config={config}
          onConfigChange={c => {
            setConfig(c);
            args.onConfigChange?.(c);
          }}
        />
      )}
    </InteractiveWrapper>
  ),
};

/**
 * Dark mode (theme test)
 */
export const DarkMode: Story = {
  args: {
    mode: 'Ledger',
    config: {
      mode: 'Ledger',
      confidenceThreshold: 0.9,
      temperature: 0.3,
      maxTokens: 4096,
      useContextWindow: true,
      trackPlayerActions: true,
      trackResourceChanges: true,
      enableTimeTravel: true,
    },
    disabled: false,
  },
  parameters: {
    backgrounds: { default: 'dark' },
  },
  render: args => (
    <div className="dark">
      <InteractiveWrapper mode="Ledger" initialConfig={args.config}>
        {(config, setConfig) => (
          <AgentConfigPanel
            {...args}
            mode="Ledger"
            config={config}
            onConfigChange={c => {
              setConfig(c);
              args.onConfigChange?.(c);
            }}
          />
        )}
      </InteractiveWrapper>
    </div>
  ),
};
