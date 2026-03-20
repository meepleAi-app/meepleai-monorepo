/**
 * EmbeddingFlowBanner Tests
 *
 * Tests covering:
 * 1. Renders nothing when `flow` param is missing
 * 2. Renders banner with game name when `flow=embedding`
 * 3. Shows upload as done when on queue step
 * 4. Shows queue as in-progress by default on queue step
 * 5. Shows queue as done when queueStatus='Completed'
 * 6. Shows queue as failed when queueStatus='Failed'
 * 7. Can be dismissed (click X, disappears)
 */

import React from 'react';
import { render, screen, fireEvent, act } from '@testing-library/react';
import { describe, it, expect, vi, beforeEach } from 'vitest';

import { EmbeddingFlowBanner } from '../embedding-flow-banner';

// ============================================================================
// Mocks
// ============================================================================

const mockUseSearchParams = vi.fn();

vi.mock('next/navigation', () => ({
  useSearchParams: () => mockUseSearchParams(),
  useRouter: vi.fn(() => ({ push: vi.fn() })),
}));

// ============================================================================
// Helpers
// ============================================================================

function makeSearchParams(params: Record<string, string>) {
  return new URLSearchParams(params);
}

// ============================================================================
// Tests
// ============================================================================

describe('EmbeddingFlowBanner', () => {
  beforeEach(() => {
    vi.clearAllMocks();
    // Clear sessionStorage between tests
    sessionStorage.clear();
  });

  describe('visibility', () => {
    it('renders nothing when flow param is missing', () => {
      mockUseSearchParams.mockReturnValue(makeSearchParams({ gameName: 'Catan', gameId: '1' }));

      render(<EmbeddingFlowBanner currentStep="upload" />);

      expect(screen.queryByTestId('embedding-flow-banner')).not.toBeInTheDocument();
    });

    it('renders nothing when gameName param is missing', () => {
      mockUseSearchParams.mockReturnValue(makeSearchParams({ flow: 'embedding', gameId: '1' }));

      render(<EmbeddingFlowBanner currentStep="upload" />);

      expect(screen.queryByTestId('embedding-flow-banner')).not.toBeInTheDocument();
    });

    it('renders banner with game name when flow=embedding and gameName present', () => {
      mockUseSearchParams.mockReturnValue(
        makeSearchParams({ flow: 'embedding', gameName: 'Catan', gameId: '1' })
      );

      render(<EmbeddingFlowBanner currentStep="upload" />);

      expect(screen.getByTestId('embedding-flow-banner')).toBeInTheDocument();
      expect(screen.getByText('Catan — Flusso Embedding')).toBeInTheDocument();
    });

    it('renders nothing when flow param has a different value', () => {
      mockUseSearchParams.mockReturnValue(
        makeSearchParams({ flow: 'other', gameName: 'Catan', gameId: '1' })
      );

      render(<EmbeddingFlowBanner currentStep="upload" />);

      expect(screen.queryByTestId('embedding-flow-banner')).not.toBeInTheDocument();
    });
  });

  describe('step statuses on queue step', () => {
    beforeEach(() => {
      mockUseSearchParams.mockReturnValue(
        makeSearchParams({ flow: 'embedding', gameName: 'Scythe', gameId: '2' })
      );
    });

    it('shows upload step as done when on queue step', () => {
      render(<EmbeddingFlowBanner currentStep="queue" />);

      const uploadStep = screen.getByTestId('flow-step-upload');
      expect(uploadStep).toHaveAttribute('data-status', 'done');
    });

    it('shows queue step as in-progress by default (no queueStatus)', () => {
      render(<EmbeddingFlowBanner currentStep="queue" />);

      const queueStep = screen.getByTestId('flow-step-queue');
      expect(queueStep).toHaveAttribute('data-status', 'in-progress');
    });

    it('shows queue step as in-progress when queueStatus is Queued', () => {
      render(<EmbeddingFlowBanner currentStep="queue" queueStatus="Queued" />);

      const queueStep = screen.getByTestId('flow-step-queue');
      expect(queueStep).toHaveAttribute('data-status', 'in-progress');
    });

    it('shows queue step as in-progress when queueStatus is Processing', () => {
      render(<EmbeddingFlowBanner currentStep="queue" queueStatus="Processing" />);

      const queueStep = screen.getByTestId('flow-step-queue');
      expect(queueStep).toHaveAttribute('data-status', 'in-progress');
    });

    it('shows queue step as done when queueStatus is Completed', () => {
      render(<EmbeddingFlowBanner currentStep="queue" queueStatus="Completed" />);

      const queueStep = screen.getByTestId('flow-step-queue');
      expect(queueStep).toHaveAttribute('data-status', 'done');
    });

    it('shows queue step as failed when queueStatus is Failed', () => {
      render(<EmbeddingFlowBanner currentStep="queue" queueStatus="Failed" />);

      const queueStep = screen.getByTestId('flow-step-queue');
      expect(queueStep).toHaveAttribute('data-status', 'failed');
    });

    it('shows agent-test step as pending when on queue step', () => {
      render(<EmbeddingFlowBanner currentStep="queue" />);

      const agentStep = screen.getByTestId('flow-step-agent-test');
      expect(agentStep).toHaveAttribute('data-status', 'pending');
    });
  });

  describe('step statuses on upload step', () => {
    beforeEach(() => {
      mockUseSearchParams.mockReturnValue(
        makeSearchParams({ flow: 'embedding', gameName: 'Pandemic', gameId: '3' })
      );
    });

    it('shows upload as done when currentStep is upload', () => {
      render(<EmbeddingFlowBanner currentStep="upload" />);

      const uploadStep = screen.getByTestId('flow-step-upload');
      expect(uploadStep).toHaveAttribute('data-status', 'done');
    });

    it('shows queue and agent-test as pending when on upload step', () => {
      render(<EmbeddingFlowBanner currentStep="upload" />);

      expect(screen.getByTestId('flow-step-queue')).toHaveAttribute('data-status', 'pending');
      expect(screen.getByTestId('flow-step-agent-test')).toHaveAttribute('data-status', 'pending');
    });
  });

  describe('step statuses on agent-test step', () => {
    beforeEach(() => {
      mockUseSearchParams.mockReturnValue(
        makeSearchParams({ flow: 'embedding', gameName: 'Ark Nova', gameId: '4' })
      );
    });

    it('shows upload and queue as done when on agent-test step', () => {
      render(<EmbeddingFlowBanner currentStep="agent-test" />);

      expect(screen.getByTestId('flow-step-upload')).toHaveAttribute('data-status', 'done');
      expect(screen.getByTestId('flow-step-queue')).toHaveAttribute('data-status', 'done');
    });

    it('shows agent-test as in-progress when agentTestDone is false', () => {
      render(<EmbeddingFlowBanner currentStep="agent-test" agentTestDone={false} />);

      expect(screen.getByTestId('flow-step-agent-test')).toHaveAttribute(
        'data-status',
        'in-progress'
      );
    });

    it('shows agent-test as done when agentTestDone is true', () => {
      render(<EmbeddingFlowBanner currentStep="agent-test" agentTestDone={true} />);

      expect(screen.getByTestId('flow-step-agent-test')).toHaveAttribute('data-status', 'done');
    });
  });

  describe('dismiss behaviour', () => {
    beforeEach(() => {
      mockUseSearchParams.mockReturnValue(
        makeSearchParams({ flow: 'embedding', gameName: 'Wingspan', gameId: '5' })
      );
    });

    it('renders a dismiss button with accessible label', () => {
      render(<EmbeddingFlowBanner currentStep="upload" />);

      expect(screen.getByRole('button', { name: 'Chiudi banner' })).toBeInTheDocument();
    });

    it('hides the banner after clicking the dismiss button', () => {
      render(<EmbeddingFlowBanner currentStep="upload" />);

      expect(screen.getByTestId('embedding-flow-banner')).toBeInTheDocument();

      act(() => {
        fireEvent.click(screen.getByRole('button', { name: 'Chiudi banner' }));
      });

      expect(screen.queryByTestId('embedding-flow-banner')).not.toBeInTheDocument();
    });

    it('persists dismissal in sessionStorage', () => {
      render(<EmbeddingFlowBanner currentStep="upload" />);

      act(() => {
        fireEvent.click(screen.getByRole('button', { name: 'Chiudi banner' }));
      });

      expect(sessionStorage.getItem('embedding-flow-dismissed-5')).toBe('true');
    });
  });
});
