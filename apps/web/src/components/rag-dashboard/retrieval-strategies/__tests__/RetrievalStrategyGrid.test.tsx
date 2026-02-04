import { render, screen } from '@testing-library/react';
import userEvent from '@testing-library/user-event';
import { describe, it, expect, vi } from 'vitest';

import { RetrievalStrategyGrid } from '../RetrievalStrategyGrid';
import { RETRIEVAL_STRATEGIES, RETRIEVAL_STRATEGY_ORDER } from '../strategy-data';

describe('RetrievalStrategyGrid', () => {
  const mockOnStrategySelect = vi.fn();

  beforeEach(() => {
    mockOnStrategySelect.mockClear();
  });

  describe('rendering', () => {
    it('renders all 6 strategies', () => {
      render(<RetrievalStrategyGrid />);

      expect(screen.getByText('Hybrid Search')).toBeInTheDocument();
      expect(screen.getByText('Semantic Search')).toBeInTheDocument();
      expect(screen.getByText('Keyword Search')).toBeInTheDocument();
      expect(screen.getByText('Contextual Search')).toBeInTheDocument();
      expect(screen.getByText('Multi-Query')).toBeInTheDocument();
      expect(screen.getByText('Agentic RAG')).toBeInTheDocument();
    });

    it('renders strategies in correct order', () => {
      render(<RetrievalStrategyGrid />);

      const cards = screen.getAllByRole('button');
      expect(cards).toHaveLength(6);

      // Verify order matches RETRIEVAL_STRATEGY_ORDER
      RETRIEVAL_STRATEGY_ORDER.forEach((strategyId, index) => {
        const strategy = RETRIEVAL_STRATEGIES[strategyId];
        expect(cards[index]).toHaveAttribute(
          'aria-label',
          `${strategy.name} retrieval strategy`
        );
      });
    });

    it('renders custom strategies when provided', () => {
      const customStrategies = [RETRIEVAL_STRATEGIES.Hybrid, RETRIEVAL_STRATEGIES.Semantic];

      render(<RetrievalStrategyGrid strategies={customStrategies} />);

      expect(screen.getByText('Hybrid Search')).toBeInTheDocument();
      expect(screen.getByText('Semantic Search')).toBeInTheDocument();
      expect(screen.queryByText('Keyword Search')).not.toBeInTheDocument();
    });
  });

  describe('selection', () => {
    it('shows selected state for selectedStrategy prop', () => {
      render(<RetrievalStrategyGrid selectedStrategy="Hybrid" />);

      // Find the Hybrid card and check for selected indicator
      expect(screen.getByText('Selected')).toBeInTheDocument();
    });

    it('does not show selected state when no strategy is selected', () => {
      render(<RetrievalStrategyGrid selectedStrategy={null} />);

      expect(screen.queryByText('Selected')).not.toBeInTheDocument();
    });

    it('calls onStrategySelect when a card is clicked', async () => {
      const user = userEvent.setup();

      render(<RetrievalStrategyGrid onStrategySelect={mockOnStrategySelect} />);

      const semanticCard = screen.getByLabelText('Semantic Search retrieval strategy');
      await user.click(semanticCard);

      expect(mockOnStrategySelect).toHaveBeenCalledWith(RETRIEVAL_STRATEGIES.Semantic);
    });

    it('toggles selection on click', async () => {
      const user = userEvent.setup();

      render(<RetrievalStrategyGrid onStrategySelect={mockOnStrategySelect} />);

      const hybridCard = screen.getByLabelText('Hybrid Search retrieval strategy');

      // First click - select
      await user.click(hybridCard);
      expect(screen.getByText('Selected')).toBeInTheDocument();

      // Second click - deselect (toggle)
      await user.click(hybridCard);
      expect(screen.queryByText('Selected')).not.toBeInTheDocument();
    });

    it('updates selection when selectedStrategy prop changes', () => {
      const { rerender } = render(<RetrievalStrategyGrid selectedStrategy="Hybrid" />);

      expect(screen.getByText('Selected')).toBeInTheDocument();

      rerender(<RetrievalStrategyGrid selectedStrategy="Semantic" />);

      // Should still have exactly one Selected badge
      expect(screen.getByText('Selected')).toBeInTheDocument();
    });
  });

  describe('accessibility', () => {
    it('has listbox role', () => {
      render(<RetrievalStrategyGrid />);

      expect(screen.getByRole('listbox')).toBeInTheDocument();
    });

    it('has correct aria-label', () => {
      render(<RetrievalStrategyGrid />);

      expect(
        screen.getByRole('listbox', { name: 'RAG Retrieval Strategies' })
      ).toBeInTheDocument();
    });

    it('all cards are focusable', () => {
      render(<RetrievalStrategyGrid />);

      const cards = screen.getAllByRole('button');
      cards.forEach((card) => {
        expect(card).toHaveAttribute('tabIndex', '0');
      });
    });
  });

  describe('styling', () => {
    it('applies custom className', () => {
      const { container } = render(
        <RetrievalStrategyGrid className="custom-class" />
      );

      const grid = container.querySelector('[role="listbox"]');
      expect(grid).toHaveClass('custom-class');
    });

    it('has responsive grid classes', () => {
      const { container } = render(<RetrievalStrategyGrid />);

      const grid = container.querySelector('[role="listbox"]');
      expect(grid).toHaveClass('grid-cols-1');
      expect(grid).toHaveClass('sm:grid-cols-2');
      expect(grid).toHaveClass('lg:grid-cols-3');
      expect(grid).toHaveClass('xl:grid-cols-6');
    });
  });
});
