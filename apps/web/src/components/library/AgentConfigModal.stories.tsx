/**
 * AgentConfigModal Storybook Stories (Issue #2852, Phase 4)
 *
 * Component-level stories for AI agent configuration modal.
 * Tests model selection, sliders, radio groups, and form states.
 */

import { within, userEvent } from '@storybook/test';
import { fn } from '@storybook/test';
import { QueryClient, QueryClientProvider } from '@tanstack/react-query';

import { DEFAULT_AGENT_CONFIG } from '@/lib/api';

import { AgentConfigModal } from './AgentConfigModal';

import type { Meta, StoryObj } from '@storybook/react';

// Create QueryClient with mock agent config
const createMockQueryClient = (hasConfig = true) => {
  const queryClient = new QueryClient({
    defaultOptions: {
      queries: { retry: false, staleTime: Infinity },
    },
  });

  if (hasConfig) {
    queryClient.setQueryData(['agent-config', 'game-123'], {
      ...DEFAULT_AGENT_CONFIG,
      customInstructions: 'Spiega sempre le regole come se fossi principiante',
    });
  }

  return queryClient;
};

const meta: Meta<typeof AgentConfigModal> = {
  title: 'Components/Library/AgentConfigModal',
  component: AgentConfigModal,
  parameters: {
    layout: 'centered',
    chromatic: {
      viewports: [375, 768, 1920],
      delay: 300,
    },
  },
  tags: ['autodocs'],
  argTypes: {
    isOpen: {
      control: 'boolean',
      description: 'Modal open state',
    },
    gameTitle: {
      control: 'text',
      description: 'Game title for display',
    },
    onClose: { action: 'closed' },
  },
};

export default meta;
type Story = StoryObj<typeof meta>;

// ============================================================================
// Default States
// ============================================================================

export const Default: Story = {
  args: {
    isOpen: true,
    gameId: 'game-123',
    gameTitle: 'Azul',
    onClose: fn(),
  },
  decorators: [
    (Story) => (
      <QueryClientProvider client={createMockQueryClient(true)}>
        <Story />
      </QueryClientProvider>
    ),
  ],
};

export const NoExistingConfig: Story = {
  args: {
    isOpen: true,
    gameId: 'game-123',
    gameTitle: 'Wingspan',
    onClose: fn(),
  },
  decorators: [
    (Story) => (
      <QueryClientProvider client={createMockQueryClient(false)}>
        <Story />
      </QueryClientProvider>
    ),
  ],
};

export const LoadingConfig: Story = {
  args: {
    isOpen: true,
    gameId: 'game-123',
    gameTitle: 'Gloomhaven',
    onClose: fn(),
  },
  decorators: [
    (Story) => {
      const loadingClient = new QueryClient({
        defaultOptions: {
          queries: { retry: false, staleTime: Infinity },
        },
      });
      return (
        <QueryClientProvider client={loadingClient}>
          <Story />
        </QueryClientProvider>
      );
    },
  ],
};

// ============================================================================
// Model Selection
// ============================================================================

export const ModelDropdownOpen: Story = {
  args: {
    ...Default.args,
  },
  decorators: [
    (Story) => (
      <QueryClientProvider client={createMockQueryClient(true)}>
        <Story />
      </QueryClientProvider>
    ),
  ],
  play: async ({ canvasElement }) => {
    const canvas = within(canvasElement);
    const modelSelect = canvas.getByRole('combobox');

    await userEvent.click(modelSelect);
  },
};

// ============================================================================
// Temperature Slider
// ============================================================================

export const TemperatureHigh: Story = {
  args: {
    ...Default.args,
  },
  decorators: [
    (Story) => {
      const client = createMockQueryClient(true);
      client.setQueryData(['agent-config', 'game-123'], {
        ...DEFAULT_AGENT_CONFIG,
        temperature: 1.8,
      });
      return (
        <QueryClientProvider client={client}>
          <Story />
        </QueryClientProvider>
      );
    },
  ],
};

export const TemperatureLow: Story = {
  args: {
    ...Default.args,
  },
  decorators: [
    (Story) => {
      const client = createMockQueryClient(true);
      client.setQueryData(['agent-config', 'game-123'], {
        ...DEFAULT_AGENT_CONFIG,
        temperature: 0.2,
      });
      return (
        <QueryClientProvider client={client}>
          <Story />
        </QueryClientProvider>
      );
    },
  ],
};

// ============================================================================
// Personality Options
// ============================================================================

export const PersonalityFriendly: Story = {
  args: {
    ...Default.args,
  },
  decorators: [
    (Story) => {
      const client = createMockQueryClient(true);
      client.setQueryData(['agent-config', 'game-123'], {
        ...DEFAULT_AGENT_CONFIG,
        personality: 'friendly',
      });
      return (
        <QueryClientProvider client={client}>
          <Story />
        </QueryClientProvider>
      );
    },
  ],
};

export const PersonalityProfessional: Story = {
  args: {
    ...Default.args,
  },
  decorators: [
    (Story) => {
      const client = createMockQueryClient(true);
      client.setQueryData(['agent-config', 'game-123'], {
        ...DEFAULT_AGENT_CONFIG,
        personality: 'professional',
      });
      return (
        <QueryClientProvider client={client}>
          <Story />
        </QueryClientProvider>
      );
    },
  ],
};

export const PersonalityEnthusiastic: Story = {
  args: {
    ...Default.args,
  },
  decorators: [
    (Story) => {
      const client = createMockQueryClient(true);
      client.setQueryData(['agent-config', 'game-123'], {
        ...DEFAULT_AGENT_CONFIG,
        personality: 'enthusiastic',
      });
      return (
        <QueryClientProvider client={client}>
          <Story />
        </QueryClientProvider>
      );
    },
  ],
};

// ============================================================================
// Detail Levels
// ============================================================================

export const DetailBrief: Story = {
  args: {
    ...Default.args,
  },
  decorators: [
    (Story) => {
      const client = createMockQueryClient(true);
      client.setQueryData(['agent-config', 'game-123'], {
        ...DEFAULT_AGENT_CONFIG,
        detailLevel: 'brief',
      });
      return (
        <QueryClientProvider client={client}>
          <Story />
        </QueryClientProvider>
      );
    },
  ],
};

export const DetailDetailed: Story = {
  args: {
    ...Default.args,
  },
  decorators: [
    (Story) => {
      const client = createMockQueryClient(true);
      client.setQueryData(['agent-config', 'game-123'], {
        ...DEFAULT_AGENT_CONFIG,
        detailLevel: 'detailed',
      });
      return (
        <QueryClientProvider client={client}>
          <Story />
        </QueryClientProvider>
      );
    },
  ],
};

// ============================================================================
// Custom Instructions
// ============================================================================

export const WithCustomInstructions: Story = {
  args: {
    ...Default.args,
  },
  decorators: [
    (Story) => {
      const client = createMockQueryClient(true);
      client.setQueryData(['agent-config', 'game-123'], {
        ...DEFAULT_AGENT_CONFIG,
        customInstructions:
          'Spiega sempre le regole come se fossi principiante. Usa esempi pratici per chiarire concetti complessi.',
      });
      return (
        <QueryClientProvider client={client}>
          <Story />
        </QueryClientProvider>
      );
    },
  ],
};

export const CustomInstructionsNearLimit: Story = {
  args: {
    ...Default.args,
  },
  decorators: [
    (Story) => {
      const client = createMockQueryClient(true);
      client.setQueryData(['agent-config', 'game-123'], {
        ...DEFAULT_AGENT_CONFIG,
        customInstructions: 'A'.repeat(950), // 950 chars, 50 remaining
      });
      return (
        <QueryClientProvider client={client}>
          <Story />
        </QueryClientProvider>
      );
    },
  ],
};

// ============================================================================
// Interaction States
// ============================================================================

export const HoverResetButton: Story = {
  args: {
    ...Default.args,
  },
  decorators: [
    (Story) => (
      <QueryClientProvider client={createMockQueryClient(true)}>
        <Story />
      </QueryClientProvider>
    ),
  ],
  play: async ({ canvasElement }) => {
    const canvas = within(canvasElement);
    const resetButton = canvas.getByRole('button', { name: /Reset/i });

    await userEvent.hover(resetButton);
  },
};

export const HoverTestButton: Story = {
  args: {
    ...Default.args,
  },
  decorators: [
    (Story) => (
      <QueryClientProvider client={createMockQueryClient(true)}>
        <Story />
      </QueryClientProvider>
    ),
  ],
  play: async ({ canvasElement }) => {
    const canvas = within(canvasElement);
    const testButton = canvas.getByRole('button', { name: /Testa/i });

    await userEvent.hover(testButton);
  },
};

export const HoverSaveButton: Story = {
  args: {
    ...Default.args,
  },
  decorators: [
    (Story) => (
      <QueryClientProvider client={createMockQueryClient(true)}>
        <Story />
      </QueryClientProvider>
    ),
  ],
  play: async ({ canvasElement }) => {
    const canvas = within(canvasElement);
    const saveButton = canvas.getByRole('button', { name: /Salva Configurazione/i });

    await userEvent.hover(saveButton);
  },
};

// ============================================================================
// Responsive Layouts
// ============================================================================

export const Mobile: Story = {
  args: {
    ...Default.args,
  },
  decorators: [
    (Story) => (
      <QueryClientProvider client={createMockQueryClient(true)}>
        <Story />
      </QueryClientProvider>
    ),
  ],
  parameters: {
    viewport: {
      defaultViewport: 'mobile1',
    },
    chromatic: {
      viewports: [375],
    },
  },
};

export const Tablet: Story = {
  args: {
    ...Default.args,
  },
  decorators: [
    (Story) => (
      <QueryClientProvider client={createMockQueryClient(true)}>
        <Story />
      </QueryClientProvider>
    ),
  ],
  parameters: {
    viewport: {
      defaultViewport: 'tablet',
    },
    chromatic: {
      viewports: [768],
    },
  },
};

export const Desktop: Story = {
  args: {
    ...Default.args,
  },
  decorators: [
    (Story) => (
      <QueryClientProvider client={createMockQueryClient(true)}>
        <Story />
      </QueryClientProvider>
    ),
  ],
  parameters: {
    viewport: {
      defaultViewport: 'desktop',
    },
    chromatic: {
      viewports: [1920],
    },
  },
};
