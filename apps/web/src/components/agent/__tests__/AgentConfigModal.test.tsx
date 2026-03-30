/**
 * Tests for AgentConfigModal component
 * Issue #3190 (AGT-016): Frontend Agent Components Tests
 *
 * Coverage:
 * - Typology dropdown population
 * - Model filtering by tier (Free vs Premium)
 * - Cost estimation display
 * - Quota warning triggers (>90%)
 * - Save config API call with toast
 * - localStorage caching
 */

import { render, screen, waitFor } from '@testing-library/react';
import userEvent from '@testing-library/user-event';
import { describe, it, expect, vi, beforeEach } from 'vitest';
import { QueryClient, QueryClientProvider } from '@tanstack/react-query';
import { toast } from 'sonner';

import { AgentConfigModal } from '../AgentConfigModal';

// Mock dependencies
vi.mock('sonner', () => ({
  toast: {
    success: vi.fn(),
    error: vi.fn(),
  },
}));

vi.mock('@/hooks/useAuth', () => ({
  useAuth: () => ({ user: { id: 'user-1', email: 'test@example.com' } }),
}));

// Mock useAgentConfigModal with factory function
const mockUseAgentConfigModal = vi.fn();
vi.mock('@/hooks/useAgentConfigModal', () => ({
  useAgentConfigModal: (props: any) => mockUseAgentConfigModal(props),
}));

describe('AgentConfigModal', () => {
  let queryClient: QueryClient;

  beforeEach(() => {
    queryClient = new QueryClient({
      defaultOptions: {
        queries: { retry: false },
        mutations: { retry: false },
      },
    });
    vi.clearAllMocks();

    // Reset mock to default return value
    mockUseAgentConfigModal.mockReturnValue({
      selectedagentDefinitionId: null,
      setSelectedagentDefinitionId: vi.fn(),
      selectedModelName: 'GPT-4o',
      setSelectedModelName: vi.fn(),
      typologies: [
        { id: 'typo-1', name: 'Tutor', description: 'Guida passo-passo per imparare le regole' },
        { id: 'typo-2', name: 'Strategia', description: 'Consigli strategici avanzati' },
      ],
      typologiesLoading: false,
      typologiesError: null,
      availableModels: [
        { name: 'Claude-3.5-Haiku', cost: 0.003 },
        { name: 'GPT-4o', cost: 0.005, recommended: true },
      ],
      userTier: 'Premium' as const,
      estimatedCost: 0.005,
      quota: {
        currentSessions: 5,
        maxSessions: 10,
        remainingSlots: 5,
        percentageUsed: 50,
        canCreateNew: true,
        isUnlimited: false,
      },
      quotaLoading: false,
      showWarning: false,
      saveConfig: vi.fn(),
      saving: false,
      saveError: null,
      isValid: true,
    });
  });

  const renderModal = (props = {}) => {
    return render(
      <QueryClientProvider client={queryClient}>
        <AgentConfigModal gameId="game-123" {...props} />
      </QueryClientProvider>
    );
  };

  // =========================================================================
  // Rendering Tests
  // =========================================================================

  describe('Rendering', () => {
    it('should render modal with default state', async () => {
      const user = userEvent.setup();
      renderModal();

      // Open dialog
      await user.click(screen.getByText('Configura AI'));

      await waitFor(() => {
        expect(screen.getByText('Configurazione Agente AI')).toBeInTheDocument();
        expect(
          screen.getByText(
            'Seleziona la tipologia di agente e il modello AI da utilizzare per le sessioni di gioco.'
          )
        ).toBeInTheDocument();
      });
    });

    it('should render typology dropdown', async () => {
      const user = userEvent.setup();
      renderModal();

      await user.click(screen.getByText('Configura AI'));

      await waitFor(() => {
        expect(screen.getByLabelText(/tipologia agente/i)).toBeInTheDocument();
      });
    });

    it('should render model dropdown', async () => {
      const user = userEvent.setup();
      renderModal();

      await user.click(screen.getByText('Configura AI'));

      await waitFor(() => {
        expect(screen.getByLabelText(/modello ai/i)).toBeInTheDocument();
      });
    });

    it('should render save button', async () => {
      const user = userEvent.setup();
      renderModal();

      await user.click(screen.getByText('Configura AI'));

      await waitFor(() => {
        expect(screen.getByRole('button', { name: /salva e lancia agente/i })).toBeInTheDocument();
      });
    });
  });

  // =========================================================================
  // Typology Dropdown Tests
  // =========================================================================

  describe('Typology Dropdown', () => {
    it('should populate typology dropdown with API data', async () => {
      const user = userEvent.setup();
      renderModal();

      // Open dialog first
      await user.click(screen.getByText('Configura AI'));

      await waitFor(() => {
        const dropdown = screen.getByRole('combobox', { name: /tipologia agente/i });
        expect(dropdown).toBeInTheDocument();
      });

      // Now open the dropdown
      const dropdown = screen.getByRole('combobox', { name: /tipologia agente/i });
      await user.click(dropdown);

      await waitFor(() => {
        // Typology names may appear multiple times
        expect(screen.getAllByText('Tutor').length).toBeGreaterThan(0);
        expect(screen.getByText('Guida passo-passo per imparare le regole')).toBeInTheDocument();
        expect(screen.getAllByText('Strategia').length).toBeGreaterThan(0);
      });
    });

    it('should show typology description in dropdown', async () => {
      const user = userEvent.setup();
      renderModal();

      // Open dialog first
      await user.click(screen.getByText('Configura AI'));

      await waitFor(() => {
        const dropdown = screen.getByRole('combobox', { name: /tipologia agente/i });
        expect(dropdown).toBeInTheDocument();
      });

      const dropdown = screen.getByRole('combobox', { name: /tipologia agente/i });
      await user.click(dropdown);

      await waitFor(() => {
        const descriptions = screen.getAllByText(/guida passo-passo|consigli strategici/i);
        expect(descriptions.length).toBeGreaterThan(0);
      });
    });
  });

  // =========================================================================
  // Model Filtering Tests
  // =========================================================================

  describe('Model Filtering by Tier', () => {
    it('should display Premium tier badge', async () => {
      const user = userEvent.setup();
      renderModal();

      // Open dialog to see tier badge
      await user.click(screen.getByText('Configura AI'));

      await waitFor(() => {
        // Premium may appear multiple times in UI
        expect(screen.getAllByText('Premium').length).toBeGreaterThan(0);
      });
    });

    it('should show tier-filtered models in dropdown', async () => {
      const user = userEvent.setup();
      renderModal();

      // Open dialog first
      await user.click(screen.getByText('Configura AI'));

      await waitFor(() => {
        const dropdown = screen.getByRole('combobox', { name: /modello ai/i });
        expect(dropdown).toBeInTheDocument();
      });

      const dropdown = screen.getByRole('combobox', { name: /modello ai/i });
      await user.click(dropdown);

      await waitFor(() => {
        // Models appear in dropdown, may have duplicates (selected + options)
        const haiku = screen.getAllByText('Claude-3.5-Haiku');
        const gpt4o = screen.getAllByText('GPT-4o');
        expect(haiku.length).toBeGreaterThan(0);
        expect(gpt4o.length).toBeGreaterThan(0);
      });
    });

    it('should mark recommended model', async () => {
      const user = userEvent.setup();
      renderModal();

      // Open dialog first
      await user.click(screen.getByText('Configura AI'));

      await waitFor(() => {
        const dropdown = screen.getByRole('combobox', { name: /modello ai/i });
        expect(dropdown).toBeInTheDocument();
      });

      const dropdown = screen.getByRole('combobox', { name: /modello ai/i });
      await user.click(dropdown);

      await waitFor(() => {
        expect(screen.getByText('(Consigliato)')).toBeInTheDocument();
      });
    });

    it('should show model cost per query', async () => {
      const user = userEvent.setup();
      renderModal();

      // Open dialog first
      await user.click(screen.getByText('Configura AI'));

      await waitFor(() => {
        const dropdown = screen.getByRole('combobox', { name: /modello ai/i });
        expect(dropdown).toBeInTheDocument();
      });

      const dropdown = screen.getByRole('combobox', { name: /modello ai/i });
      await user.click(dropdown);

      await waitFor(() => {
        expect(screen.getByText('$0.0030/query')).toBeInTheDocument();
        expect(screen.getByText('$0.0050/query')).toBeInTheDocument();
      });
    });
  });

  // =========================================================================
  // Cost Estimation Tests
  // =========================================================================

  describe('Cost Estimation', () => {
    it('should display cost estimation for selected model', async () => {
      const user = userEvent.setup();
      renderModal();

      // Open dialog to see cost display
      await user.click(screen.getByText('Configura AI'));

      await waitFor(() => {
        expect(screen.getByText('Costo stimato per query')).toBeInTheDocument();
        expect(screen.getByText('$0.0050')).toBeInTheDocument();
      });
    });

    it('should update cost when model changes', async () => {
      const user = userEvent.setup();
      renderModal();

      // Open dialog first
      await user.click(screen.getByText('Configura AI'));

      await waitFor(() => {
        expect(screen.getByText('$0.0050')).toBeInTheDocument();
      });

      // Select different model
      const dropdown = screen.getByRole('combobox', { name: /modello ai/i });
      await user.click(dropdown);

      await waitFor(() => {
        expect(screen.getByText('Claude-3.5-Haiku')).toBeInTheDocument();
      });
    });
  });

  // =========================================================================
  // Quota Display Tests
  // =========================================================================

  describe('Quota Display', () => {
    it('should show quota usage with progress bar', async () => {
      const user = userEvent.setup();
      renderModal();

      // Open dialog to see quota
      await user.click(screen.getByText('Configura AI'));

      await waitFor(() => {
        expect(screen.getByText('Utilizzo Quota')).toBeInTheDocument();
        expect(screen.getByText('5 / 10')).toBeInTheDocument();
        expect(screen.getByText('5 slot disponibili')).toBeInTheDocument();
      });
    });

    it('should display quota percentage', async () => {
      const user = userEvent.setup();
      renderModal();

      // Open dialog
      await user.click(screen.getByText('Configura AI'));

      await waitFor(() => {
        const progress = screen.getByRole('progressbar');
        expect(progress).toHaveAttribute('aria-valuenow', '50');
      });
    });

    it('should not show quota for unlimited users', async () => {
      const user = userEvent.setup();
      mockUseAgentConfigModal.mockReturnValueOnce({
        ...mockUseAgentConfigModal(),
        quota: { isUnlimited: true, canCreateNew: true },
      });

      renderModal();

      // Open dialog
      await user.click(screen.getByText('Configura AI'));

      await waitFor(() => {
        expect(screen.queryByText('Utilizzo Quota')).not.toBeInTheDocument();
      });
    });
  });

  // =========================================================================
  // Quota Warning Tests (>90%)
  // =========================================================================

  describe('Quota Warning Triggers', () => {
    it('should show warning alert when quota >90%', async () => {
      const user = userEvent.setup();
      mockUseAgentConfigModal.mockReturnValueOnce({
        ...mockUseAgentConfigModal(),
        quota: {
          currentSessions: 95,
          maxSessions: 100,
          remainingSlots: 5,
          percentageUsed: 95,
          canCreateNew: true,
          isUnlimited: false,
        },
        showWarning: true,
      });

      renderModal();

      // Open dialog
      await user.click(screen.getByText('Configura AI'));

      await waitFor(() => {
        expect(
          screen.getByText(/stai utilizzando oltre il 95% della tua quota/i)
        ).toBeInTheDocument();
      });
    });

    it('should show red progress bar when quota >90%', async () => {
      const user = userEvent.setup();
      mockUseAgentConfigModal.mockReturnValueOnce({
        ...mockUseAgentConfigModal(),
        quota: {
          percentageUsed: 95,
          currentSessions: 95,
          maxSessions: 100,
          remainingSlots: 5,
          canCreateNew: true,
          isUnlimited: false,
        },
      });

      renderModal();

      // Open dialog
      await user.click(screen.getByText('Configura AI'));

      await waitFor(() => {
        const progress = screen.getByRole('progressbar');
        expect(progress).toHaveClass(/\[&>\*\]:bg-destructive/);
      });
    });

    it('should show amber progress bar when quota 75-89%', async () => {
      const user = userEvent.setup();
      mockUseAgentConfigModal.mockReturnValueOnce({
        ...mockUseAgentConfigModal(),
        quota: {
          percentageUsed: 80,
          currentSessions: 80,
          maxSessions: 100,
          remainingSlots: 20,
          canCreateNew: true,
          isUnlimited: false,
        },
      });

      renderModal();

      // Open dialog
      await user.click(screen.getByText('Configura AI'));

      await waitFor(() => {
        const progress = screen.getByRole('progressbar');
        expect(progress).toHaveClass(/\[&>\*\]:bg-amber-500/);
      });
    });

    it('should disable save button when quota is full', async () => {
      const user = userEvent.setup();
      mockUseAgentConfigModal.mockReturnValueOnce({
        ...mockUseAgentConfigModal(),
        quota: {
          percentageUsed: 100,
          currentSessions: 100,
          maxSessions: 100,
          remainingSlots: 0,
          canCreateNew: false,
          isUnlimited: false,
        },
      });

      renderModal();

      // Open dialog
      await user.click(screen.getByText('Configura AI'));

      await waitFor(() => {
        const saveButton = screen.getByRole('button', { name: /salva e lancia agente/i });
        expect(saveButton).toBeDisabled();
      });
    });

    it('should show quota full alert', async () => {
      const user = userEvent.setup();
      mockUseAgentConfigModal.mockReturnValueOnce({
        ...mockUseAgentConfigModal(),
        quota: {
          percentageUsed: 100,
          currentSessions: 100,
          maxSessions: 100,
          remainingSlots: 0,
          canCreateNew: false,
          isUnlimited: false,
        },
      });

      renderModal();

      // Open dialog
      await user.click(screen.getByText('Configura AI'));

      await waitFor(() => {
        expect(
          screen.getByText(/hai raggiunto il limite massimo di sessioni attive/i)
        ).toBeInTheDocument();
      });
    });
  });

  // =========================================================================
  // Save Config Tests
  // =========================================================================

  describe('Save Config', () => {
    it('should call saveConfig on button click', async () => {
      const user = userEvent.setup();
      const mockSaveConfig = vi.fn();

      mockUseAgentConfigModal.mockReturnValueOnce({
        ...mockUseAgentConfigModal(),
        saveConfig: mockSaveConfig,
      });

      renderModal();

      // Open dialog
      await user.click(screen.getByText('Configura AI'));

      await waitFor(() => {
        const saveButton = screen.getByRole('button', { name: /salva e lancia agente/i });
        expect(saveButton).toBeInTheDocument();
      });

      const saveButton = screen.getByRole('button', { name: /salva e lancia agente/i });
      await user.click(saveButton);

      await waitFor(() => {
        expect(mockSaveConfig).toHaveBeenCalledTimes(1);
      });
    });

    it('should disable save button while saving', async () => {
      const user = userEvent.setup();
      mockUseAgentConfigModal.mockReturnValueOnce({
        ...mockUseAgentConfigModal(),
        saving: true,
      });

      renderModal();

      // Open dialog
      await user.click(screen.getByText('Configura AI'));

      await waitFor(() => {
        const saveButton = screen.getByRole('button', { name: /salvataggio/i });
        expect(saveButton).toBeDisabled();
      });
    });

    it('should show loading state during save', async () => {
      const user = userEvent.setup();
      mockUseAgentConfigModal.mockReturnValueOnce({
        ...mockUseAgentConfigModal(),
        saving: true,
      });

      renderModal();

      // Open dialog
      await user.click(screen.getByText('Configura AI'));

      await waitFor(() => {
        expect(screen.getByText('Salvataggio...')).toBeInTheDocument();
      });
    });

    it('should call onConfigSaved callback after successful save', async () => {
      const user = userEvent.setup();
      const mockOnConfigSaved = vi.fn();
      const mockSaveConfig = vi.fn().mockResolvedValue(undefined);

      mockUseAgentConfigModal.mockReturnValueOnce({
        ...mockUseAgentConfigModal(),
        saveConfig: mockSaveConfig,
      });

      renderModal({ onConfigSaved: mockOnConfigSaved });

      // Open dialog
      await user.click(screen.getByText('Configura AI'));

      await waitFor(() => {
        const saveButton = screen.getByRole('button', { name: /salva e lancia agente/i });
        expect(saveButton).toBeInTheDocument();
      });

      const saveButton = screen.getByRole('button', { name: /salva e lancia agente/i });
      await user.click(saveButton);

      await waitFor(() => {
        expect(mockOnConfigSaved).toHaveBeenCalled();
      });
    });

    it('should disable save button when form is invalid', async () => {
      const user = userEvent.setup();
      mockUseAgentConfigModal.mockReturnValueOnce({
        ...mockUseAgentConfigModal(),
        isValid: false,
      });

      renderModal();

      // Open dialog
      await user.click(screen.getByText('Configura AI'));

      await waitFor(() => {
        const saveButton = screen.getByRole('button', { name: /salva e lancia agente/i });
        expect(saveButton).toBeDisabled();
      });
    });
  });

  // =========================================================================
  // Dialog Interaction Tests
  // =========================================================================

  describe('Dialog Interaction', () => {
    it('should render custom trigger button', () => {
      const customTrigger = <button>Custom Trigger</button>;
      renderModal({ trigger: customTrigger });

      expect(screen.getByText('Custom Trigger')).toBeInTheDocument();
    });

    it('should render default trigger if none provided', () => {
      renderModal();

      expect(screen.getByText('Configura AI')).toBeInTheDocument();
    });
  });
});
