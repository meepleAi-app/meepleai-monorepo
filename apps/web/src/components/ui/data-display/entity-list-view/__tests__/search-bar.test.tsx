/**
 * Tests for SearchBar component
 */

import { render, screen } from '@testing-library/react';
import userEvent from '@testing-library/user-event';
import { SearchBar } from '../components/search-bar';
import { vi } from 'vitest';

describe('SearchBar', () => {
  const mockOnChange = vi.fn();

  beforeEach(() => {
    vi.clearAllMocks();
  });

  describe('Rendering', () => {
    it('should render search input', () => {
      render(<SearchBar value="" onChange={mockOnChange} />);

      expect(screen.getByRole('searchbox')).toBeInTheDocument();
    });

    it('should render with custom placeholder', () => {
      render(<SearchBar value="" onChange={mockOnChange} placeholder="Search games..." />);

      expect(screen.getByPlaceholderText('Search games...')).toBeInTheDocument();
    });

    it('should render search icon', () => {
      const { container } = render(<SearchBar value="" onChange={mockOnChange} />);

      const searchIcon = container.querySelector('svg');
      expect(searchIcon).toBeInTheDocument();
    });
  });

  describe('Input Handling', () => {
    it('should call onChange when typing', async () => {
      const user = userEvent.setup();

      // Use controlled component pattern
      let currentValue = '';
      const handleChange = (val: string) => {
        currentValue = val;
        mockOnChange(val);
      };

      const { rerender } = render(<SearchBar value={currentValue} onChange={handleChange} />);

      const input = screen.getByRole('searchbox');

      // Type each character and rerender
      for (const char of 'gloom') {
        await user.type(input, char);
        rerender(<SearchBar value={currentValue} onChange={handleChange} />);
      }

      expect(mockOnChange).toHaveBeenCalledTimes(5);
      expect(mockOnChange).toHaveBeenLastCalledWith('gloom');
    });

    it('should display current value', () => {
      render(<SearchBar value="test query" onChange={mockOnChange} />);

      expect(screen.getByRole('searchbox')).toHaveValue('test query');
    });
  });

  describe('Clear Button', () => {
    it('should show clear button when value is non-empty', () => {
      render(<SearchBar value="test" onChange={mockOnChange} />);

      expect(screen.getByLabelText(/clear search/i)).toBeInTheDocument();
    });

    it('should not show clear button when value is empty', () => {
      render(<SearchBar value="" onChange={mockOnChange} />);

      expect(screen.queryByLabelText(/clear search/i)).not.toBeInTheDocument();
    });

    it('should call onChange with empty string when clear clicked', async () => {
      const user = userEvent.setup();
      render(<SearchBar value="test" onChange={mockOnChange} />);

      const clearButton = screen.getByLabelText(/clear search/i);
      await user.click(clearButton);

      expect(mockOnChange).toHaveBeenCalledWith('');
    });
  });

  describe('Keyboard Shortcut', () => {
    it('should focus input on Cmd/Ctrl + K', async () => {
      const user = userEvent.setup();
      render(<SearchBar value="" onChange={mockOnChange} />);

      const input = screen.getByRole('searchbox');

      // Simulate Ctrl+K (or Cmd+K)
      await user.keyboard('{Control>}k{/Control}');

      expect(input).toHaveFocus();
    });
  });

  describe('Accessibility', () => {
    it('should have searchbox role', () => {
      render(<SearchBar value="" onChange={mockOnChange} />);

      expect(screen.getByRole('searchbox')).toBeInTheDocument();
    });

    it('should have aria-label', () => {
      render(<SearchBar value="" onChange={mockOnChange} />);

      expect(screen.getByLabelText('Search')).toBeInTheDocument();
    });
  });
});
