/**
 * AgentCreationSheet Component Tests
 * Issue #4776: AgentCreationSheet wizard
 *
 * Tests the single-page wizard for creating an AI agent.
 */

import { render, screen, fireEvent, waitFor, within } from '@testing-library/react';
import { describe, it, expect, vi, beforeEach } from 'vitest';
import React from 'react';
import { QueryClient, QueryClientProvider } from '@tanstack/react-query';
import type { Mock } from 'vitest';

import { createTestQueryClient } from '@/__tests__/utils/query-test-utils';

// Mock next/navigation
vi.mock('next/navigation', () => ({
  useRouter: () => ({
    push: vi.fn(),
    replace: vi.fn(),
    prefetch: vi.fn(),
  }),
}));

// Mock sonner
vi.mock('sonner', () => ({
  toast: {
    success: vi.fn(),
    error: vi.fn(),
  },
}));

// Mock API module
vi.mock('@/lib/api', () => ({
  api: {
    library: {
      getLibrary: vi.fn(),
    },
    admin: {
      getAiModels: vi.fn(),
    },
    agents: {
      getSlots: vi.fn(),
      createWithSetup: vi.fn(),
    },
    pdf: {
      uploadPdf: vi.fn(),
    },
  },
}));

// Mock sub-components that have their own API calls
vi.mock('@/hooks/queries/useAgentTypologies', () => ({
  useApprovedTypologies: () => ({
    data: [
      { id: 'typ-1', name: 'Rules Helper', description: 'Helps with rules', defaultStrategyName: 'Balanced', basePrompt: '', status: 'Approved', createdBy: '', isDeleted: false, createdAt: '' },
    ],
    isLoading: false,
    error: null,
  }),
}));

vi.mock('@/hooks/queries/useRagStrategies', () => ({
  useRagStrategies: () => ({
    data: [
      { name: 'Fast', displayName: 'Fast', complexity: 0, description: 'Quick', useCase: 'Simple', estimatedTokens: 500, requiresAdmin: false },
      { name: 'Balanced', displayName: 'Balanced', complexity: 1, description: 'Balanced', useCase: 'General', estimatedTokens: 1000, requiresAdmin: false },
    ],
    isLoading: false,
    error: null,
  }),
}));

import { AgentCreationSheet } from '../AgentCreationSheet';
import { api } from '@/lib/api';

const mockSlotsResponse = {
  total: 3,
  used: 1,
  available: 2,
  slots: [
    { slotIndex: 0, agentId: 'agent-1', agentName: 'Existing Agent', gameId: 'g-1', status: 'active' as const },
    { slotIndex: 1, agentId: null, agentName: null, gameId: null, status: 'available' as const },
    { slotIndex: 2, agentId: null, agentName: null, gameId: null, status: 'available' as const },
  ],
};

const mockNoSlotsResponse = {
  total: 1,
  used: 1,
  available: 0,
  slots: [
    { slotIndex: 0, agentId: 'agent-1', agentName: 'Existing Agent', gameId: 'g-1', status: 'active' as const },
  ],
};

const mockLibraryResponse = {
  items: [
    {
      id: 'entry-1', userId: 'u-1', gameId: 'game-1', gameTitle: 'Catan',
      gamePublisher: 'Kosmos', gameYearPublished: 1995, gameIconUrl: null, gameImageUrl: null,
      addedAt: '2024-01-01T00:00:00Z', notes: null, isFavorite: true, currentState: 'Owned',
      stateChangedAt: null, stateNotes: null, hasKb: true, kbCardCount: 1, kbIndexedCount: 1, kbProcessingCount: 0, agentIsOwned: true,
    },
  ],
  page: 1, pageSize: 100, totalCount: 1, totalPages: 1,
  hasNextPage: false, hasPreviousPage: false,
};

const mockModelsResponse = {
  items: [
    {
      id: 'model-1', name: 'gpt-4o-mini', displayName: 'GPT-4o Mini', provider: 'openai',
      modelIdentifier: 'openai/gpt-4o-mini', isPrimary: true, status: 'active',
      cost: { inputCostPer1kTokens: 0.00015, outputCostPer1kTokens: 0.0006, currency: 'USD' },
      temperature: 0.7, maxTokens: 4096, createdAt: '2024-01-01T00:00:00Z', updatedAt: null,
    },
  ],
  total: 1, page: 1, pageSize: 50,
};

describe('AgentCreationSheet', () => {
  let queryClient: QueryClient;
  const mockOnClose = vi.fn();

  beforeEach(() => {
    queryClient = createTestQueryClient();
    vi.clearAllMocks();

    // Default mock responses
    (api.agents.getSlots as Mock).mockResolvedValue(mockSlotsResponse);
    (api.library.getLibrary as Mock).mockResolvedValue(mockLibraryResponse);
    (api.admin.getAiModels as Mock).mockResolvedValue(mockModelsResponse);
  });

  const wrapper = ({ children }: { children: React.ReactNode }) => (
    <QueryClientProvider client={queryClient}>{children}</QueryClientProvider>
  );

  const renderSheet = (props?: Partial<React.ComponentProps<typeof AgentCreationSheet>>) =>
    render(
      <AgentCreationSheet
        isOpen={true}
        onClose={mockOnClose}
        {...props}
      />,
      { wrapper }
    );

  describe('Layout & Structure', () => {
    it('renders sheet with title "Crea Agente"', async () => {
      renderSheet();
      expect(screen.getByText('Crea Agente')).toBeInTheDocument();
    });

    it('renders game title in header when provided', () => {
      renderSheet({ initialGameTitle: 'Catan' });
      expect(screen.getByText('Catan')).toBeInTheDocument();
    });

    it('renders all 4 section headers', () => {
      renderSheet();
      expect(screen.getByText('Gioco')).toBeInTheDocument();
      expect(screen.getByText('Knowledge Base')).toBeInTheDocument();
      expect(screen.getByText('Configura Agente')).toBeInTheDocument();
      expect(screen.getByText('Costi & Slot')).toBeInTheDocument();
    });

    it('renders footer with Annulla and Crea buttons', () => {
      renderSheet();
      expect(screen.getByText('Annulla')).toBeInTheDocument();
      expect(screen.getByText('Crea e Inizia Chat')).toBeInTheDocument();
    });

    it('renders close button', () => {
      renderSheet();
      expect(screen.getByLabelText('Close')).toBeInTheDocument();
    });
  });

  describe('Section Collapse', () => {
    it('toggles section when header clicked', async () => {
      renderSheet();

      // All sections start expanded - find game section content (GameSelector label)
      await waitFor(() => {
        expect(screen.getByText('Select Game')).toBeInTheDocument();
      });

      // Click "Gioco" section header to collapse
      fireEvent.click(screen.getByText('Gioco'));

      // Game selector label should be hidden now
      expect(screen.queryByText('Select Game')).not.toBeInTheDocument();

      // Click again to expand
      fireEvent.click(screen.getByText('Gioco'));
      expect(screen.getByText('Select Game')).toBeInTheDocument();
    });
  });

  describe('Game Section', () => {
    it('renders GameSelector', async () => {
      renderSheet();
      await waitFor(() => {
        expect(screen.getByText('Select Game')).toBeInTheDocument();
      });
    });

    it('shows "In collezione" badge when game is in library', async () => {
      renderSheet();

      await waitFor(() => {
        expect(screen.queryByText('Loading games...')).not.toBeInTheDocument();
      });

      // Select Catan from dropdown
      fireEvent.click(screen.getByLabelText('Select game'));
      fireEvent.click(screen.getByText('Catan'));

      await waitFor(() => {
        expect(screen.getByText('In collezione')).toBeInTheDocument();
      });
    });
  });

  describe('Knowledge Base Section', () => {
    it('renders PDF upload drop zone', () => {
      renderSheet();
      expect(screen.getByText(/Trascina PDF qui/)).toBeInTheDocument();
    });

    it('shows help text when no game selected', () => {
      renderSheet();
      expect(screen.getByText(/Seleziona prima un gioco/)).toBeInTheDocument();
    });

    it('disables drop zone when no game selected', () => {
      renderSheet();
      const dropZone = screen.getByLabelText('Upload PDF files');
      expect(dropZone).toHaveClass('pointer-events-none');
    });
  });

  describe('Agent Config Section', () => {
    it('renders agent name input', () => {
      renderSheet();
      expect(screen.getByLabelText('Nome Agente')).toBeInTheDocument();
    });

    it('shows placeholder with game name', () => {
      renderSheet({ initialGameTitle: 'Catan' });
      const input = screen.getByLabelText('Nome Agente');
      expect(input).toHaveAttribute('placeholder', 'Esperto di Catan');
    });

    it('renders TypologySelector', async () => {
      renderSheet();
      expect(screen.getByText('Agent Type')).toBeInTheDocument();
    });

    it('renders StrategySelector', () => {
      renderSheet();
      expect(screen.getByText('RAG Strategy')).toBeInTheDocument();
    });

    it('renders ModelSelector', async () => {
      renderSheet();
      await waitFor(() => {
        expect(screen.getByText('AI Model')).toBeInTheDocument();
      });
    });
  });

  describe('Costs & Slots Section', () => {
    it('shows slot usage bar', async () => {
      renderSheet();

      await waitFor(() => {
        expect(screen.getByText(/1 \/ 3 usati/)).toBeInTheDocument();
      });
    });

    it('shows no slots warning when all used', async () => {
      (api.agents.getSlots as Mock).mockResolvedValue(mockNoSlotsResponse);

      renderSheet();

      await waitFor(() => {
        expect(screen.getByText(/Nessuno slot disponibile/)).toBeInTheDocument();
      });
    });
  });

  describe('Create Flow', () => {
    it('disables create button when no game selected', () => {
      renderSheet();
      const createBtn = screen.getByText('Crea e Inizia Chat');
      expect(createBtn.closest('button')).toBeDisabled();
    });

    it('disables create button when no slots available', async () => {
      (api.agents.getSlots as Mock).mockResolvedValue(mockNoSlotsResponse);

      renderSheet({ initialGameId: 'game-1', initialGameTitle: 'Catan' });

      await waitFor(() => {
        expect(screen.getByText(/Nessuno slot disponibile/)).toBeInTheDocument();
      });

      const createBtn = screen.getByText('Crea e Inizia Chat');
      expect(createBtn.closest('button')).toBeDisabled();
    });

    it('calls createWithSetup when create button clicked', async () => {
      (api.agents.createWithSetup as Mock).mockResolvedValue({
        agentId: 'new-agent-1',
        agentName: 'Esperto di Catan',
        threadId: 'thread-123',
        slotUsed: 1,
        gameAddedToCollection: false,
      });

      renderSheet();

      // Wait for data to load
      await waitFor(() => {
        expect(screen.queryByText('Loading games...')).not.toBeInTheDocument();
      });

      // Select game
      fireEvent.click(screen.getByLabelText('Select game'));
      fireEvent.click(screen.getByText('Catan'));

      // Click create
      await waitFor(() => {
        const createBtn = screen.getByText('Crea e Inizia Chat');
        expect(createBtn.closest('button')).not.toBeDisabled();
      });

      fireEvent.click(screen.getByText('Crea e Inizia Chat'));

      await waitFor(() => {
        expect(api.agents.createWithSetup).toHaveBeenCalledWith(
          expect.objectContaining({
            gameId: 'game-1',
          })
        );
      });
    });

    it('shows loading state during creation', async () => {
      // Make createWithSetup hang
      (api.agents.createWithSetup as Mock).mockImplementation(() => new Promise(() => {}));

      renderSheet();

      await waitFor(() => {
        expect(screen.queryByText('Loading games...')).not.toBeInTheDocument();
      });

      // Select game
      fireEvent.click(screen.getByLabelText('Select game'));
      fireEvent.click(screen.getByText('Catan'));

      await waitFor(() => {
        const createBtn = screen.getByText('Crea e Inizia Chat');
        expect(createBtn.closest('button')).not.toBeDisabled();
      });

      fireEvent.click(screen.getByText('Crea e Inizia Chat'));

      await waitFor(() => {
        expect(screen.getByText('Creazione...')).toBeInTheDocument();
      });
    });
  });

  describe('Close Behavior', () => {
    it('calls onClose when Annulla clicked', () => {
      renderSheet();
      fireEvent.click(screen.getByText('Annulla'));
      expect(mockOnClose).toHaveBeenCalled();
    });

    it('calls onClose when close button clicked', () => {
      renderSheet();
      fireEvent.click(screen.getByLabelText('Close'));
      expect(mockOnClose).toHaveBeenCalled();
    });

    it('does not render when isOpen is false', () => {
      renderSheet({ isOpen: false });
      expect(screen.queryByText('Crea Agente')).not.toBeInTheDocument();
    });
  });
});
