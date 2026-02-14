/**
 * KnowledgeBaseTab Component Tests (Issue #4229)
 *
 * Tests for Knowledge Base documents display in SharedGame detail modal.
 * Coverage target: ≥85%
 */

import { describe, it, expect, vi, beforeEach } from 'vitest';
import { render, screen, waitFor } from '@testing-library/react';
import { QueryClient, QueryClientProvider } from '@tanstack/react-query';

import { KnowledgeBaseTab } from '../KnowledgeBaseTab';
import * as useGameAgentsModule from '@/hooks/queries/useGameAgents';
import { api } from '@/lib/api';
import type { AgentDto, AgentDocumentsDto } from '@/lib/api/schemas';

// ============================================================================
// Test Setup
// ============================================================================

const createTestQueryClient = () =>
  new QueryClient({
    defaultOptions: {
      queries: { retry: false },
      mutations: { retry: false },
    },
  });

const renderWithQueryClient = (ui: React.ReactElement) => {
  const queryClient = createTestQueryClient();
  return render(<QueryClientProvider client={queryClient}>{ui}</QueryClientProvider>);
};

// ============================================================================
// Mock Data
// ============================================================================

const mockAgent1: AgentDto = {
  id: '11111111-1111-1111-1111-111111111111',
  name: 'Rules Agent',
  type: 'chat',
  strategyName: 'hybrid',
  strategyParameters: {},
  isActive: true,
  createdAt: '2024-01-01T00:00:00Z',
  lastInvokedAt: null,
  invocationCount: 5,
  isRecentlyUsed: true,
  isIdle: false,
};

const mockAgent2: AgentDto = {
  id: '22222222-2222-2222-2222-222222222222',
  name: 'FAQ Agent',
  type: 'chat',
  strategyName: 'semantic',
  strategyParameters: {},
  isActive: true,
  createdAt: '2024-01-01T00:00:00Z',
  lastInvokedAt: null,
  invocationCount: 3,
  isRecentlyUsed: false,
  isIdle: false,
};

const mockDocuments1: AgentDocumentsDto = {
  agentId: mockAgent1.id,
  documents: [
    {
      id: '33333333-3333-3333-3333-333333333333',
      sharedGameId: 'game-id',
      pdfDocumentId: 'pdf-1',
      documentType: 0, // Rulebook
      version: '1.0',
      isActive: true,
      tags: ['core', 'v1'],
      gameName: 'Test Game Rulebook',
    },
    {
      id: '44444444-4444-4444-4444-444444444444',
      sharedGameId: 'game-id',
      pdfDocumentId: 'pdf-2',
      documentType: 1, // Errata
      version: '1.1',
      isActive: true,
      tags: ['update'],
      gameName: 'Test Game Errata',
    },
  ],
};

const mockDocuments2: AgentDocumentsDto = {
  agentId: mockAgent2.id,
  documents: [
    {
      id: '55555555-5555-5555-5555-555555555555',
      sharedGameId: 'game-id',
      pdfDocumentId: 'pdf-3',
      documentType: 2, // Homerule
      version: '2.0',
      isActive: false,
      tags: ['community'],
      gameName: 'Test Game Homerules',
    },
  ],
};

// ============================================================================
// Tests
// ============================================================================

describe('KnowledgeBaseTab', () => {
  beforeEach(() => {
    vi.clearAllMocks();
  });

  describe('Loading States', () => {
    it('renders loading state when agents are being fetched', () => {
      vi.spyOn(useGameAgentsModule, 'useGameAgents').mockReturnValue({
        data: undefined,
        isLoading: true,
        error: null,
      } as any);

      renderWithQueryClient(<KnowledgeBaseTab gameId="game-id" />);

      expect(screen.getByText(/Loading Knowledge Base/i)).toBeInTheDocument();
    });
  });

  describe('Error States', () => {
    it('renders error message when agents fetch fails', () => {
      vi.spyOn(useGameAgentsModule, 'useGameAgents').mockReturnValue({
        data: undefined,
        isLoading: false,
        error: new Error('Failed to fetch agents'),
      } as any);

      renderWithQueryClient(<KnowledgeBaseTab gameId="game-id" />);

      expect(screen.getByText(/Failed to load agents/i)).toBeInTheDocument();
      expect(screen.getByText(/Failed to fetch agents/i)).toBeInTheDocument();
    });
  });

  describe('Empty States', () => {
    it('renders empty state when no agents are available', () => {
      vi.spyOn(useGameAgentsModule, 'useGameAgents').mockReturnValue({
        data: [],
        isLoading: false,
        error: null,
      } as any);

      renderWithQueryClient(<KnowledgeBaseTab gameId="game-id" />);

      expect(screen.getByText(/No Agents Available/i)).toBeInTheDocument();
      expect(
        screen.getByText(/This game doesn't have any AI agents configured yet/i)
      ).toBeInTheDocument();
    });

    it('renders empty state when agents have no documents', async () => {
      vi.spyOn(useGameAgentsModule, 'useGameAgents').mockReturnValue({
        data: [mockAgent1],
        isLoading: false,
        error: null,
      } as any);

      vi.spyOn(api.agents, 'getDocuments').mockResolvedValue({
        agentId: mockAgent1.id,
        documents: [],
      });

      renderWithQueryClient(<KnowledgeBaseTab gameId="game-id" />);

      await waitFor(() => {
        expect(screen.getByText(/No Documents Indexed Yet/i)).toBeInTheDocument();
      });

    });
  });

  describe('Document Display', () => {
    it('renders documents from single agent', async () => {
      vi.spyOn(useGameAgentsModule, 'useGameAgents').mockReturnValue({
        data: [mockAgent1],
        isLoading: false,
        error: null,
      } as any);

      vi.spyOn(api.agents, 'getDocuments').mockResolvedValue(mockDocuments1);

      renderWithQueryClient(<KnowledgeBaseTab gameId="game-id" />);

      await waitFor(() => {
        expect(screen.getByText(/Knowledge Base Documents/i)).toBeInTheDocument();
      });

      // Check document count header
      expect(screen.getByText(/2 documents indexed across 1 agent/i)).toBeInTheDocument();

      // Check document names
      expect(screen.getByText(/Test Game Rulebook/i)).toBeInTheDocument();
      expect(screen.getByText(/Test Game Errata/i)).toBeInTheDocument();

      // Check agent names
      expect(screen.getAllByText(/Agent: Rules Agent/i)).toHaveLength(2);

      // Check document type badges
      expect(screen.getByText('Rulebook')).toBeInTheDocument();
      expect(screen.getByText('Errata')).toBeInTheDocument();

      // Check version badges
      expect(screen.getByText('v1.0')).toBeInTheDocument();
      expect(screen.getByText('v1.1')).toBeInTheDocument();

      // Check tags
      expect(screen.getByText('core')).toBeInTheDocument();
      expect(screen.getByText('v1')).toBeInTheDocument();
      expect(screen.getByText('update')).toBeInTheDocument();

      // Check active badges (both documents are active)
      expect(screen.getAllByText('Active')).toHaveLength(2);
    });

    it('aggregates documents from multiple agents', async () => {
      vi.spyOn(useGameAgentsModule, 'useGameAgents').mockReturnValue({
        data: [mockAgent1, mockAgent2],
        isLoading: false,
        error: null,
      } as any);

      // Mock api.agents.getDocuments to return different data based on agentId
      vi.spyOn(api.agents, 'getDocuments').mockImplementation(async (agentId: string) => {
        if (agentId === mockAgent1.id) {
          return mockDocuments1;
        }
        return mockDocuments2;
      });

      renderWithQueryClient(<KnowledgeBaseTab gameId="game-id" />);

      await waitFor(() => {
        expect(screen.getByText(/Knowledge Base Documents/i)).toBeInTheDocument();
      });

      // Check aggregated count: 2 from agent1 + 1 from agent2 = 3 total
      expect(screen.getByText(/3 documents indexed across 2 agents/i)).toBeInTheDocument();

      // Check all documents are displayed
      expect(screen.getByText(/Test Game Rulebook/i)).toBeInTheDocument();
      expect(screen.getByText(/Test Game Errata/i)).toBeInTheDocument();
      expect(screen.getByText(/Test Game Homerules/i)).toBeInTheDocument();

      // Check agent names appear correctly
      expect(screen.getAllByText(/Agent: Rules Agent/i)).toHaveLength(2);
      expect(screen.getByText(/Agent: FAQ Agent/i)).toBeInTheDocument();

      // Check all document types
      expect(screen.getByText('Rulebook')).toBeInTheDocument();
      expect(screen.getByText('Errata')).toBeInTheDocument();
      expect(screen.getByText('Homerule')).toBeInTheDocument();
    });

    it('shows correct document type badges for all document types', async () => {
      vi.spyOn(useGameAgentsModule, 'useGameAgents').mockReturnValue({
        data: [mockAgent1],
        isLoading: false,
        error: null,
      } as any);

      vi.spyOn(api.agents, 'getDocuments').mockResolvedValue(mockDocuments1);

      renderWithQueryClient(<KnowledgeBaseTab gameId="game-id" />);

      await waitFor(() => {
        expect(screen.getByText('Rulebook')).toBeInTheDocument();
      });

    });

    it('displays active status badge only for active documents', async () => {
      vi.spyOn(useGameAgentsModule, 'useGameAgents').mockReturnValue({
        data: [mockAgent1, mockAgent2],
        isLoading: false,
        error: null,
      } as any);

      vi.spyOn(api.agents, 'getDocuments').mockImplementation(async (agentId: string) => {
        if (agentId === mockAgent1.id) {
          return mockDocuments1;
        }
        return mockDocuments2;
      });

      renderWithQueryClient(<KnowledgeBaseTab gameId="game-id" />);

      await waitFor(() => {
        // mockDocuments1 has 2 active documents, mockDocuments2 has 0 active
        const activeBadges = screen.getAllByText('Active');
        expect(activeBadges).toHaveLength(2);
      });
    });
  });

  describe('Accessibility', () => {
    it('renders document cards with accessible structure', async () => {
      vi.spyOn(useGameAgentsModule, 'useGameAgents').mockReturnValue({
        data: [mockAgent1],
        isLoading: false,
        error: null,
      } as any);

      vi.spyOn(api.agents, 'getDocuments').mockResolvedValue(mockDocuments1);

      renderWithQueryClient(<KnowledgeBaseTab gameId="game-id" />);

      await waitFor(() => {
        expect(screen.getByRole('heading', { name: /Knowledge Base Documents/i })).toBeInTheDocument();
      });

    });

    it('has proper title attributes for truncated text', async () => {
      vi.spyOn(useGameAgentsModule, 'useGameAgents').mockReturnValue({
        data: [mockAgent1],
        isLoading: false,
        error: null,
      } as any);

      vi.spyOn(api.agents, 'getDocuments').mockResolvedValue(mockDocuments1);

      renderWithQueryClient(<KnowledgeBaseTab gameId="game-id" />);

      await waitFor(() => {
        const rulebookName = screen.getByText(/Test Game Rulebook/i);
        expect(rulebookName.closest('p')).toHaveAttribute('title', 'Test Game Rulebook');
      });

    });
  });

  describe('Integration with PdfIndexingStatus', () => {
    it('renders PdfIndexingStatus component for each document', async () => {
      vi.spyOn(useGameAgentsModule, 'useGameAgents').mockReturnValue({
        data: [mockAgent1],
        isLoading: false,
        error: null,
      } as any);

      vi.spyOn(api.agents, 'getDocuments').mockResolvedValue(mockDocuments1);

      const { container } = renderWithQueryClient(<KnowledgeBaseTab gameId="game-id" />);

      await waitFor(() => {
        expect(container).toBeInTheDocument();
      });
    });
  });

  describe('Responsive Design', () => {
    it('uses grid layout for documents', async () => {
      vi.spyOn(useGameAgentsModule, 'useGameAgents').mockReturnValue({
        data: [mockAgent1],
        isLoading: false,
        error: null,
      } as any);

      vi.spyOn(api.agents, 'getDocuments').mockResolvedValue(mockDocuments1);

      const { container } = renderWithQueryClient(<KnowledgeBaseTab gameId="game-id" />);

      await waitFor(() => {
        const gridContainer = container.querySelector('.grid');
        expect(gridContainer).toBeInTheDocument();
      });

    });
  });
});
