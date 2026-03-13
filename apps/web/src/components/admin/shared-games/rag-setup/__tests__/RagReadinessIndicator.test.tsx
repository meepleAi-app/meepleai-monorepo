/**
 * Tests for RagReadinessIndicator component
 * Admin RAG Dashboard - Issue #4364
 *
 * Coverage:
 * - Status badge rendering for each readiness state (Italian text)
 * - Horizontal stepper labels (Documenti, Elaborazione, Agente, Chat)
 * - Per-document progress bars when processing
 * - Failed documents warning section
 */

import { render, screen } from '@testing-library/react';
import { describe, it, expect } from 'vitest';

import type { GameRagReadiness, DocumentStatus } from '@/lib/api/schemas/rag-setup.schemas';
import { READINESS_STATES } from '@/lib/api/schemas/rag-setup.schemas';

import { RagReadinessIndicator } from '../RagReadinessIndicator';

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

function createMockReadiness(overrides: Partial<GameRagReadiness> = {}): GameRagReadiness {
  return {
    gameId: 'game-1',
    gameTitle: 'Catan',
    gameStatus: 'Active',
    totalDocuments: 0,
    readyDocuments: 0,
    processingDocuments: 0,
    failedDocuments: 0,
    documents: [],
    linkedAgent: null,
    overallReadiness: READINESS_STATES.NO_DOCUMENTS,
    ...overrides,
  };
}

// =========================================================================
// Tests
// =========================================================================

describe('RagReadinessIndicator', () => {
  // -----------------------------------------------------------------------
  // Status badges
  // -----------------------------------------------------------------------

  describe('Status badge', () => {
    it('renders "Nessun documento" badge for NO_DOCUMENTS state', () => {
      const readiness = createMockReadiness({
        overallReadiness: READINESS_STATES.NO_DOCUMENTS,
      });

      render(<RagReadinessIndicator readiness={readiness} />);

      expect(screen.getByText('Nessun documento')).toBeInTheDocument();
    });

    it('renders "Elaborazione in corso" badge for DOCUMENTS_PROCESSING state', () => {
      const readiness = createMockReadiness({
        overallReadiness: READINESS_STATES.DOCUMENTS_PROCESSING,
        processingDocuments: 1,
        documents: [
          createMockDocument({
            processingState: 'Processing',
            progressPercentage: 50,
          }),
        ],
      });

      render(<RagReadinessIndicator readiness={readiness} />);

      expect(screen.getByText('Elaborazione in corso')).toBeInTheDocument();
    });

    it('renders "Elaborazione fallita" badge for DOCUMENTS_FAILED state', () => {
      const readiness = createMockReadiness({
        overallReadiness: READINESS_STATES.DOCUMENTS_FAILED,
        failedDocuments: 1,
        documents: [
          createMockDocument({
            processingState: 'Failed',
            progressPercentage: 0,
            errorMessage: 'OCR failed',
          }),
        ],
      });

      render(<RagReadinessIndicator readiness={readiness} />);

      expect(screen.getByText('Elaborazione fallita')).toBeInTheDocument();
    });

    it('renders "Pronto per agente" badge for READY_FOR_AGENT state', () => {
      const readiness = createMockReadiness({
        overallReadiness: READINESS_STATES.READY_FOR_AGENT,
        totalDocuments: 1,
        readyDocuments: 1,
        documents: [createMockDocument()],
      });

      render(<RagReadinessIndicator readiness={readiness} />);

      expect(screen.getByText('Pronto per agente')).toBeInTheDocument();
    });

    it('renders "Operativo" badge for FULLY_OPERATIONAL state', () => {
      const readiness = createMockReadiness({
        overallReadiness: READINESS_STATES.FULLY_OPERATIONAL,
        totalDocuments: 1,
        readyDocuments: 1,
        documents: [createMockDocument()],
        linkedAgent: {
          agentId: 'agent-1',
          name: 'Catan Assistant',
          type: 'RAG',
          isActive: true,
          isReady: true,
        },
      });

      render(<RagReadinessIndicator readiness={readiness} />);

      expect(screen.getByText('Operativo')).toBeInTheDocument();
    });
  });

  // -----------------------------------------------------------------------
  // Horizontal stepper
  // -----------------------------------------------------------------------

  describe('Stepper labels', () => {
    it('renders all 4 stepper labels', () => {
      const readiness = createMockReadiness();

      render(<RagReadinessIndicator readiness={readiness} />);

      expect(screen.getByText('Documenti')).toBeInTheDocument();
      expect(screen.getByText('Elaborazione')).toBeInTheDocument();
      expect(screen.getByText('Agente')).toBeInTheDocument();
      expect(screen.getByText('Chat')).toBeInTheDocument();
    });
  });

  // -----------------------------------------------------------------------
  // Per-document progress bars
  // -----------------------------------------------------------------------

  describe('Per-document progress', () => {
    it('shows per-document progress section when processingDocuments > 0', () => {
      const readiness = createMockReadiness({
        overallReadiness: READINESS_STATES.DOCUMENTS_PROCESSING,
        processingDocuments: 1,
        documents: [
          createMockDocument({
            documentId: 'doc-processing',
            fileName: 'rules_v2.pdf',
            processingState: 'Processing',
            progressPercentage: 42,
          }),
        ],
      });

      render(<RagReadinessIndicator readiness={readiness} />);

      expect(screen.getByText('rules_v2.pdf')).toBeInTheDocument();
      expect(screen.getByText('42%')).toBeInTheDocument();
    });

    it('does NOT show progress section when processingDocuments is 0', () => {
      const readiness = createMockReadiness({
        overallReadiness: READINESS_STATES.READY_FOR_AGENT,
        processingDocuments: 0,
        documents: [createMockDocument({ fileName: 'should_not_appear.pdf' })],
      });

      render(<RagReadinessIndicator readiness={readiness} />);

      // The file name should not appear because the progress section is hidden
      expect(screen.queryByText('should_not_appear.pdf')).not.toBeInTheDocument();
    });

    it('shows multiple processing documents', () => {
      const readiness = createMockReadiness({
        overallReadiness: READINESS_STATES.DOCUMENTS_PROCESSING,
        processingDocuments: 2,
        documents: [
          createMockDocument({
            documentId: 'doc-a',
            fileName: 'doc_a.pdf',
            processingState: 'Processing',
            progressPercentage: 20,
          }),
          createMockDocument({
            documentId: 'doc-b',
            fileName: 'doc_b.pdf',
            processingState: 'Processing',
            progressPercentage: 75,
          }),
        ],
      });

      render(<RagReadinessIndicator readiness={readiness} />);

      expect(screen.getByText('doc_a.pdf')).toBeInTheDocument();
      expect(screen.getByText('doc_b.pdf')).toBeInTheDocument();
      expect(screen.getByText('20%')).toBeInTheDocument();
      expect(screen.getByText('75%')).toBeInTheDocument();
    });
  });

  // -----------------------------------------------------------------------
  // Failed documents warning
  // -----------------------------------------------------------------------

  describe('Failed documents warning', () => {
    it('shows failed documents warning when failedDocuments > 0', () => {
      const readiness = createMockReadiness({
        overallReadiness: READINESS_STATES.DOCUMENTS_FAILED,
        failedDocuments: 1,
        documents: [
          createMockDocument({
            processingState: 'Failed',
            progressPercentage: 0,
            errorMessage: null,
          }),
        ],
      });

      render(<RagReadinessIndicator readiness={readiness} />);

      expect(screen.getByText(/1 documento\/i fallito\/i/)).toBeInTheDocument();
    });

    it('shows error message in failed documents warning', () => {
      const errorMessage = 'Impossibile estrarre testo dal PDF';
      const readiness = createMockReadiness({
        overallReadiness: READINESS_STATES.DOCUMENTS_FAILED,
        failedDocuments: 1,
        documents: [
          createMockDocument({
            processingState: 'Failed',
            progressPercentage: 0,
            errorMessage,
          }),
        ],
      });

      render(<RagReadinessIndicator readiness={readiness} />);

      expect(screen.getByText(new RegExp(errorMessage))).toBeInTheDocument();
    });

    it('does NOT show failed documents warning when failedDocuments is 0', () => {
      const readiness = createMockReadiness({
        overallReadiness: READINESS_STATES.READY_FOR_AGENT,
        failedDocuments: 0,
        documents: [createMockDocument()],
      });

      render(<RagReadinessIndicator readiness={readiness} />);

      expect(screen.queryByText(/fallito/i)).not.toBeInTheDocument();
    });
  });

  // -----------------------------------------------------------------------
  // General rendering
  // -----------------------------------------------------------------------

  describe('General rendering', () => {
    it('renders "Stato RAG" label', () => {
      const readiness = createMockReadiness();

      render(<RagReadinessIndicator readiness={readiness} />);

      expect(screen.getByText('Stato RAG')).toBeInTheDocument();
    });
  });
});
