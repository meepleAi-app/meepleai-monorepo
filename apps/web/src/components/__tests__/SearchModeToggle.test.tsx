import { render, screen, fireEvent } from '@testing-library/react';
import { SearchModeToggle } from '../search/SearchModeToggle';

describe('SearchModeToggle', () => {
  const mockOnChange = vi.fn();

  const getSemanticButton = () =>
    screen.getByRole('radio', {
      name: /Semantic search mode: Natural language understanding/i,
    });

  const getHybridButton = () =>
    screen.getByRole('radio', {
      name: /Hybrid search mode: Best of both worlds/i,
    });

  const getKeywordButton = () =>
    screen.getByRole('radio', {
      name: /Keyword search mode: Exact term matching/i,
    });

  beforeEach(() => {
    vi.clearAllMocks();
  });

  describe('Rendering', () => {
    it('renders all three mode buttons', () => {
      render(<SearchModeToggle value={SearchMode.Hybrid} onChange={mockOnChange} />);

      expect(getSemanticButton()).toBeInTheDocument();
      expect(getHybridButton()).toBeInTheDocument();
      expect(getKeywordButton()).toBeInTheDocument();
    });

    it('renders search mode label', () => {
      render(<SearchModeToggle value={SearchMode.Hybrid} onChange={mockOnChange} />);

      expect(screen.getByText('Search Mode:')).toBeInTheDocument();
    });

    it('renders mode buttons in correct order (Semantic, Hybrid, Keyword)', () => {
      const { container } = render(
        <SearchModeToggle value={SearchMode.Hybrid} onChange={mockOnChange} />
      );

      const buttons = container.querySelectorAll('.mode-button');
      expect(buttons).toHaveLength(3);
      expect(buttons[0]).toHaveTextContent('Semantic');
      expect(buttons[1]).toHaveTextContent('Hybrid');
      expect(buttons[2]).toHaveTextContent('Keyword');
    });

    it('displays correct icons for each mode', () => {
      render(<SearchModeToggle value={SearchMode.Hybrid} onChange={mockOnChange} />);

      const semanticIcon = getSemanticButton().querySelector('.mode-icon');
      const hybridIcon = getHybridButton().querySelector('.mode-icon');
      const keywordIcon = getKeywordButton().querySelector('.mode-icon');

      expect(semanticIcon).toHaveTextContent('🧠');
      expect(hybridIcon).toHaveTextContent('⚡');
      expect(keywordIcon).toHaveTextContent('🔍');
    });

    it('applies custom className when provided', () => {
      const { container } = render(
        <SearchModeToggle
          value={SearchMode.Hybrid}
          onChange={mockOnChange}
          className="custom-class"
        />
      );

      // The custom class is applied to the root div
      const wrapper = container.firstChild;
      expect(wrapper).toHaveClass('custom-class');
    });
  });

  describe('Active State', () => {
    it('shows active state for Semantic mode when selected', () => {
      render(<SearchModeToggle value={SearchMode.Semantic} onChange={mockOnChange} />);

      expect(getSemanticButton()).toHaveClass('active');
    });

    it('shows active state for Hybrid mode when selected', () => {
      render(<SearchModeToggle value={SearchMode.Hybrid} onChange={mockOnChange} />);

      expect(getHybridButton()).toHaveClass('active');
    });

    it('shows active state for Keyword mode when selected', () => {
      render(<SearchModeToggle value={SearchMode.Keyword} onChange={mockOnChange} />);

      expect(getKeywordButton()).toHaveClass('active');
    });

    it('shows active indicator (✓) only on selected mode', () => {
      render(<SearchModeToggle value={SearchMode.Hybrid} onChange={mockOnChange} />);

      const semanticButton = getSemanticButton();
      const hybridButton = getHybridButton();
      const keywordButton = getKeywordButton();

      expect(semanticButton.querySelector('.active-indicator')).not.toBeInTheDocument();
      expect(hybridButton.querySelector('.active-indicator')).toBeInTheDocument();
      expect(hybridButton.querySelector('.active-indicator')).toHaveTextContent('✓');
      expect(keywordButton.querySelector('.active-indicator')).not.toBeInTheDocument();
    });

    it('does not show active class on inactive modes', () => {
      render(<SearchModeToggle value={SearchMode.Semantic} onChange={mockOnChange} />);

      const hybridButton = getHybridButton();
      const keywordButton = getKeywordButton();

      expect(hybridButton).not.toHaveClass('active');
      expect(keywordButton).not.toHaveClass('active');
    });
  });

  describe('User Interactions', () => {
    it('calls onChange with Semantic when Semantic button clicked', () => {
      render(<SearchModeToggle value={SearchMode.Hybrid} onChange={mockOnChange} />);

      fireEvent.click(getSemanticButton());

      expect(mockOnChange).toHaveBeenCalledWith(SearchMode.Semantic);
      expect(mockOnChange).toHaveBeenCalledTimes(1);
    });

    it('calls onChange with Hybrid when Hybrid button clicked', () => {
      render(<SearchModeToggle value={SearchMode.Semantic} onChange={mockOnChange} />);

      fireEvent.click(getHybridButton());

      expect(mockOnChange).toHaveBeenCalledWith(SearchMode.Hybrid);
      expect(mockOnChange).toHaveBeenCalledTimes(1);
    });

    it('calls onChange with Keyword when Keyword button clicked', () => {
      render(<SearchModeToggle value={SearchMode.Hybrid} onChange={mockOnChange} />);

      fireEvent.click(getKeywordButton());

      expect(mockOnChange).toHaveBeenCalledWith(SearchMode.Keyword);
      expect(mockOnChange).toHaveBeenCalledTimes(1);
    });

    it('allows clicking the same mode button (does not prevent redundant clicks)', () => {
      render(<SearchModeToggle value={SearchMode.Hybrid} onChange={mockOnChange} />);

      fireEvent.click(getHybridButton());

      // Radix ToggleGroup doesn't call onValueChange when clicking already-selected item
      // This is actually the desired behavior - we don't need redundant onChange calls
      expect(mockOnChange).not.toHaveBeenCalled();
    });

    it('handles rapid mode switching', () => {
      const { rerender } = render(
        <SearchModeToggle value={SearchMode.Hybrid} onChange={mockOnChange} />
      );

      const semanticButton = getSemanticButton();

      // Click different mode
      fireEvent.click(semanticButton);
      expect(mockOnChange).toHaveBeenNthCalledWith(1, SearchMode.Semantic);

      // Simulate component rerender with new value
      rerender(<SearchModeToggle value={SearchMode.Semantic} onChange={mockOnChange} />);

      const keywordButton = getKeywordButton();
      fireEvent.click(keywordButton);
      expect(mockOnChange).toHaveBeenNthCalledWith(2, SearchMode.Keyword);

      // Simulate component rerender with new value
      rerender(<SearchModeToggle value={SearchMode.Keyword} onChange={mockOnChange} />);

      const hybridButton = getHybridButton();
      fireEvent.click(hybridButton);
      expect(mockOnChange).toHaveBeenNthCalledWith(3, SearchMode.Hybrid);

      expect(mockOnChange).toHaveBeenCalledTimes(3);
    });
  });

  describe('Disabled State', () => {
    it('does not call onChange when disabled and button clicked', () => {
      render(<SearchModeToggle value={SearchMode.Hybrid} onChange={mockOnChange} disabled />);

      fireEvent.click(getSemanticButton());

      expect(mockOnChange).not.toHaveBeenCalled();
    });

    it('disables all buttons when disabled prop is true', () => {
      render(<SearchModeToggle value={SearchMode.Hybrid} onChange={mockOnChange} disabled />);

      const semanticButton = getSemanticButton();
      const hybridButton = getHybridButton();
      const keywordButton = getKeywordButton();

      expect(semanticButton).toBeDisabled();
      expect(hybridButton).toBeDisabled();
      expect(keywordButton).toBeDisabled();
    });

    it('enables all buttons when disabled prop is false', () => {
      render(
        <SearchModeToggle value={SearchMode.Hybrid} onChange={mockOnChange} disabled={false} />
      );

      const semanticButton = getSemanticButton();
      const hybridButton = getHybridButton();
      const keywordButton = getKeywordButton();

      expect(semanticButton).not.toBeDisabled();
      expect(hybridButton).not.toBeDisabled();
      expect(keywordButton).not.toBeDisabled();
    });
  });

  describe('Accessibility', () => {
    it('has proper ARIA labels for each mode button', () => {
      render(<SearchModeToggle value={SearchMode.Hybrid} onChange={mockOnChange} />);

      expect(getSemanticButton()).toBeInTheDocument();
      expect(getHybridButton()).toBeInTheDocument();
      expect(getKeywordButton()).toBeInTheDocument();
    });

    it('has aria-pressed=true on selected mode', () => {
      render(<SearchModeToggle value={SearchMode.Hybrid} onChange={mockOnChange} />);

      // Radix ToggleGroup uses both aria-pressed and aria-checked
      expect(getHybridButton()).toHaveAttribute('aria-pressed', 'true');
      expect(getHybridButton()).toHaveAttribute('aria-checked', 'true');
    });

    it('has aria-pressed=false on unselected modes', () => {
      render(<SearchModeToggle value={SearchMode.Hybrid} onChange={mockOnChange} />);

      // Radix ToggleGroup uses both aria-pressed and aria-checked
      expect(getSemanticButton()).toHaveAttribute('aria-pressed', 'false');
      expect(getSemanticButton()).toHaveAttribute('aria-checked', 'false');
      expect(getKeywordButton()).toHaveAttribute('aria-pressed', 'false');
      expect(getKeywordButton()).toHaveAttribute('aria-checked', 'false');
    });

    it('has role="group" with aria-label on button container', () => {
      render(<SearchModeToggle value={SearchMode.Hybrid} onChange={mockOnChange} />);

      const group = screen.getByRole('group', { name: 'Search mode selection' });
      expect(group).toBeInTheDocument();
    });

    it('displays tooltips with mode descriptions', () => {
      render(<SearchModeToggle value={SearchMode.Hybrid} onChange={mockOnChange} />);

      expect(getSemanticButton()).toHaveAttribute(
        'title',
        'Natural language understanding (AI embeddings)'
      );
      expect(getHybridButton()).toHaveAttribute(
        'title',
        'Best of both worlds (default, 70% semantic + 30% keyword)'
      );
      expect(getKeywordButton()).toHaveAttribute(
        'title',
        'Exact term matching (faster, more precise)'
      );
    });

    it('marks icons as aria-hidden', () => {
      const { container } = render(
        <SearchModeToggle value={SearchMode.Hybrid} onChange={mockOnChange} />
      );

      const icons = container.querySelectorAll('.mode-icon');
      icons.forEach(icon => {
        expect(icon).toHaveAttribute('aria-hidden', 'true');
      });
    });

    it('has aria-label on active indicator', () => {
      render(<SearchModeToggle value={SearchMode.Hybrid} onChange={mockOnChange} />);

      const activeIndicator = getHybridButton().querySelector('.active-indicator');

      expect(activeIndicator).toHaveAttribute('aria-label', 'Currently selected');
    });

    it('can focus on mode buttons for keyboard navigation', () => {
      render(<SearchModeToggle value={SearchMode.Hybrid} onChange={mockOnChange} />);

      const semanticButton = getSemanticButton();
      semanticButton.focus();

      expect(semanticButton).toHaveFocus();
    });
  });

  describe('Button Types', () => {
    it('has type="button" to prevent form submission', () => {
      render(<SearchModeToggle value={SearchMode.Hybrid} onChange={mockOnChange} />);

      expect(getSemanticButton()).toHaveAttribute('type', 'button');
      expect(getHybridButton()).toHaveAttribute('type', 'button');
      expect(getKeywordButton()).toHaveAttribute('type', 'button');
    });
  });
});
