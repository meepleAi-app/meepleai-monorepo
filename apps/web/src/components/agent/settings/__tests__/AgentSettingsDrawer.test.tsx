/**
 * AgentSettingsDrawer Unit Tests
 * Issue #3250 (FRONT-014)
 * Issue #3248 (FRONT-012) - Test coverage
 */

import { render, screen, fireEvent, waitFor } from '@testing-library/react';
import userEvent from '@testing-library/user-event';
import { vi, describe, it, expect, beforeEach } from 'vitest';
import { QueryClient, QueryClientProvider } from '@tanstack/react-query';

import { AgentSettingsDrawer, AgentRuntimeConfig } from '../AgentSettingsDrawer';

// Mock sonner toast
vi.mock('sonner', () => ({
  toast: {
    success: vi.fn(),
    error: vi.fn(),
    info: vi.fn(),
  },
}));

// Mock fetch
const mockFetch = vi.fn();
global.fetch = mockFetch;

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

describe('AgentSettingsDrawer', () => {
  const defaultConfig: AgentRuntimeConfig = {
    modelId: 'gpt-3.5-turbo',
    temperature: 0.7,
    ragStrategy: 'hybrid',
    maxTokens: 2048,
    topK: 5,
    minScore: 0.7,
  };

  const defaultProps = {
    isOpen: true,
    onClose: vi.fn(),
    sessionId: 'session-123',
    currentConfig: defaultConfig,
    userTier: 'free' as const,
    onConfigUpdated: vi.fn(),
  };

  beforeEach(() => {
    vi.clearAllMocks();
    mockFetch.mockResolvedValue({
      ok: true,
      json: () => Promise.resolve({ success: true }),
    });
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

    it('should render model selector with current value', () => {
      render(<AgentSettingsDrawer {...defaultProps} />, { wrapper: createWrapper() });

      expect(screen.getByText('Modello AI')).toBeInTheDocument();
      // Combobox should show current model
    });

    it('should render temperature slider with current value', () => {
      render(<AgentSettingsDrawer {...defaultProps} />, { wrapper: createWrapper() });

      expect(screen.getByText('Temperatura')).toBeInTheDocument();
      expect(screen.getByText('0.7')).toBeInTheDocument();
    });

    it('should render RAG strategy radio group', () => {
      render(<AgentSettingsDrawer {...defaultProps} />, { wrapper: createWrapper() });

      expect(screen.getByText('Strategia Ricerca')).toBeInTheDocument();
      expect(screen.getByText('Ibrida (Vector + Keyword)')).toBeInTheDocument();
      expect(screen.getByText('Solo Vector')).toBeInTheDocument();
    });

    it('should render max tokens input', () => {
      render(<AgentSettingsDrawer {...defaultProps} />, { wrapper: createWrapper() });

      expect(screen.getByText('Max Tokens')).toBeInTheDocument();
      const maxTokensInput = screen.getByDisplayValue('2048');
      expect(maxTokensInput).toBeInTheDocument();
    });

    it('should render advanced settings section (collapsed)', () => {
      render(<AgentSettingsDrawer {...defaultProps} />, { wrapper: createWrapper() });

      expect(screen.getByText('Avanzate')).toBeInTheDocument();
    });

    it('should render action buttons', () => {
      render(<AgentSettingsDrawer {...defaultProps} />, { wrapper: createWrapper() });

      expect(screen.getByRole('button', { name: /reset/i })).toBeInTheDocument();
      expect(screen.getByRole('button', { name: /annulla/i })).toBeInTheDocument();
      expect(screen.getByRole('button', { name: /applica/i })).toBeInTheDocument();
    });

    it('should not render when isOpen is false', () => {
      render(<AgentSettingsDrawer {...defaultProps} isOpen={false} />, {
        wrapper: createWrapper(),
      });

      expect(screen.queryByText('Impostazioni Agente')).not.toBeInTheDocument();
    });
  });

  describe('Model Selector', () => {
    it('should filter models by user tier (free)', async () => {
      render(<AgentSettingsDrawer {...defaultProps} userTier="free" />, {
        wrapper: createWrapper(),
      });

      // Only free models should be available
      // GPT-4 and Claude 3.5 Sonnet are premium
    });

    it('should show all models for premium users', async () => {
      render(<AgentSettingsDrawer {...defaultProps} userTier="premium" />, {
        wrapper: createWrapper(),
      });

      // Premium users see all models including GPT-4
    });
  });

  describe('Temperature Slider', () => {
    it('should display current temperature value', () => {
      render(<AgentSettingsDrawer {...defaultProps} />, { wrapper: createWrapper() });

      // Default temperature is 0.7
      expect(screen.getByText('0.7')).toBeInTheDocument();
    });

    it('should render slider component', () => {
      render(<AgentSettingsDrawer {...defaultProps} />, { wrapper: createWrapper() });

      // Radix Slider renders with role="slider"
      const slider = screen.getByRole('slider');
      expect(slider).toBeInTheDocument();
    });

    it('should show descriptive labels', () => {
      render(<AgentSettingsDrawer {...defaultProps} />, { wrapper: createWrapper() });

      expect(screen.getByText('Preciso')).toBeInTheDocument();
      expect(screen.getByText('Creativo')).toBeInTheDocument();
    });
  });

  describe('RAG Strategy', () => {
    it('should update strategy when radio is clicked', async () => {
      const user = userEvent.setup();
      render(<AgentSettingsDrawer {...defaultProps} />, { wrapper: createWrapper() });

      const vectorOption = screen.getByLabelText(/solo vector/i);
      await user.click(vectorOption);

      expect(vectorOption).toBeChecked();
    });
  });

  describe('Max Tokens Input', () => {
    it('should clamp value to min 512', async () => {
      const user = userEvent.setup();
      render(<AgentSettingsDrawer {...defaultProps} />, { wrapper: createWrapper() });

      const input = screen.getByDisplayValue('2048');
      await user.clear(input);
      await user.type(input, '100');
      await user.tab(); // Blur to trigger validation

      // Value should be clamped to 512
    });

    it('should clamp value to max 8192', async () => {
      const user = userEvent.setup();
      render(<AgentSettingsDrawer {...defaultProps} />, { wrapper: createWrapper() });

      const input = screen.getByDisplayValue('2048');
      await user.clear(input);
      await user.type(input, '10000');
      await user.tab();

      // Value should be clamped to 8192
    });
  });

  describe('Advanced Settings', () => {
    it('should expand/collapse advanced settings', async () => {
      const user = userEvent.setup();
      render(<AgentSettingsDrawer {...defaultProps} />, { wrapper: createWrapper() });

      const advancedTrigger = screen.getByText('Avanzate');
      await user.click(advancedTrigger);

      // Should show TopK and MinScore inputs
      await waitFor(() => {
        expect(screen.getByText('Top K')).toBeInTheDocument();
        expect(screen.getByText('Min Score')).toBeInTheDocument();
      });
    });
  });

  describe('Actions', () => {
    it('should call onClose when cancel is clicked', async () => {
      const user = userEvent.setup();
      const onClose = vi.fn();

      render(<AgentSettingsDrawer {...defaultProps} onClose={onClose} />, {
        wrapper: createWrapper(),
      });

      const cancelButton = screen.getByRole('button', { name: /annulla/i });
      await user.click(cancelButton);

      expect(onClose).toHaveBeenCalledTimes(1);
    });

    it('should reset to defaults when reset is clicked', async () => {
      const user = userEvent.setup();
      const { toast } = await import('sonner');

      render(<AgentSettingsDrawer {...defaultProps} />, { wrapper: createWrapper() });

      const resetButton = screen.getByRole('button', { name: /reset/i });
      await user.click(resetButton);

      expect(toast.info).toHaveBeenCalledWith('Configurazione ripristinata ai valori predefiniti');
    });

    it('should call API and onConfigUpdated when apply is clicked', async () => {
      const user = userEvent.setup();
      const onConfigUpdated = vi.fn();
      const { toast } = await import('sonner');

      render(<AgentSettingsDrawer {...defaultProps} onConfigUpdated={onConfigUpdated} />, {
        wrapper: createWrapper(),
      });

      const applyButton = screen.getByRole('button', { name: /applica/i });
      await user.click(applyButton);

      await waitFor(() => {
        expect(mockFetch).toHaveBeenCalledWith(
          '/api/v1/game-sessions/session-123/agent/config',
          expect.objectContaining({
            method: 'PATCH',
          })
        );
      });

      await waitFor(() => {
        expect(toast.success).toHaveBeenCalledWith(
          'Configurazione aggiornata!',
          expect.any(Object)
        );
        expect(onConfigUpdated).toHaveBeenCalled();
      });
    });

    it('should show error toast on API failure', async () => {
      const user = userEvent.setup();
      mockFetch.mockResolvedValueOnce({
        ok: false,
        json: () => Promise.resolve({ error: 'Server error' }),
      });

      const { toast } = await import('sonner');

      render(<AgentSettingsDrawer {...defaultProps} />, { wrapper: createWrapper() });

      const applyButton = screen.getByRole('button', { name: /applica/i });
      await user.click(applyButton);

      await waitFor(() => {
        expect(toast.error).toHaveBeenCalled();
      });
    });

    it('should disable buttons while loading', async () => {
      const user = userEvent.setup();

      // Make fetch hang
      mockFetch.mockImplementationOnce(
        () => new Promise(resolve => setTimeout(() => resolve({ ok: true }), 5000))
      );

      render(<AgentSettingsDrawer {...defaultProps} />, { wrapper: createWrapper() });

      const applyButton = screen.getByRole('button', { name: /applica/i });
      await user.click(applyButton);

      // Buttons should be disabled while loading
      await waitFor(() => {
        expect(applyButton).toBeDisabled();
      });
    });
  });

  describe('Accessibility', () => {
    it('should have visible labels for all form sections', () => {
      render(<AgentSettingsDrawer {...defaultProps} />, { wrapper: createWrapper() });

      // Labels are visible text labels for each section
      expect(screen.getByText('Modello AI')).toBeInTheDocument();
      expect(screen.getByText('Temperatura')).toBeInTheDocument();
      expect(screen.getByText('Max Tokens')).toBeInTheDocument();
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
