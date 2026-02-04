import { render, screen, fireEvent, waitFor } from '@testing-library/react';
import userEvent from '@testing-library/user-event';
import { describe, it, expect, vi } from 'vitest';

import { StrategyDetailModal } from '../StrategyDetailModal';

describe('StrategyDetailModal', () => {
  const mockOnClose = vi.fn();
  const mockOnConfigureClick = vi.fn();

  beforeEach(() => {
    mockOnClose.mockClear();
    mockOnConfigureClick.mockClear();
  });

  describe('rendering', () => {
    it('renders nothing when strategy is null', () => {
      const { container } = render(
        <StrategyDetailModal
          strategy={null}
          isOpen={true}
          onClose={mockOnClose}
        />
      );
      expect(container.querySelector('[role="dialog"]')).not.toBeInTheDocument();
    });

    it('renders nothing when isOpen is false', () => {
      const { container } = render(
        <StrategyDetailModal
          strategy="Hybrid"
          isOpen={false}
          onClose={mockOnClose}
        />
      );
      expect(container.querySelector('[role="dialog"]')).not.toBeInTheDocument();
    });

    it('renders modal when isOpen is true with valid strategy', async () => {
      render(
        <StrategyDetailModal
          strategy="Hybrid"
          isOpen={true}
          onClose={mockOnClose}
        />
      );

      await waitFor(() => {
        expect(screen.getByRole('dialog')).toBeInTheDocument();
      });
      expect(screen.getByText('Hybrid Search')).toBeInTheDocument();
    });

    it('renders strategy icon', async () => {
      render(
        <StrategyDetailModal
          strategy="Hybrid"
          isOpen={true}
          onClose={mockOnClose}
        />
      );

      await waitFor(() => {
        expect(screen.getByText('🔀')).toBeInTheDocument();
      });
    });

    it('renders tabs for navigation', async () => {
      render(
        <StrategyDetailModal
          strategy="Hybrid"
          isOpen={true}
          onClose={mockOnClose}
        />
      );

      await waitFor(() => {
        expect(screen.getByRole('tab', { name: 'Overview' })).toBeInTheDocument();
        expect(screen.getByRole('tab', { name: 'Pros & Cons' })).toBeInTheDocument();
        expect(screen.getByRole('tab', { name: 'Flow' })).toBeInTheDocument();
        expect(screen.getByRole('tab', { name: 'Example' })).toBeInTheDocument();
      });
    });
  });

  describe('tab navigation', () => {
    it('shows overview content by default', async () => {
      render(
        <StrategyDetailModal
          strategy="Hybrid"
          isOpen={true}
          onClose={mockOnClose}
        />
      );

      await waitFor(() => {
        expect(screen.getByText('Best Use Cases')).toBeInTheDocument();
      });
    });

    it('switches to pros/cons tab when clicked', async () => {
      const user = userEvent.setup();

      render(
        <StrategyDetailModal
          strategy="Hybrid"
          isOpen={true}
          onClose={mockOnClose}
        />
      );

      await waitFor(() => {
        expect(screen.getByRole('tab', { name: 'Pros & Cons' })).toBeInTheDocument();
      });

      await user.click(screen.getByRole('tab', { name: 'Pros & Cons' }));

      await waitFor(() => {
        expect(screen.getByText('Advantages')).toBeInTheDocument();
        expect(screen.getByText('Considerations')).toBeInTheDocument();
      });
    });

    it('switches to flow tab when clicked', async () => {
      const user = userEvent.setup();

      render(
        <StrategyDetailModal
          strategy="Hybrid"
          isOpen={true}
          onClose={mockOnClose}
        />
      );

      await waitFor(() => {
        expect(screen.getByRole('tab', { name: 'Flow' })).toBeInTheDocument();
      });

      await user.click(screen.getByRole('tab', { name: 'Flow' }));

      await waitFor(() => {
        expect(screen.getByText('Processing Flow')).toBeInTheDocument();
      });
    });

    it('switches to example tab when clicked', async () => {
      const user = userEvent.setup();

      render(
        <StrategyDetailModal
          strategy="Hybrid"
          isOpen={true}
          onClose={mockOnClose}
        />
      );

      await waitFor(() => {
        expect(screen.getByRole('tab', { name: 'Example' })).toBeInTheDocument();
      });

      await user.click(screen.getByRole('tab', { name: 'Example' }));

      await waitFor(() => {
        expect(screen.getByText('Example Interaction')).toBeInTheDocument();
      });
    });
  });

  describe('interactions', () => {
    it('calls onClose when close button is clicked', async () => {
      const user = userEvent.setup();

      render(
        <StrategyDetailModal
          strategy="Hybrid"
          isOpen={true}
          onClose={mockOnClose}
        />
      );

      await waitFor(() => {
        expect(screen.getByLabelText('Close modal')).toBeInTheDocument();
      });

      await user.click(screen.getByLabelText('Close modal'));

      expect(mockOnClose).toHaveBeenCalled();
    });

    it('calls onConfigureClick when configure button is clicked', async () => {
      const user = userEvent.setup();

      render(
        <StrategyDetailModal
          strategy="Hybrid"
          isOpen={true}
          onClose={mockOnClose}
          onConfigureClick={mockOnConfigureClick}
        />
      );

      await waitFor(() => {
        expect(screen.getByRole('button', { name: /Configure/i })).toBeInTheDocument();
      });

      await user.click(screen.getByRole('button', { name: /Configure/i }));

      expect(mockOnConfigureClick).toHaveBeenCalledWith('Hybrid');
    });

    it('does not render configure button when onConfigureClick is not provided', async () => {
      render(
        <StrategyDetailModal
          strategy="Hybrid"
          isOpen={true}
          onClose={mockOnClose}
        />
      );

      await waitFor(() => {
        expect(screen.getByRole('dialog')).toBeInTheDocument();
      });

      expect(screen.queryByRole('button', { name: /Configure/i })).not.toBeInTheDocument();
    });
  });

  describe('different strategies', () => {
    it.each([
      ['Hybrid', 'Hybrid Search', '🔀'],
      ['Semantic', 'Semantic Search', '🧠'],
      ['Keyword', 'Keyword Search', '🔑'],
      ['Contextual', 'Contextual Search', '💬'],
      ['MultiQuery', 'Multi-Query', '🔄'],
      ['Agentic', 'Agentic RAG', '🤖'],
    ] as const)('renders %s strategy correctly', async (strategyId, expectedTitle, expectedIcon) => {
      render(
        <StrategyDetailModal
          strategy={strategyId}
          isOpen={true}
          onClose={mockOnClose}
        />
      );

      await waitFor(() => {
        expect(screen.getByText(expectedTitle)).toBeInTheDocument();
        expect(screen.getByText(expectedIcon)).toBeInTheDocument();
      });
    });
  });

  describe('accessibility', () => {
    it('has dialog role', async () => {
      render(
        <StrategyDetailModal
          strategy="Hybrid"
          isOpen={true}
          onClose={mockOnClose}
        />
      );

      await waitFor(() => {
        expect(screen.getByRole('dialog')).toBeInTheDocument();
      });
    });

    it('has aria-describedby for modal description', async () => {
      render(
        <StrategyDetailModal
          strategy="Hybrid"
          isOpen={true}
          onClose={mockOnClose}
        />
      );

      await waitFor(() => {
        const dialog = screen.getByRole('dialog');
        expect(dialog).toHaveAttribute('aria-describedby', 'strategy-modal-description');
      });
    });

    it('close button has aria-label', async () => {
      render(
        <StrategyDetailModal
          strategy="Hybrid"
          isOpen={true}
          onClose={mockOnClose}
        />
      );

      await waitFor(() => {
        expect(screen.getByLabelText('Close modal')).toBeInTheDocument();
      });
    });
  });

  describe('metrics display', () => {
    it('displays strategy metrics in footer', async () => {
      render(
        <StrategyDetailModal
          strategy="Hybrid"
          isOpen={true}
          onClose={mockOnClose}
        />
      );

      await waitFor(() => {
        expect(screen.getByText(/Latency/)).toBeInTheDocument();
        expect(screen.getByText(/Accuracy/)).toBeInTheDocument();
        expect(screen.getByText(/Cost/)).toBeInTheDocument();
      });
    });
  });
});
