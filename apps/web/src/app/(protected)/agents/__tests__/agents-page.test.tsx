/**
 * Agents Page Tests
 * Issue #4778: /agents page entry point for agent creation
 *
 * Tests the agents list page with creation button and slot indicator.
 */

import { render, screen, fireEvent, waitFor } from '@testing-library/react';
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
    agents: {
      getAll: vi.fn(),
      getSlots: vi.fn(),
      createWithSetup: vi.fn(),
    },
    library: {
      getLibrary: vi.fn(),
    },
    admin: {
      getAiModels: vi.fn(),
    },
    pdf: {
      uploadPdf: vi.fn(),
    },
  },
}));

// Mock hooks
vi.mock('@/hooks/queries/useAgents', () => ({
  useAgents: vi.fn(),
}));

vi.mock('@/hooks/queries/useAgentSlots', () => ({
  useAgentSlots: vi.fn(),
  useHasAvailableSlots: () => true,
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
      { name: 'Balanced', displayName: 'Balanced', complexity: 1, description: 'Balanced', useCase: 'General', estimatedTokens: 1000, requiresAdmin: false },
    ],
    isLoading: false,
    error: null,
  }),
}));

import AgentsPage from '../page';
import { useAgents } from '@/hooks/queries/useAgents';
import { useAgentSlots } from '@/hooks/queries/useAgentSlots';

const mockAgents = [
  { id: 'agent-1', name: 'RulesMaster', type: 'Tutor', strategyName: 'Balanced', invocationCount: 42 },
  { id: 'agent-2', name: 'StrategyBot', type: 'Arbitro', strategyName: 'Fast', invocationCount: 10 },
];

const mockSlotsData = {
  total: 3,
  used: 1,
  available: 2,
  slots: [
    { slotIndex: 0, agentId: 'agent-1', agentName: 'RulesMaster', gameId: 'g-1', status: 'active' as const },
    { slotIndex: 1, agentId: null, agentName: null, gameId: null, status: 'available' as const },
    { slotIndex: 2, agentId: null, agentName: null, gameId: null, status: 'available' as const },
  ],
};

describe('AgentsPage', () => {
  let queryClient: QueryClient;

  beforeEach(() => {
    queryClient = createTestQueryClient();
    vi.clearAllMocks();

    (useAgents as Mock).mockReturnValue({
      data: mockAgents,
      isLoading: false,
    });

    (useAgentSlots as Mock).mockReturnValue({
      data: mockSlotsData,
      isLoading: false,
    });
  });

  const renderPage = () =>
    render(
      <QueryClientProvider client={queryClient}>
        <AgentsPage />
      </QueryClientProvider>
    );

  describe('Layout & Header', () => {
    it('renders page title', () => {
      renderPage();
      expect(screen.getByText('AI Agents')).toBeInTheDocument();
    });

    it('renders "Crea Agente" button', () => {
      renderPage();
      expect(screen.getByText('Crea Agente')).toBeInTheDocument();
    });

    it('renders slot indicator bar', () => {
      renderPage();
      expect(screen.getByText('1 / 3 slot usati')).toBeInTheDocument();
    });

    it('renders search input', () => {
      renderPage();
      expect(screen.getByLabelText('Search agents')).toBeInTheDocument();
    });

    it('renders agent count', () => {
      renderPage();
      expect(screen.getByText('2 agents found')).toBeInTheDocument();
    });
  });

  describe('Agent Grid', () => {
    it('renders agent cards', () => {
      renderPage();
      expect(screen.getByText('RulesMaster')).toBeInTheDocument();
      expect(screen.getByText('StrategyBot')).toBeInTheDocument();
    });

    it('shows agent type as subtitle', () => {
      renderPage();
      expect(screen.getByText('Tutor agent')).toBeInTheDocument();
      expect(screen.getByText('Arbitro agent')).toBeInTheDocument();
    });
  });

  describe('Creation Sheet', () => {
    it('opens AgentCreationSheet when "Crea Agente" clicked', async () => {
      renderPage();
      fireEvent.click(screen.getByText('Crea Agente'));

      await waitFor(() => {
        expect(screen.getByText('Crea Agente', { selector: '[class*="SheetTitle"], h2' })).toBeInTheDocument();
      });
    });

    it('disables "Crea Agente" when no slots available', () => {
      (useAgentSlots as Mock).mockReturnValue({
        data: { total: 1, used: 1, available: 0, slots: [] },
        isLoading: false,
      });

      renderPage();
      const button = screen.getAllByText('Crea Agente')[0].closest('button');
      expect(button).toBeDisabled();
    });
  });

  describe('Empty State', () => {
    it('shows empty state when no agents', () => {
      (useAgents as Mock).mockReturnValue({
        data: [],
        isLoading: false,
      });

      renderPage();
      expect(screen.getByText('No agents yet. Create your first AI agent!')).toBeInTheDocument();
    });

    it('shows "Crea il tuo primo agente" button in empty state', () => {
      (useAgents as Mock).mockReturnValue({
        data: [],
        isLoading: false,
      });

      renderPage();
      expect(screen.getByText('Crea il tuo primo agente')).toBeInTheDocument();
    });

    it('shows search-specific empty state when filtering', () => {
      (useAgents as Mock).mockReturnValue({
        data: [],
        isLoading: false,
      });

      renderPage();
      const searchInput = screen.getByLabelText('Search agents');
      fireEvent.change(searchInput, { target: { value: 'nonexistent' } });

      expect(screen.getByText('No agents found')).toBeInTheDocument();
      expect(screen.getByText('Clear search')).toBeInTheDocument();
    });
  });

  describe('Search', () => {
    it('filters agents by name', () => {
      renderPage();
      const searchInput = screen.getByLabelText('Search agents');
      fireEvent.change(searchInput, { target: { value: 'Rules' } });

      expect(screen.getByText('RulesMaster')).toBeInTheDocument();
      expect(screen.queryByText('StrategyBot')).not.toBeInTheDocument();
      expect(screen.getByText('1 agent found')).toBeInTheDocument();
    });
  });
});
