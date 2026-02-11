/**
 * RagReadyIndicator Component Tests (Issue #4065)
 *
 * Tests for Knowledge Base RAG readiness indicator UI.
 */

import React from 'react';
import { render, screen } from '@testing-library/react';
import userEvent from '@testing-library/user-event';
import { describe, it, expect, vi, beforeEach } from 'vitest';
import { axe } from 'jest-axe';

import { RagReadyIndicator } from '../RagReadyIndicator';

import type { UseEmbeddingStatusReturn } from '@/hooks/useEmbeddingStatus';

// Mock Next.js Link
vi.mock('next/link', () => ({
  default: ({ children, href, ...props }: { children: React.ReactNode; href: string }) => (
    <a href={href} {...props}>{children}</a>
  ),
}));

// Mock useEmbeddingStatus hook
const mockHookReturn: UseEmbeddingStatusReturn = {
  data: null,
  isLoading: false,
  isPolling: false,
  isReady: false,
  isFailed: false,
  stageLabel: 'In attesa...',
  chunkProgress: '0/0',
  error: null,
  refetch: vi.fn(),
};

vi.mock('@/hooks/useEmbeddingStatus', () => ({
  useEmbeddingStatus: vi.fn(() => mockHookReturn),
}));

import { useEmbeddingStatus } from '@/hooks/useEmbeddingStatus';

const mockUseEmbeddingStatus = vi.mocked(useEmbeddingStatus);

function setHookReturn(overrides: Partial<UseEmbeddingStatusReturn>) {
  const returnValue = { ...mockHookReturn, ...overrides };
  mockUseEmbeddingStatus.mockReturnValue(returnValue);
  return returnValue;
}

describe('RagReadyIndicator', () => {
  beforeEach(() => {
    vi.clearAllMocks();
    mockUseEmbeddingStatus.mockReturnValue({ ...mockHookReturn });
  });

  // ===== Loading state =====

  describe('Loading state', () => {
    it('shows loading spinner', () => {
      setHookReturn({ isLoading: true });

      render(<RagReadyIndicator gameId="game-1" />);

      expect(screen.getByTestId('rag-indicator-loading')).toBeInTheDocument();
      expect(screen.getByText('Verifica stato Knowledge Base...')).toBeInTheDocument();
    });
  });

  // ===== Processing state =====

  describe('Processing state', () => {
    it('renders stage indicators during embedding', () => {
      setHookReturn({
        isPolling: true,
        data: {
          status: 'Embedding',
          progress: 60,
          totalChunks: 100,
          processedChunks: 60,
          errorMessage: null,
        },
        stageLabel: 'Generazione embeddings: 60/100 (60%)',
        chunkProgress: '60/100',
      });

      render(<RagReadyIndicator gameId="game-1" />);

      expect(screen.getByTestId('rag-ready-indicator')).toBeInTheDocument();
      expect(screen.getByTestId('stage-label')).toHaveTextContent('Generazione embeddings: 60/100 (60%)');
      expect(screen.getByTestId('chunk-progress')).toHaveTextContent('60/100');
    });

    it('renders all 4 embedding stage indicators', () => {
      setHookReturn({
        isPolling: true,
        data: {
          status: 'Chunking',
          progress: 40,
          totalChunks: 100,
          processedChunks: 40,
          errorMessage: null,
        },
        stageLabel: 'Creazione chunks: 40/100',
        chunkProgress: '40/100',
      });

      render(<RagReadyIndicator gameId="game-1" />);

      expect(screen.getByTestId('embedding-stage-extracting')).toBeInTheDocument();
      expect(screen.getByTestId('embedding-stage-chunking')).toBeInTheDocument();
      expect(screen.getByTestId('embedding-stage-embedding')).toBeInTheDocument();
      expect(screen.getByTestId('embedding-stage-completed')).toBeInTheDocument();
    });

    it('renders progress bar during processing', () => {
      setHookReturn({
        isPolling: true,
        data: {
          status: 'Embedding',
          progress: 75,
          totalChunks: 200,
          processedChunks: 150,
          errorMessage: null,
        },
        stageLabel: 'Generazione embeddings: 150/200 (75%)',
        chunkProgress: '150/200',
      });

      render(<RagReadyIndicator gameId="game-1" />);

      expect(screen.getByRole('progressbar')).toBeInTheDocument();
    });
  });

  // ===== Ready state =====

  describe('Ready state', () => {
    it('shows "Knowledge Base Pronta" badge', () => {
      setHookReturn({
        isReady: true,
        data: {
          status: 'Completed',
          progress: 100,
          totalChunks: 120,
          processedChunks: 120,
          errorMessage: null,
          gameName: 'Catan',
        },
      });

      render(<RagReadyIndicator gameId="game-1" gameName="Catan" />);

      expect(screen.getByTestId('rag-ready-badge')).toBeInTheDocument();
      expect(screen.getByText('Knowledge Base Pronta')).toBeInTheDocument();
    });

    it('renders "Chat Now" button with correct href', () => {
      setHookReturn({
        isReady: true,
        data: {
          status: 'Completed',
          progress: 100,
          totalChunks: 120,
          processedChunks: 120,
          errorMessage: null,
        },
      });

      render(<RagReadyIndicator gameId="game-123" />);

      const chatButton = screen.getByTestId('chat-now-button');
      expect(chatButton).toBeInTheDocument();

      const link = chatButton.closest('a');
      expect(link).toHaveAttribute('href', '/chat?gameId=game-123');
    });

    it('does not show stage indicators when ready', () => {
      setHookReturn({ isReady: true });

      render(<RagReadyIndicator gameId="game-1" />);

      expect(screen.queryByTestId('embedding-stage-extracting')).not.toBeInTheDocument();
    });

    it('shows game name in ready message', () => {
      setHookReturn({
        isReady: true,
        data: {
          status: 'Completed',
          progress: 100,
          totalChunks: 120,
          processedChunks: 120,
          errorMessage: null,
          gameName: 'Wingspan',
        },
      });

      render(<RagReadyIndicator gameId="game-1" gameName="Wingspan" />);

      expect(screen.getByText(/Wingspan.*pronto per le domande AI/i)).toBeInTheDocument();
    });
  });

  // ===== Failed state =====

  describe('Failed state', () => {
    it('shows error message on failure', () => {
      setHookReturn({
        isFailed: true,
        data: {
          status: 'Failed',
          progress: 45,
          totalChunks: 100,
          processedChunks: 45,
          errorMessage: 'Out of memory during embedding',
        },
      });

      render(<RagReadyIndicator gameId="game-1" />);

      expect(screen.getByTestId('rag-failed')).toBeInTheDocument();
      expect(screen.getByText('Elaborazione fallita')).toBeInTheDocument();
      expect(screen.getByText('Out of memory during embedding')).toBeInTheDocument();
    });

    it('shows retry upload button when callback provided', () => {
      const onRetryUpload = vi.fn();
      setHookReturn({
        isFailed: true,
        data: {
          status: 'Failed',
          progress: 0,
          totalChunks: 0,
          processedChunks: 0,
          errorMessage: 'Failed',
        },
      });

      render(<RagReadyIndicator gameId="game-1" onRetryUpload={onRetryUpload} />);

      expect(screen.getByTestId('retry-upload-button')).toBeInTheDocument();
    });

    it('calls onRetryUpload when retry button clicked', async () => {
      const user = userEvent.setup();
      const onRetryUpload = vi.fn();
      setHookReturn({
        isFailed: true,
        data: {
          status: 'Failed',
          progress: 0,
          totalChunks: 0,
          processedChunks: 0,
          errorMessage: 'Error',
        },
      });

      render(<RagReadyIndicator gameId="game-1" onRetryUpload={onRetryUpload} />);

      await user.click(screen.getByTestId('retry-upload-button'));

      expect(onRetryUpload).toHaveBeenCalledTimes(1);
    });

    it('does not show retry button without callback', () => {
      setHookReturn({
        isFailed: true,
        data: {
          status: 'Failed',
          progress: 0,
          totalChunks: 0,
          processedChunks: 0,
          errorMessage: 'Error',
        },
      });

      render(<RagReadyIndicator gameId="game-1" />);

      expect(screen.queryByTestId('retry-upload-button')).not.toBeInTheDocument();
    });
  });

  // ===== Network error =====

  describe('Network error', () => {
    it('shows connection error state', () => {
      setHookReturn({
        error: new Error('Connection refused'),
      });

      render(<RagReadyIndicator gameId="game-1" />);

      expect(screen.getByTestId('rag-indicator-error')).toBeInTheDocument();
      expect(screen.getByText('Errore di connessione')).toBeInTheDocument();
      expect(screen.getByText('Connection refused')).toBeInTheDocument();
    });

    it('has retry button for connection errors', () => {
      const mockRefetch = vi.fn();
      setHookReturn({
        error: new Error('Timeout'),
        refetch: mockRefetch,
      });

      render(<RagReadyIndicator gameId="game-1" />);

      expect(screen.getByText('Riprova')).toBeInTheDocument();
    });
  });

  // ===== Pending state =====

  describe('Pending state', () => {
    it('renders nothing when Pending and not polling', () => {
      setHookReturn({ isPolling: false });

      const { container } = render(<RagReadyIndicator gameId="game-1" />);

      expect(container.innerHTML).toBe('');
    });
  });

  // ===== Accessibility =====

  describe('Accessibility', () => {
    it('has no axe violations during processing', async () => {
      setHookReturn({
        isPolling: true,
        data: {
          status: 'Embedding',
          progress: 60,
          totalChunks: 100,
          processedChunks: 60,
          errorMessage: null,
        },
        stageLabel: 'Generating embeddings...',
        chunkProgress: '60/100',
      });

      const { container } = render(<RagReadyIndicator gameId="game-1" />);

      const results = await axe(container);
      expect(results).toHaveNoViolations();
    });

    it('has no axe violations when ready', async () => {
      setHookReturn({
        isReady: true,
        data: {
          status: 'Completed',
          progress: 100,
          totalChunks: 120,
          processedChunks: 120,
          errorMessage: null,
        },
      });

      const { container } = render(<RagReadyIndicator gameId="game-1" gameName="Catan" />);

      const results = await axe(container);
      expect(results).toHaveNoViolations();
    });

    it('has no axe violations when failed', async () => {
      setHookReturn({
        isFailed: true,
        data: {
          status: 'Failed',
          progress: 0,
          totalChunks: 0,
          processedChunks: 0,
          errorMessage: 'Error',
        },
      });

      const { container } = render(<RagReadyIndicator gameId="game-1" onRetryUpload={vi.fn()} />);

      const results = await axe(container);
      expect(results).toHaveNoViolations();
    });

    it('uses aria-live regions for status updates', () => {
      setHookReturn({
        isPolling: true,
        data: {
          status: 'Embedding',
          progress: 50,
          totalChunks: 100,
          processedChunks: 50,
          errorMessage: null,
        },
        stageLabel: 'Generating embeddings...',
        chunkProgress: '50/100',
      });

      render(<RagReadyIndicator gameId="game-1" />);

      const liveRegion = screen.getByTestId('stage-label');
      expect(liveRegion).toHaveAttribute('aria-live', 'polite');
    });

    it('has role="alert" for error states', () => {
      setHookReturn({
        isFailed: true,
        data: {
          status: 'Failed',
          progress: 0,
          totalChunks: 0,
          processedChunks: 0,
          errorMessage: 'Error',
        },
      });

      render(<RagReadyIndicator gameId="game-1" />);

      expect(screen.getByRole('alert')).toBeInTheDocument();
    });
  });
});
