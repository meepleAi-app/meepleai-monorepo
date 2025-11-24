import React from 'react';
import { render, screen, fireEvent, waitFor } from '@testing-library/react';
import userEvent from '@testing-library/user-event';
import { MentionInput } from '../MentionInput';
import { api, type UserSearchResult } from '@/lib/api';

// Mock the API module
vi.mock('@/lib/api', () => ({
  api: {
    get: vi.fn(),
  },
}));

// Mock useDebounce hook
vi.mock('@/hooks/useDebounce', () => ({
  useDebounce: (value: string, delay: number) => value,
}));

const mockUsers: UserSearchResult[] = [
  { id: '1', displayName: 'Alice Anderson', email: 'alice@test.com' },
  { id: '2', displayName: 'Bob Builder', email: 'bob@test.com' },
  { id: '3', displayName: 'Charlie Chen', email: 'charlie@test.com' },
];

      fireEvent.keyDown(textarea, { key: 'ArrowDown' });

      await waitFor(() => {
        const options = screen.getAllByRole('option');
        expect(options[options.length - 1]).toHaveAttribute('aria-selected', 'true');
      });
    });

    it('selects user with Enter key', async () => {
      const textarea = screen.getByRole('combobox');

      fireEvent.keyDown(textarea, { key: 'Enter' });

      await waitFor(() => {
        expect(mockOnChange).toHaveBeenCalledWith('@Alice Anderson ');
        expect(screen.queryByRole('listbox')).not.toBeInTheDocument();
      });
    });

    it('closes dropdown with Escape key', async () => {
      const textarea = screen.getByRole('combobox');

      expect(screen.getByRole('listbox')).toBeInTheDocument();

      fireEvent.keyDown(textarea, { key: 'Escape' });

      await waitFor(() => {
        expect(screen.queryByRole('listbox')).not.toBeInTheDocument();
      });
    });

    it('closes dropdown with Tab key', async () => {
      const textarea = screen.getByRole('combobox');

      expect(screen.getByRole('listbox')).toBeInTheDocument();

      fireEvent.keyDown(textarea, { key: 'Tab' });

      await waitFor(() => {
        expect(screen.queryByRole('listbox')).not.toBeInTheDocument();
      });
    });
  });

  describe('User Selection', () => {
    it('inserts mention when clicking on user', async () => {
      // Use rerender to properly update the component with new value
      const { rerender } = render(<MentionInput value="" onChange={mockOnChange} />);
      const textarea = screen.getByRole('combobox');

      // Start fresh and type the mention to trigger dropdown
      fireEvent.change(textarea, {
        target: { value: 'Hello @al', selectionStart: 9 },
      });

      // Simulate parent component updating the value
      rerender(<MentionInput value="Hello @al" onChange={mockOnChange} />);

      await waitFor(
        () => {
          expect(screen.getByRole('listbox')).toBeInTheDocument();
        },
        { timeout: 3000 }
      );

      // Get any option with Alice in it
      const options = screen.getAllByRole('option');
      const aliceOption = options[0]; // First option should be Alice
      fireEvent.click(aliceOption);

      await waitFor(() => {
        expect(mockOnChange).toHaveBeenCalledWith('Hello @Alice Anderson ');
        expect(screen.queryByRole('listbox')).not.toBeInTheDocument();
      });
    });

    it('replaces @query with @DisplayName followed by space', async () => {
      const { rerender } = render(<MentionInput value="" onChange={mockOnChange} />);
      const textarea = screen.getByRole('combobox');

      // Start fresh and type the mention to trigger dropdown
      fireEvent.change(textarea, { target: { value: '@bob', selectionStart: 4 } });

      // Simulate parent component updating the value
      rerender(<MentionInput value="@bob" onChange={mockOnChange} />);

      await waitFor(
        () => {
          expect(screen.getByRole('listbox')).toBeInTheDocument();
        },
        { timeout: 3000 }
      );

      const options = screen.getAllByRole('option');
      const bobOption = options.find((opt) => opt.textContent?.includes('Bob Builder'));
      fireEvent.click(bobOption!);

      await waitFor(() => {
        expect(mockOnChange).toHaveBeenCalledWith('@Bob Builder ');
      });
    });

    it('preserves text before and after mention', async () => {
      const { rerender } = render(<MentionInput value="" onChange={mockOnChange} />);
      const textarea = screen.getByRole('combobox') as HTMLTextAreaElement;

      // Start fresh and type the mention to trigger dropdown
      fireEvent.change(textarea, {
        target: { value: 'Hello @al world', selectionStart: 9 },
      });

      // Simulate parent component updating the value
      rerender(<MentionInput value="Hello @al world" onChange={mockOnChange} />);

      // Ensure cursor position is maintained after rerender
      textarea.setSelectionRange(9, 9);

      await waitFor(
        () => {
          expect(screen.getByRole('listbox')).toBeInTheDocument();
        },
        { timeout: 3000 }
      );

      const options = screen.getAllByRole('option');
      const aliceOption = options[0];
      fireEvent.click(aliceOption);

      await waitFor(() => {
        expect(mockOnChange).toHaveBeenCalledWith('Hello @Alice Anderson  world');
      });
    });

    it('updates selectedIndex on mouse enter', async () => {
      render(<MentionInput value="" onChange={mockOnChange} />);
      const textarea = screen.getByRole('combobox');

      fireEvent.change(textarea, { target: { value: '@al', selectionStart: 3 } });

      await waitFor(() => {
        expect(screen.getByRole('listbox')).toBeInTheDocument();
      });

      const options = screen.getAllByRole('option');
      expect(options[0]).toHaveAttribute('aria-selected', 'true');

      fireEvent.mouseEnter(options[2]);

      await waitFor(() => {
        expect(options[2]).toHaveAttribute('aria-selected', 'true');
      });
    });
  });

  describe('ARIA Attributes', () => {
    it('sets correct ARIA attributes on textarea', () => {
      render(<MentionInput value="" onChange={mockOnChange} />);
      const textarea = screen.getByRole('combobox');

      expect(textarea).toHaveAttribute('role', 'combobox');
      expect(textarea).toHaveAttribute('aria-expanded', 'false');
      expect(textarea).toHaveAttribute('aria-autocomplete', 'list');
    });

    it('updates aria-expanded when dropdown opens', async () => {
      render(<MentionInput value="" onChange={mockOnChange} />);
      const textarea = screen.getByRole('combobox');

      fireEvent.change(textarea, { target: { value: '@al', selectionStart: 3 } });

      await waitFor(() => {
        expect(textarea).toHaveAttribute('aria-expanded', 'true');
        expect(textarea).toHaveAttribute('aria-controls', 'mention-dropdown');
      });
    });

    it('sets aria-activedescendant to selected option', async () => {
      render(<MentionInput value="" onChange={mockOnChange} />);
      const textarea = screen.getByRole('combobox');

      fireEvent.change(textarea, { target: { value: '@al', selectionStart: 3 } });

      await waitFor(() => {
        expect(textarea).toHaveAttribute('aria-activedescendant', 'mention-option-0');
      });

      fireEvent.keyDown(textarea, { key: 'ArrowDown' });

      await waitFor(() => {
        expect(textarea).toHaveAttribute('aria-activedescendant', 'mention-option-1');
      });
    });

    it('sets correct ARIA attributes on dropdown options', async () => {
      render(<MentionInput value="" onChange={mockOnChange} />);
      const textarea = screen.getByRole('combobox');

      fireEvent.change(textarea, { target: { value: '@al', selectionStart: 3 } });

      await waitFor(() => {
        const listbox = screen.getByRole('listbox');
        expect(listbox).toHaveAttribute('id', 'mention-dropdown');

        const options = screen.getAllByRole('option');
        expect(options[0]).toHaveAttribute('id', 'mention-option-0');
        expect(options[0]).toHaveAttribute('aria-selected', 'true');
        expect(options[1]).toHaveAttribute('aria-selected', 'false');
      });
    });
  });

  describe('Edge Cases', () => {
    it('handles rapid typing with debounced searches', async () => {
      // For this test, we want to verify the debounce hook is called with correct params
      // Since we mocked useDebounce to return immediately, all queries will trigger
      // This test verifies that each distinct query triggers a search
      // Use minLength=1 to allow single-char queries for this test
      render(<MentionInput value="" onChange={mockOnChange} minLength={1} />);
      const textarea = screen.getByRole('combobox');

      // Simulate rapid typing - each change will trigger due to mock
      fireEvent.change(textarea, { target: { value: '@a', selectionStart: 2 } });

      await waitFor(
        () => {
          expect(mockApiGet).toHaveBeenCalledWith('/api/v1/users/search?query=a');
        },
        { timeout: 3000 }
      );

      fireEvent.change(textarea, { target: { value: '@al', selectionStart: 3 } });

      await waitFor(
        () => {
          expect(mockApiGet).toHaveBeenCalledWith('/api/v1/users/search?query=al');
        },
        { timeout: 3000 }
      );

      fireEvent.change(textarea, { target: { value: '@ali', selectionStart: 4 } });

      await waitFor(
        () => {
          expect(mockApiGet).toHaveBeenCalledWith('/api/v1/users/search?query=ali');
        },
        { timeout: 3000 }
      );

      // Verify all three searches were made (due to mocked debounce)
      expect(mockApiGet).toHaveBeenCalledTimes(3);
    });

    it('handles empty query gracefully', async () => {
      render(<MentionInput value="" onChange={mockOnChange} />);
      const textarea = screen.getByRole('combobox');

      fireEvent.change(textarea, { target: { value: '@', selectionStart: 1 } });

      await waitFor(() => {
        expect(mockApiGet).not.toHaveBeenCalled();
        expect(screen.queryByRole('listbox')).not.toBeInTheDocument();
      });
    });

    it('handles cursor at position 0', () => {
      render(<MentionInput value="@test" onChange={mockOnChange} />);
      const textarea = screen.getByRole('combobox');

      fireEvent.change(textarea, { target: { value: '@test', selectionStart: 0 } });

      expect(mockApiGet).not.toHaveBeenCalled();
    });

    it('highlights matching text in search results', async () => {
      render(<MentionInput value="" onChange={mockOnChange} />);
      const textarea = screen.getByRole('combobox');

      fireEvent.change(textarea, { target: { value: '@ali', selectionStart: 4 } });

      await waitFor(() => {
        const dropdown = screen.getByRole('listbox');
        const strongTags = dropdown.querySelectorAll('strong');
        expect(strongTags.length).toBeGreaterThan(0);
        expect(strongTags[0]).toHaveTextContent('Ali');
      });
    });

    it('handles multiple @ symbols correctly', async () => {
      render(<MentionInput value="" onChange={mockOnChange} />);
      const textarea = screen.getByRole('combobox');

      fireEvent.change(textarea, {
        target: { value: 'Hello @bob and @alice', selectionStart: 21 },
      });

      await waitFor(() => {
        expect(mockApiGet).toHaveBeenCalledWith('/api/v1/users/search?query=alice');
      });
    });
  });
});
