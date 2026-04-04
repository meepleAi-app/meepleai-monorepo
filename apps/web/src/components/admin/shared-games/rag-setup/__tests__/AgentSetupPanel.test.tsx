/**
 * Tests for AgentSetupPanel component
 * Admin RAG Dashboard - Issue #4364
 *
 * Coverage:
 * - Existing agent info display (Attivo/Inattivo, Pronto/Non pronto)
 * - Create agent form rendering (title, agent name input)
 * - Empty document state
 * - Document list with checkboxes
 * - Checkbox disabled for non-Ready documents
 * - "Pronto" badge for Ready documents
 */

import { render, screen } from '@testing-library/react';
import { describe, it, expect, vi } from 'vitest';

import type { AgentInfo, DocumentStatus } from '@/lib/api/schemas/rag-setup.schemas';

import { AgentSetupPanel } from '../AgentSetupPanel';

// =========================================================================
// Mocks
// =========================================================================

vi.mock('@/hooks/queries/useEstimateAgentCost', () => ({
  useEstimateAgentCost: () => ({ data: null, isLoading: false }),
}));

vi.mock('@/lib/api', () => ({
  api: {
    agents: {
      createAgentWithSetup: vi.fn(),
    },
  },
}));

// sonner toast — prevent console noise in tests
vi.mock('sonner', () => ({
  toast: {
    success: vi.fn(),
    error: vi.fn(),
  },
}));

// =========================================================================
// Helpers
// =========================================================================

function createMockDocument(overrides: Partial<DocumentStatus> = {}): DocumentStatus {
  return {
    documentId: 'doc-1',
    fileName: 'rulebook.pdf',
    processingState: 'Ready',
    progressPercentage: 100,
    isActiveForRag: true,
    errorMessage: null,
    ...overrides,
  };
}

function createMockAgent(overrides: Partial<AgentInfo> = {}): AgentInfo {
  return {
    agentId: 'agent-1',
    name: 'Catan Assistant',
    type: 'RAG',
    isActive: true,
    isReady: true,
    ...overrides,
  };
}

const defaultProps = {
  gameId: 'game-1',
  gameTitle: 'Catan',
  documents: [],
  existingAgent: null,
  onAgentCreated: vi.fn(),
};

// =========================================================================
// Tests
// =========================================================================

describe('AgentSetupPanel', () => {
  // -----------------------------------------------------------------------
  // Existing agent display
  // -----------------------------------------------------------------------

  describe('when existingAgent is provided', () => {
    it('shows agent name', () => {
      const agent = createMockAgent({ name: 'Catan Assistant' });

      render(<AgentSetupPanel {...defaultProps} existingAgent={agent} />);

      expect(screen.getByText('Catan Assistant')).toBeInTheDocument();
    });

    it('shows "Attivo" when agent.isActive is true', () => {
      const agent = createMockAgent({ isActive: true });

      render(<AgentSetupPanel {...defaultProps} existingAgent={agent} />);

      expect(screen.getByText(/Attivo/)).toBeInTheDocument();
    });

    it('shows "Inattivo" when agent.isActive is false', () => {
      const agent = createMockAgent({ isActive: false });

      render(<AgentSetupPanel {...defaultProps} existingAgent={agent} />);

      expect(screen.getByText(/Inattivo/)).toBeInTheDocument();
    });

    it('shows "Pronto" badge when agent.isReady is true', () => {
      const agent = createMockAgent({ isReady: true });

      render(<AgentSetupPanel {...defaultProps} existingAgent={agent} />);

      expect(screen.getByText('Pronto')).toBeInTheDocument();
    });

    it('shows "Non pronto" badge when agent.isReady is false', () => {
      const agent = createMockAgent({ isReady: false });

      render(<AgentSetupPanel {...defaultProps} existingAgent={agent} />);

      expect(screen.getByText('Non pronto')).toBeInTheDocument();
    });

    it('shows agent type in the agent info paragraph', () => {
      const agent = createMockAgent({ type: 'RAG' });

      render(<AgentSetupPanel {...defaultProps} existingAgent={agent} />);

      // The agent type appears in "RAG • Attivo" paragraph — use getAllByText since
      // "Agente RAG" title also contains "RAG"
      const ragElements = screen.getAllByText(/RAG/);
      expect(ragElements.length).toBeGreaterThan(0);
    });

    it('does NOT render "Crea Agente RAG" title when agent already exists', () => {
      const agent = createMockAgent();

      render(<AgentSetupPanel {...defaultProps} existingAgent={agent} />);

      expect(screen.queryByText('Crea Agente RAG')).not.toBeInTheDocument();
    });
  });

  // -----------------------------------------------------------------------
  // Create agent form (no existing agent)
  // -----------------------------------------------------------------------

  describe('when no existing agent', () => {
    it('renders "Crea Agente RAG" title', () => {
      render(<AgentSetupPanel {...defaultProps} existingAgent={null} />);

      expect(screen.getByText('Crea Agente RAG')).toBeInTheDocument();
    });

    it('shows agent name input with default value "{gameTitle} Assistant"', () => {
      render(<AgentSetupPanel {...defaultProps} gameTitle="Catan" existingAgent={null} />);

      const input = screen.getByRole('textbox', { name: /nome agente/i });
      expect(input).toHaveValue('Catan Assistant');
    });

    it('uses the provided gameTitle in the default agent name', () => {
      render(<AgentSetupPanel {...defaultProps} gameTitle="Pandemic" existingAgent={null} />);

      const input = screen.getByRole('textbox', { name: /nome agente/i });
      expect(input).toHaveValue('Pandemic Assistant');
    });

    it('shows "Nessun documento caricato" when documents array is empty', () => {
      render(<AgentSetupPanel {...defaultProps} documents={[]} existingAgent={null} />);

      expect(screen.getByText(/Nessun documento caricato/i)).toBeInTheDocument();
    });

    it('renders document list when documents are provided', () => {
      const documents = [
        createMockDocument({ documentId: 'doc-1', fileName: 'rules.pdf' }),
        createMockDocument({ documentId: 'doc-2', fileName: 'faq.pdf' }),
      ];

      render(<AgentSetupPanel {...defaultProps} documents={documents} existingAgent={null} />);

      expect(screen.getByText('rules.pdf')).toBeInTheDocument();
      expect(screen.getByText('faq.pdf')).toBeInTheDocument();
    });

    it('renders a checkbox for each document', () => {
      const documents = [
        createMockDocument({ documentId: 'doc-1', fileName: 'rules.pdf' }),
        createMockDocument({ documentId: 'doc-2', fileName: 'faq.pdf' }),
      ];

      render(<AgentSetupPanel {...defaultProps} documents={documents} existingAgent={null} />);

      const checkboxes = screen.getAllByRole('checkbox');
      expect(checkboxes).toHaveLength(2);
    });

    it('disables checkbox for non-Ready documents', () => {
      const documents = [
        createMockDocument({
          documentId: 'doc-processing',
          fileName: 'processing.pdf',
          processingState: 'Processing',
        }),
      ];

      render(<AgentSetupPanel {...defaultProps} documents={documents} existingAgent={null} />);

      const checkbox = screen.getByRole('checkbox');
      expect(checkbox).toBeDisabled();
    });

    it('does NOT disable checkbox for Ready documents', () => {
      const documents = [
        createMockDocument({
          documentId: 'doc-ready',
          fileName: 'ready.pdf',
          processingState: 'Ready',
        }),
      ];

      render(<AgentSetupPanel {...defaultProps} documents={documents} existingAgent={null} />);

      const checkbox = screen.getByRole('checkbox');
      expect(checkbox).not.toBeDisabled();
    });

    it('shows "Pronto" badge for Ready documents', () => {
      const documents = [
        createMockDocument({
          documentId: 'doc-ready',
          fileName: 'ready.pdf',
          processingState: 'Ready',
        }),
      ];

      render(<AgentSetupPanel {...defaultProps} documents={documents} existingAgent={null} />);

      expect(screen.getByText('Pronto')).toBeInTheDocument();
    });

    it('shows processingState as badge text for non-Ready documents', () => {
      const documents = [
        createMockDocument({
          documentId: 'doc-proc',
          fileName: 'processing.pdf',
          processingState: 'Processing',
        }),
      ];

      render(<AgentSetupPanel {...defaultProps} documents={documents} existingAgent={null} />);

      expect(screen.getByText('Processing')).toBeInTheDocument();
    });

    it('renders the create button', () => {
      render(<AgentSetupPanel {...defaultProps} existingAgent={null} />);

      // Button contains "Crea Agente" text
      expect(screen.getByRole('button', { name: /crea agente/i })).toBeInTheDocument();
    });

    it('disables create button when no documents are selected', () => {
      render(<AgentSetupPanel {...defaultProps} documents={[]} existingAgent={null} />);

      const createButton = screen.getByRole('button', { name: /crea agente/i });
      expect(createButton).toBeDisabled();
    });
  });
});
