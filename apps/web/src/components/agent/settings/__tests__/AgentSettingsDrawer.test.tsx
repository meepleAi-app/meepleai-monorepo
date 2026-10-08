/**
 * AgentSettingsDrawer Unit Tests
 * Issue #3250 (FRONT-014) — Rewritten for dynamic model API
 *
 * Tests:
 * - Renders models from API (mock useAvailableModels response)
 * - Free tier filters to free models only
 * - Premium tier shows all models
 * - Cost estimate displays for paid models
 * - Apply calls PATCH /api/v1/agents/{id}/configuration
 * - Name shown read-only (#4138: shared agent, renaming it would rename it for everyone)
 * - Reset restores defaults
 * - Cancel closes drawer
 */

import { render, screen, fireEvent, waitFor } from '@testing-library/react';
import userEvent from '@testing-library/user-event';
import { vi, describe, it, expect, beforeEach } from 'vitest';
import { QueryClient, QueryClientProvider } from '@tanstack/react-query';

import { AgentSettingsDrawer } from '../AgentSettingsDrawer';

// ============================================================================
// Mocks
// ============================================================================

// Mock sonner toast
vi.mock('sonner', () => ({
  toast: {
    success: vi.fn(),
    error: vi.fn(),
    info: vi.fn(),
  },
}));

// Mock models data
const FREE_MODELS = [
  {
    id: 'meta-llama/llama-3.3-70b-instruct:free',
    name: 'Llama 3.3 70B Free',
    provider: 'openrouter',
    tier: 'Free',
    costPer1kInputTokens: 0,
    costPer1kOutputTokens: 0,
    maxTokens: 8192,
    supportsStreaming: true,
    description: 'Free model for all users',
  },
  {
    id: 'deepseek/deepseek-chat:free',
    name: 'DeepSeek Chat Free',
    provider: 'openrouter',
    tier: 'Free',
    costPer1kInputTokens: 0,
    costPer1kOutputTokens: 0,
    maxTokens: 4096,
    supportsStreaming: true,
    description: 'Cost-effective free model',
  },
];

const PREMIUM_MODELS = [
  ...FREE_MODELS,
  {
    id: 'anthropic/claude-3.5-sonnet',
    name: 'Claude 3.5 Sonnet',
    provider: 'openrouter',
    tier: 'Premium',
    costPer1kInputTokens: 0.003,
    costPer1kOutputTokens: 0.015,
    maxTokens: 8192,
    supportsStreaming: true,
    description: 'Best quality responses',
  },
];

// #3394: config contract no longer exposes selectedDocumentIds (KB linking is a separate
// flow owned by the AgentConfiguration aggregate / updateSelectedDocuments endpoint).
const MOCK_AGENT_CONFIG = {
  id: 'config-1',
  agentId: 'agent-123',
  llmModel: 'meta-llama/llama-3.3-70b-instruct:free',
  llmProvider: 'openrouter',
  temperature: 0.3,
  maxTokens: 2048,
  isCurrent: true,
  createdAt: '2026-01-01T00:00:00Z',
};

// Mock the hooks
const mockPatchConfig = vi.fn();

vi.mock('@/hooks/queries/useModels', () => ({
  useAvailableModels: vi.fn(),
  useAgentConfiguration: vi.fn(),
  useUpdateAgentConfiguration: vi.fn(),
}));

// Issue #4138: no `@/lib/api` mock here any more. The drawer used to call
// api.agents.updateUserAgent to rename the agent; it no longer touches that
// client at all, so leaving the module unmocked means any regression that
// reintroduces the call fails loudly instead of being absorbed by a stub.

// Import mocked modules for dynamic control
import {
  useAvailableModels,
  useAgentConfiguration,
  useUpdateAgentConfiguration,
} from '@/hooks/queries/useModels';

const mockUseAvailableModels = vi.mocked(useAvailableModels);
const mockUseAgentConfiguration = vi.mocked(useAgentConfiguration);
const mockUseUpdateAgentConfiguration = vi.mocked(useUpdateAgentConfiguration);

// ============================================================================
// Helpers
// ============================================================================

const createWrapper = () => {
  const queryClient = new QueryClient({
    defaultOptions: {
      queries: { retry: false },
      mutations: { retry: false },
    },
  });

  return ({ children }: { children: React.ReactNode }) => (
    <QueryClientProvider client={queryClient}>{children}</QueryClientProvider>
  );
};

const defaultProps = {
  isOpen: true,
  onClose: vi.fn(),
  agentId: 'agent-123',
  agentName: 'Test Agent',
  userTier: 'free',
  onConfigUpdated: vi.fn(),
};

function setupMocks(options?: {
  models?: typeof FREE_MODELS;
  config?: typeof MOCK_AGENT_CONFIG | null;
  loading?: boolean;
}) {
  const models = options?.models ?? FREE_MODELS;
  const config = options?.config !== undefined ? options.config : MOCK_AGENT_CONFIG;
  const loading = options?.loading ?? false;

  mockUseAvailableModels.mockReturnValue({
    data: models,
    isLoading: loading,
    error: null,
    isError: false,
    isPending: loading,
    isSuccess: !loading,
    status: loading ? 'pending' : 'success',
    fetchStatus: 'idle',
  } as any);

  mockUseAgentConfiguration.mockReturnValue({
    data: config,
    isLoading: loading,
    error: null,
    isError: false,
    isPending: loading,
    isSuccess: !loading,
    status: loading ? 'pending' : 'success',
    fetchStatus: 'idle',
  } as any);

  mockPatchConfig.mockReset();
  mockUseUpdateAgentConfiguration.mockReturnValue({
    mutate: mockPatchConfig,
    isPending: false,
    isError: false,
    error: null,
    isSuccess: false,
    isIdle: true,
    status: 'idle',
    data: undefined,
    variables: undefined,
    reset: vi.fn(),
    mutateAsync: vi.fn(),
  } as any);
}

// ============================================================================
// Tests
// ============================================================================

describe('AgentSettingsDrawer', () => {
  beforeEach(() => {
    vi.clearAllMocks();
    setupMocks();
  });

  describe('Rendering', () => {
    it('should render drawer title', () => {
      render(<AgentSettingsDrawer {...defaultProps} />, { wrapper: createWrapper() });
      expect(screen.getByText('Impostazioni Agente')).toBeInTheDocument();
    });

    it('should render info banner about next query', () => {
      render(<AgentSettingsDrawer {...defaultProps} />, { wrapper: createWrapper() });
      expect(
        screen.getByText('Le modifiche si applicano alla prossima domanda')
      ).toBeInTheDocument();
    });

    it('should render model selector label', () => {
      render(<AgentSettingsDrawer {...defaultProps} />, { wrapper: createWrapper() });
      expect(screen.getByText('Modello AI')).toBeInTheDocument();
    });

    it('should render temperature slider with current value', () => {
      render(<AgentSettingsDrawer {...defaultProps} />, { wrapper: createWrapper() });
      expect(screen.getByText('Temperatura')).toBeInTheDocument();
      expect(screen.getByText('0.3')).toBeInTheDocument();
    });

    it('should render max tokens input', () => {
      render(<AgentSettingsDrawer {...defaultProps} />, { wrapper: createWrapper() });
      expect(screen.getByText('Max Tokens')).toBeInTheDocument();
      const maxTokensInput = screen.getByTestId('max-tokens-input');
      expect(maxTokensInput).toBeInTheDocument();
    });

    it('should render the agent name read-only', () => {
      render(<AgentSettingsDrawer {...defaultProps} />, { wrapper: createWrapper() });
      expect(screen.getByText('Agente')).toBeInTheDocument();
      expect(screen.getByTestId('agent-name-readonly')).toHaveTextContent('Test Agent');
    });

    it('should render action buttons', () => {
      render(<AgentSettingsDrawer {...defaultProps} />, { wrapper: createWrapper() });
      expect(screen.getByText('Reset')).toBeInTheDocument();
      expect(screen.getByText('Annulla')).toBeInTheDocument();
      expect(screen.getByText('Applica')).toBeInTheDocument();
    });

    it('should not render when isOpen is false', () => {
      render(<AgentSettingsDrawer {...defaultProps} isOpen={false} />, {
        wrapper: createWrapper(),
      });
      expect(screen.queryByText('Impostazioni Agente')).not.toBeInTheDocument();
    });

    it('should show loading spinner while data loads', () => {
      setupMocks({ loading: true });
      render(<AgentSettingsDrawer {...defaultProps} />, { wrapper: createWrapper() });
      // Should not show form fields when loading
      expect(screen.queryByText('Modello AI')).not.toBeInTheDocument();
    });
  });

  describe('Model Selector', () => {
    it('should render models from API for free tier', () => {
      setupMocks({ models: FREE_MODELS });
      render(<AgentSettingsDrawer {...defaultProps} userTier="free" />, {
        wrapper: createWrapper(),
      });

      // The model selector should be present
      const selector = screen.getByTestId('model-selector');
      expect(selector).toBeInTheDocument();
    });

    it('should render all models for premium tier', () => {
      setupMocks({ models: PREMIUM_MODELS });
      render(<AgentSettingsDrawer {...defaultProps} userTier="premium" />, {
        wrapper: createWrapper(),
      });

      const selector = screen.getByTestId('model-selector');
      expect(selector).toBeInTheDocument();
    });

    it('should show model description when model is selected', () => {
      setupMocks({ models: FREE_MODELS });
      render(<AgentSettingsDrawer {...defaultProps} />, { wrapper: createWrapper() });

      // The description of the default selected model should be visible
      expect(screen.getByText('Free model for all users')).toBeInTheDocument();
    });

    it('should show cost estimate for paid models', () => {
      setupMocks({
        models: PREMIUM_MODELS,
        config: {
          ...MOCK_AGENT_CONFIG,
          llmModel: 'anthropic/claude-3.5-sonnet',
        },
      });
      render(<AgentSettingsDrawer {...defaultProps} userTier="premium" />, {
        wrapper: createWrapper(),
      });

      // Cost = (0.003 * 2 + 0.015 * 1) = 0.021
      expect(screen.getByText(/~\$0\.0210\/query/)).toBeInTheDocument();
    });

    it('should not show cost estimate for free models', () => {
      setupMocks({ models: FREE_MODELS });
      render(<AgentSettingsDrawer {...defaultProps} />, { wrapper: createWrapper() });

      // No dollar sign cost badge for free models
      expect(screen.queryByText(/~\$/)).not.toBeInTheDocument();
    });
  });

  describe('Temperature Slider', () => {
    it('should display current temperature value', () => {
      render(<AgentSettingsDrawer {...defaultProps} />, { wrapper: createWrapper() });
      expect(screen.getByText('0.3')).toBeInTheDocument();
    });

    it('should render slider component', () => {
      render(<AgentSettingsDrawer {...defaultProps} />, { wrapper: createWrapper() });
      const slider = screen.getByRole('slider');
      expect(slider).toBeInTheDocument();
    });

    it('should show descriptive labels', () => {
      render(<AgentSettingsDrawer {...defaultProps} />, { wrapper: createWrapper() });
      expect(screen.getByText('Preciso')).toBeInTheDocument();
      expect(screen.getByText('Creativo')).toBeInTheDocument();
    });
  });

  describe('Actions', () => {
    it('should call onClose when cancel is clicked', async () => {
      const user = userEvent.setup();
      const onClose = vi.fn();

      render(<AgentSettingsDrawer {...defaultProps} onClose={onClose} />, {
        wrapper: createWrapper(),
      });

      const cancelButton = screen.getByText('Annulla');
      await user.click(cancelButton);

      expect(onClose).toHaveBeenCalledTimes(1);
    });

    it('should reset to defaults when reset is clicked', async () => {
      const user = userEvent.setup();
      const { toast } = await import('sonner');

      render(<AgentSettingsDrawer {...defaultProps} />, { wrapper: createWrapper() });

      const resetButton = screen.getByText('Reset');
      await user.click(resetButton);

      expect(toast.info).toHaveBeenCalledWith('Configurazione ripristinata ai valori predefiniti');
    });

    it('should call patchConfig when apply is clicked with changed values', async () => {
      const user = userEvent.setup();

      setupMocks();
      render(<AgentSettingsDrawer {...defaultProps} />, { wrapper: createWrapper() });

      // Change max tokens to trigger a diff using fireEvent (number inputs + userEvent.clear can be unreliable)
      const maxTokensInput = screen.getByTestId('max-tokens-input');
      fireEvent.change(maxTokensInput, { target: { value: '4096' } });

      const applyButton = screen.getByTestId('apply-config-btn');
      await user.click(applyButton);

      expect(mockPatchConfig).toHaveBeenCalledWith(
        expect.objectContaining({ maxTokens: 4096 }),
        expect.any(Object)
      );
    });

    it('should close drawer when apply is clicked with no config changes (name-only)', async () => {
      const user = userEvent.setup();
      const onClose = vi.fn();

      render(<AgentSettingsDrawer {...defaultProps} onClose={onClose} />, {
        wrapper: createWrapper(),
      });

      // Don't change anything — just click apply
      const applyButton = screen.getByTestId('apply-config-btn');
      await user.click(applyButton);

      // No config changes → should close
      expect(onClose).toHaveBeenCalled();
    });
  });

  // Issue #4138: the agent is system-wide and admin-managed, so a rename here
  // would rename it for every user. `should update name field when typing` is
  // gone with the editable input; the two cases below replace it — the name is
  // still shown, and it is no longer writable.
  describe('Agent Name', () => {
    it('should show the agent name', () => {
      render(<AgentSettingsDrawer {...defaultProps} agentName="My Agent" />, {
        wrapper: createWrapper(),
      });

      expect(screen.getByTestId('agent-name-readonly')).toHaveTextContent('My Agent');
    });

    it('should offer no way to edit the name', () => {
      render(<AgentSettingsDrawer {...defaultProps} agentName="My Agent" />, {
        wrapper: createWrapper(),
      });

      expect(screen.queryByTestId('agent-name-input')).not.toBeInTheDocument();
      expect(screen.queryByRole('textbox', { name: /nome/i })).not.toBeInTheDocument();
    });

    it('should omit the name block entirely when no name is given', () => {
      render(<AgentSettingsDrawer {...defaultProps} agentName="" />, {
        wrapper: createWrapper(),
      });

      expect(screen.queryByTestId('agent-name-readonly')).not.toBeInTheDocument();
    });
  });

  describe('Accessibility', () => {
    it('should have visible labels for all form sections', () => {
      render(<AgentSettingsDrawer {...defaultProps} />, { wrapper: createWrapper() });

      expect(screen.getByText('Modello AI')).toBeInTheDocument();
      expect(screen.getByText('Temperatura')).toBeInTheDocument();
      expect(screen.getByText('Max Tokens')).toBeInTheDocument();
      expect(screen.getByText('Agente')).toBeInTheDocument();
    });

    it('should have proper aria attributes on slider', () => {
      render(<AgentSettingsDrawer {...defaultProps} />, { wrapper: createWrapper() });

      const slider = screen.getByRole('slider');
      expect(slider).toHaveAttribute('aria-valuenow');
      expect(slider).toHaveAttribute('aria-valuemin');
      expect(slider).toHaveAttribute('aria-valuemax');
    });
  });
});
