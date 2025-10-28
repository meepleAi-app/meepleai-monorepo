import { render, screen, fireEvent } from '@testing-library/react';
import SearchModeToggle, { SearchMode } from '../SearchModeToggle';

describe('SearchModeToggle', () => {
  const mockOnChange = jest.fn();

  beforeEach(() => {
    jest.clearAllMocks();
  });

  describe('Rendering', () => {
    it('renders all three mode buttons', () => {
      render(<SearchModeToggle value={SearchMode.Hybrid} onChange={mockOnChange} />);

      expect(screen.getByRole('button', { name: /Semantic search mode/i })).toBeInTheDocument();
      expect(screen.getByRole('button', { name: /Hybrid search mode/i })).toBeInTheDocument();
      expect(screen.getByRole('button', { name: /Keyword search mode/i })).toBeInTheDocument();
    });

    it('renders search mode label', () => {
      render(<SearchModeToggle value={SearchMode.Hybrid} onChange={mockOnChange} />);

      expect(screen.getByText('Search Mode:')).toBeInTheDocument();
    });

    it('renders mode buttons in correct order (Semantic, Hybrid, Keyword)', () => {
      const { container } = render(<SearchModeToggle value={SearchMode.Hybrid} onChange={mockOnChange} />);

      const buttons = container.querySelectorAll('.mode-button');
      expect(buttons).toHaveLength(3);
      expect(buttons[0]).toHaveTextContent('Semantic');
      expect(buttons[1]).toHaveTextContent('Hybrid');
      expect(buttons[2]).toHaveTextContent('Keyword');
    });

    it('displays correct icons for each mode', () => {
      render(<SearchModeToggle value={SearchMode.Hybrid} onChange={mockOnChange} />);

      const semanticIcon = screen.getByRole('button', { name: /Semantic/i }).querySelector('.mode-icon');
      const hybridIcon = screen.getByRole('button', { name: /Hybrid/i }).querySelector('.mode-icon');
      const keywordIcon = screen.getByRole('button', { name: /Keyword/i }).querySelector('.mode-icon');

      expect(semanticIcon).toHaveTextContent('🧠');
      expect(hybridIcon).toHaveTextContent('⚡');
      expect(keywordIcon).toHaveTextContent('🔍');
    });

    it('applies custom className when provided', () => {
      const { container } = render(
        <SearchModeToggle value={SearchMode.Hybrid} onChange={mockOnChange} className="custom-class" />
      );

      const wrapper = container.querySelector('.search-mode-toggle');
      expect(wrapper).toHaveClass('custom-class');
    });
  });

  describe('Active State', () => {
    it('shows active state for Semantic mode when selected', () => {
      render(<SearchModeToggle value={SearchMode.Semantic} onChange={mockOnChange} />);

      const semanticButton = screen.getByRole('button', { name: /Semantic/i });
      expect(semanticButton).toHaveClass('active');
    });

    it('shows active state for Hybrid mode when selected', () => {
      render(<SearchModeToggle value={SearchMode.Hybrid} onChange={mockOnChange} />);

      const hybridButton = screen.getByRole('button', { name: /Hybrid/i });
      expect(hybridButton).toHaveClass('active');
    });

    it('shows active state for Keyword mode when selected', () => {
      render(<SearchModeToggle value={SearchMode.Keyword} onChange={mockOnChange} />);

      const keywordButton = screen.getByRole('button', { name: /Keyword/i });
      expect(keywordButton).toHaveClass('active');
    });

    it('shows active indicator (✓) only on selected mode', () => {
      render(<SearchModeToggle value={SearchMode.Hybrid} onChange={mockOnChange} />);

      const semanticButton = screen.getByRole('button', { name: /Semantic/i });
      const hybridButton = screen.getByRole('button', { name: /Hybrid/i });
      const keywordButton = screen.getByRole('button', { name: /Keyword/i });

      expect(semanticButton.querySelector('.active-indicator')).not.toBeInTheDocument();
      expect(hybridButton.querySelector('.active-indicator')).toBeInTheDocument();
      expect(hybridButton.querySelector('.active-indicator')).toHaveTextContent('✓');
      expect(keywordButton.querySelector('.active-indicator')).not.toBeInTheDocument();
    });

    it('does not show active class on inactive modes', () => {
      render(<SearchModeToggle value={SearchMode.Semantic} onChange={mockOnChange} />);

      const hybridButton = screen.getByRole('button', { name: /Hybrid/i });
      const keywordButton = screen.getByRole('button', { name: /Keyword/i });

      expect(hybridButton).not.toHaveClass('active');
      expect(keywordButton).not.toHaveClass('active');
    });
  });

  describe('User Interactions', () => {
    it('calls onChange with Semantic when Semantic button clicked', () => {
      render(<SearchModeToggle value={SearchMode.Hybrid} onChange={mockOnChange} />);

      const semanticButton = screen.getByRole('button', { name: /Semantic/i });
      fireEvent.click(semanticButton);

      expect(mockOnChange).toHaveBeenCalledWith(SearchMode.Semantic);
      expect(mockOnChange).toHaveBeenCalledTimes(1);
    });

    it('calls onChange with Hybrid when Hybrid button clicked', () => {
      render(<SearchModeToggle value={SearchMode.Semantic} onChange={mockOnChange} />);

      const hybridButton = screen.getByRole('button', { name: /Hybrid/i });
      fireEvent.click(hybridButton);

      expect(mockOnChange).toHaveBeenCalledWith(SearchMode.Hybrid);
      expect(mockOnChange).toHaveBeenCalledTimes(1);
    });

    it('calls onChange with Keyword when Keyword button clicked', () => {
      render(<SearchModeToggle value={SearchMode.Hybrid} onChange={mockOnChange} />);

      const keywordButton = screen.getByRole('button', { name: /Keyword/i });
      fireEvent.click(keywordButton);

      expect(mockOnChange).toHaveBeenCalledWith(SearchMode.Keyword);
      expect(mockOnChange).toHaveBeenCalledTimes(1);
    });

    it('allows clicking the same mode button (does not prevent redundant clicks)', () => {
      render(<SearchModeToggle value={SearchMode.Hybrid} onChange={mockOnChange} />);

      const hybridButton = screen.getByRole('button', { name: /Hybrid/i });
      fireEvent.click(hybridButton);

      expect(mockOnChange).toHaveBeenCalledWith(SearchMode.Hybrid);
      expect(mockOnChange).toHaveBeenCalledTimes(1);
    });

    it('handles rapid mode switching', () => {
      render(<SearchModeToggle value={SearchMode.Hybrid} onChange={mockOnChange} />);

      const semanticButton = screen.getByRole('button', { name: /Semantic/i });
      const keywordButton = screen.getByRole('button', { name: /Keyword/i });
      const hybridButton = screen.getByRole('button', { name: /Hybrid/i });

      fireEvent.click(semanticButton);
      fireEvent.click(keywordButton);
      fireEvent.click(hybridButton);
      fireEvent.click(semanticButton);

      expect(mockOnChange).toHaveBeenCalledTimes(4);
      expect(mockOnChange).toHaveBeenNthCalledWith(1, SearchMode.Semantic);
      expect(mockOnChange).toHaveBeenNthCalledWith(2, SearchMode.Keyword);
      expect(mockOnChange).toHaveBeenNthCalledWith(3, SearchMode.Hybrid);
      expect(mockOnChange).toHaveBeenNthCalledWith(4, SearchMode.Semantic);
    });
  });

  describe('Disabled State', () => {
    it('does not call onChange when disabled and button clicked', () => {
      render(<SearchModeToggle value={SearchMode.Hybrid} onChange={mockOnChange} disabled />);

      const semanticButton = screen.getByRole('button', { name: /Semantic/i });
      fireEvent.click(semanticButton);

      expect(mockOnChange).not.toHaveBeenCalled();
    });

    it('disables all buttons when disabled prop is true', () => {
      render(<SearchModeToggle value={SearchMode.Hybrid} onChange={mockOnChange} disabled />);

      const semanticButton = screen.getByRole('button', { name: /Semantic/i });
      const hybridButton = screen.getByRole('button', { name: /Hybrid/i });
      const keywordButton = screen.getByRole('button', { name: /Keyword/i });

      expect(semanticButton).toBeDisabled();
      expect(hybridButton).toBeDisabled();
      expect(keywordButton).toBeDisabled();
    });

    it('enables all buttons when disabled prop is false', () => {
      render(<SearchModeToggle value={SearchMode.Hybrid} onChange={mockOnChange} disabled={false} />);

      const semanticButton = screen.getByRole('button', { name: /Semantic/i });
      const hybridButton = screen.getByRole('button', { name: /Hybrid/i });
      const keywordButton = screen.getByRole('button', { name: /Keyword/i });

      expect(semanticButton).not.toBeDisabled();
      expect(hybridButton).not.toBeDisabled();
      expect(keywordButton).not.toBeDisabled();
    });
  });

  describe('Accessibility', () => {
    it('has proper ARIA labels for each mode button', () => {
      render(<SearchModeToggle value={SearchMode.Hybrid} onChange={mockOnChange} />);

      expect(screen.getByRole('button', { name: /Semantic search mode: Natural language understanding/i })).toBeInTheDocument();
      expect(screen.getByRole('button', { name: /Hybrid search mode: Best of both worlds/i })).toBeInTheDocument();
      expect(screen.getByRole('button', { name: /Keyword search mode: Exact term matching/i })).toBeInTheDocument();
    });

    it('has aria-pressed=true on selected mode', () => {
      render(<SearchModeToggle value={SearchMode.Hybrid} onChange={mockOnChange} />);

      const hybridButton = screen.getByRole('button', { name: /Hybrid/i });
      expect(hybridButton).toHaveAttribute('aria-pressed', 'true');
    });

    it('has aria-pressed=false on unselected modes', () => {
      render(<SearchModeToggle value={SearchMode.Hybrid} onChange={mockOnChange} />);

      const semanticButton = screen.getByRole('button', { name: /Semantic/i });
      const keywordButton = screen.getByRole('button', { name: /Keyword/i });

      expect(semanticButton).toHaveAttribute('aria-pressed', 'false');
      expect(keywordButton).toHaveAttribute('aria-pressed', 'false');
    });

    it('has role="group" with aria-label on button container', () => {
      render(<SearchModeToggle value={SearchMode.Hybrid} onChange={mockOnChange} />);

      const group = screen.getByRole('group', { name: 'Search mode selection' });
      expect(group).toBeInTheDocument();
    });

    it('displays tooltips with mode descriptions', () => {
      render(<SearchModeToggle value={SearchMode.Hybrid} onChange={mockOnChange} />);

      const semanticButton = screen.getByRole('button', { name: /Semantic/i });
      const hybridButton = screen.getByRole('button', { name: /Hybrid/i });
      const keywordButton = screen.getByRole('button', { name: /Keyword/i });

      expect(semanticButton).toHaveAttribute('title', 'Natural language understanding (AI embeddings)');
      expect(hybridButton).toHaveAttribute('title', 'Best of both worlds (default, 70% semantic + 30% keyword)');
      expect(keywordButton).toHaveAttribute('title', 'Exact term matching (faster, more precise)');
    });

    it('marks icons as aria-hidden', () => {
      const { container } = render(<SearchModeToggle value={SearchMode.Hybrid} onChange={mockOnChange} />);

      const icons = container.querySelectorAll('.mode-icon');
      icons.forEach(icon => {
        expect(icon).toHaveAttribute('aria-hidden', 'true');
      });
    });

    it('has aria-label on active indicator', () => {
      render(<SearchModeToggle value={SearchMode.Hybrid} onChange={mockOnChange} />);

      const hybridButton = screen.getByRole('button', { name: /Hybrid/i });
      const activeIndicator = hybridButton.querySelector('.active-indicator');

      expect(activeIndicator).toHaveAttribute('aria-label', 'Currently selected');
    });

    it('can focus on mode buttons for keyboard navigation', () => {
      render(<SearchModeToggle value={SearchMode.Hybrid} onChange={mockOnChange} />);

      const semanticButton = screen.getByRole('button', { name: /Semantic/i });
      semanticButton.focus();

      expect(semanticButton).toHaveFocus();
    });
  });

  describe('Button Types', () => {
    it('has type="button" to prevent form submission', () => {
      render(<SearchModeToggle value={SearchMode.Hybrid} onChange={mockOnChange} />);

      const semanticButton = screen.getByRole('button', { name: /Semantic/i });
      const hybridButton = screen.getByRole('button', { name: /Hybrid/i });
      const keywordButton = screen.getByRole('button', { name: /Keyword/i });

      expect(semanticButton).toHaveAttribute('type', 'button');
      expect(hybridButton).toHaveAttribute('type', 'button');
      expect(keywordButton).toHaveAttribute('type', 'button');
    });
  });
});
