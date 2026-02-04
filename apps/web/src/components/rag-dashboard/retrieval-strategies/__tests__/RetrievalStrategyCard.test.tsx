import { render, screen, fireEvent } from '@testing-library/react';
import userEvent from '@testing-library/user-event';
import { describe, it, expect, vi } from 'vitest';

import { RetrievalStrategyCard } from '../RetrievalStrategyCard';
import { RETRIEVAL_STRATEGIES } from '../strategy-data';

describe('RetrievalStrategyCard', () => {
  const mockStrategy = RETRIEVAL_STRATEGIES.Hybrid;
  const mockOnClick = vi.fn();

  beforeEach(() => {
    mockOnClick.mockClear();
  });

  describe('rendering', () => {
    it('renders strategy name correctly', () => {
      render(<RetrievalStrategyCard strategy={mockStrategy} />);
      expect(screen.getByText('Hybrid Search')).toBeInTheDocument();
    });

    it('renders strategy icon', () => {
      render(<RetrievalStrategyCard strategy={mockStrategy} />);
      expect(screen.getByText('🔀')).toBeInTheDocument();
    });

    it('renders strategy description', () => {
      render(<RetrievalStrategyCard strategy={mockStrategy} />);
      expect(
        screen.getByText('Vector + Keyword combined for best of both worlds')
      ).toBeInTheDocument();
    });

    it('renders metric badges', () => {
      render(<RetrievalStrategyCard strategy={mockStrategy} />);
      // Check for all metric indicators
      expect(screen.getByText('⏱️ Med')).toBeInTheDocument(); // Latency
      expect(screen.getByText('🎯 High')).toBeInTheDocument(); // Accuracy
      expect(screen.getByText('💰 Med')).toBeInTheDocument(); // Cost
    });

    it('renders recommended badge for Hybrid strategy', () => {
      render(<RetrievalStrategyCard strategy={mockStrategy} />);
      expect(screen.getByText('Recommended')).toBeInTheDocument();
    });

    it('does not render recommended badge for non-recommended strategies', () => {
      render(<RetrievalStrategyCard strategy={RETRIEVAL_STRATEGIES.Keyword} />);
      expect(screen.queryByText('Recommended')).not.toBeInTheDocument();
    });
  });

  describe('selection state', () => {
    it('shows selected indicator when isSelected is true', () => {
      render(<RetrievalStrategyCard strategy={mockStrategy} isSelected />);
      expect(screen.getByText('Selected')).toBeInTheDocument();
    });

    it('does not show selected indicator when isSelected is false', () => {
      render(<RetrievalStrategyCard strategy={mockStrategy} isSelected={false} />);
      expect(screen.queryByText('Selected')).not.toBeInTheDocument();
    });

    it('applies selected ring styles when selected', () => {
      const { container } = render(
        <RetrievalStrategyCard strategy={mockStrategy} isSelected />
      );
      const card = container.querySelector('[role="button"]');
      expect(card).toHaveClass('ring-2');
    });
  });

  describe('interactions', () => {
    it('calls onClick when clicked', async () => {
      const user = userEvent.setup();
      render(<RetrievalStrategyCard strategy={mockStrategy} onClick={mockOnClick} />);

      const card = screen.getByRole('button');
      await user.click(card);

      expect(mockOnClick).toHaveBeenCalledWith(mockStrategy);
    });

    it('calls onClick when Enter key is pressed', () => {
      render(<RetrievalStrategyCard strategy={mockStrategy} onClick={mockOnClick} />);

      const card = screen.getByRole('button');
      fireEvent.keyDown(card, { key: 'Enter' });

      expect(mockOnClick).toHaveBeenCalledWith(mockStrategy);
    });

    it('calls onClick when Space key is pressed', () => {
      render(<RetrievalStrategyCard strategy={mockStrategy} onClick={mockOnClick} />);

      const card = screen.getByRole('button');
      fireEvent.keyDown(card, { key: ' ' });

      expect(mockOnClick).toHaveBeenCalledWith(mockStrategy);
    });

    it('does not call onClick when other keys are pressed', () => {
      render(<RetrievalStrategyCard strategy={mockStrategy} onClick={mockOnClick} />);

      const card = screen.getByRole('button');
      fireEvent.keyDown(card, { key: 'Tab' });

      expect(mockOnClick).not.toHaveBeenCalled();
    });
  });

  describe('accessibility', () => {
    it('has correct role attribute', () => {
      render(<RetrievalStrategyCard strategy={mockStrategy} />);
      expect(screen.getByRole('button')).toBeInTheDocument();
    });

    it('has correct aria-label', () => {
      render(<RetrievalStrategyCard strategy={mockStrategy} />);
      expect(
        screen.getByLabelText('Hybrid Search retrieval strategy')
      ).toBeInTheDocument();
    });

    it('has correct aria-selected when selected', () => {
      render(<RetrievalStrategyCard strategy={mockStrategy} isSelected />);
      expect(screen.getByRole('button')).toHaveAttribute('aria-selected', 'true');
    });

    it('has correct aria-selected when not selected', () => {
      render(<RetrievalStrategyCard strategy={mockStrategy} isSelected={false} />);
      expect(screen.getByRole('button')).toHaveAttribute('aria-selected', 'false');
    });

    it('is focusable via tabIndex', () => {
      render(<RetrievalStrategyCard strategy={mockStrategy} />);
      expect(screen.getByRole('button')).toHaveAttribute('tabIndex', '0');
    });
  });

  describe('different strategies', () => {
    it.each([
      ['Hybrid', '🔀', 'Hybrid Search'],
      ['Semantic', '🧠', 'Semantic Search'],
      ['Keyword', '🔑', 'Keyword Search'],
      ['Contextual', '💬', 'Contextual Search'],
      ['MultiQuery', '🔄', 'Multi-Query'],
      ['Agentic', '🤖', 'Agentic RAG'],
    ] as const)('renders %s strategy correctly', (strategyId, expectedIcon, expectedName) => {
      const strategy = RETRIEVAL_STRATEGIES[strategyId];
      render(<RetrievalStrategyCard strategy={strategy} />);

      expect(screen.getByText(expectedIcon)).toBeInTheDocument();
      expect(screen.getByText(expectedName)).toBeInTheDocument();
    });
  });
});
