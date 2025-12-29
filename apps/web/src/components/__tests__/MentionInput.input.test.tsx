import type { Mock } from 'vitest';
import React from 'react';
import { render, screen, fireEvent, waitFor } from '@testing-library/react';
import userEvent from '@testing-library/user-event';
import { MentionInput } from '../chat/MentionInput';
import { api, type UserSearchResult } from '@/lib/api';

// Mock the API module
vi.mock('@/lib/api', () => ({
  api: {
    auth: {
      searchUsers: vi.fn(),
    },
  },
}));

// Mock the logger
const mockLoggerError = vi.fn();
vi.mock('@/lib/logger', () => ({
  logger: {
    error: (...args: unknown[]) => mockLoggerError(...args),
    warn: vi.fn(),
    info: vi.fn(),
    debug: vi.fn(),
  },
  createErrorContext: vi.fn().mockReturnValue({}),
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

describe('MentionInput', () => {
  const mockOnChange = vi.fn();
  const mockSearchUsers = api.auth.searchUsers as Mock<typeof api.auth.searchUsers>;

  beforeEach(() => {
    vi.clearAllMocks();
    mockSearchUsers.mockResolvedValue(mockUsers);
  });

  afterEach(() => {
    vi.clearAllTimers();
  });

  describe('Basic Rendering', () => {
    it('renders textarea with initial value', () => {
      render(<MentionInput value="test comment" onChange={mockOnChange} />);
      const textarea = screen.getByRole('combobox');
      expect(textarea).toBeInTheDocument();
      expect(textarea).toHaveValue('test comment');
    });

    it('renders with custom placeholder', () => {
      render(<MentionInput value="" onChange={mockOnChange} placeholder="Custom placeholder" />);
      expect(screen.getByPlaceholderText('Custom placeholder')).toBeInTheDocument();
    });

    it('renders with default placeholder when not provided', () => {
      render(<MentionInput value="" onChange={mockOnChange} />);
      expect(
        screen.getByPlaceholderText('Write a comment and mention users with @username')
      ).toBeInTheDocument();
    });

    it('respects disabled prop', () => {
      render(<MentionInput value="" onChange={mockOnChange} disabled={true} />);
      expect(screen.getByRole('combobox')).toBeDisabled();
    });
  });

  describe('Text Input', () => {
    it('calls onChange when user types', async () => {
      render(<MentionInput value="" onChange={mockOnChange} />);
      const textarea = screen.getByRole('combobox');

      await userEvent.type(textarea, 'Hello');

      expect(mockOnChange).toHaveBeenCalled();
    });

    it('updates value when typing regular text', async () => {
      const { rerender } = render(<MentionInput value="" onChange={mockOnChange} />);
      const textarea = screen.getByRole('combobox');

      fireEvent.change(textarea, { target: { value: 'Hello world', selectionStart: 11 } });

      expect(mockOnChange).toHaveBeenCalledWith('Hello world');
    });
  });

  describe('Mention Detection', () => {
    it('detects @ character and triggers search with minLength requirement', async () => {
      render(<MentionInput value="" onChange={mockOnChange} minLength={2} />);
      const textarea = screen.getByRole('combobox');

      // Type @ followed by 2 characters
      fireEvent.change(textarea, { target: { value: '@al', selectionStart: 3 } });

      await waitFor(() => {
        expect(mockSearchUsers).toHaveBeenCalledWith('al');
      });
    });

    it('does not trigger search with query shorter than minLength', async () => {
      render(<MentionInput value="" onChange={mockOnChange} minLength={2} />);
      const textarea = screen.getByRole('combobox');

      fireEvent.change(textarea, { target: { value: '@a', selectionStart: 2 } });

      await waitFor(() => {
        expect(mockSearchUsers).not.toHaveBeenCalled();
      });
    });

    it('extracts query correctly after @ character', async () => {
      render(<MentionInput value="" onChange={mockOnChange} />);
      const textarea = screen.getByRole('combobox');

      fireEvent.change(textarea, { target: { value: 'Hello @alice', selectionStart: 12 } });

      await waitFor(() => {
        expect(mockSearchUsers).toHaveBeenCalledWith('alice');
      });
    });

    it('stops mention detection at whitespace', async () => {
      render(<MentionInput value="" onChange={mockOnChange} />);
      const textarea = screen.getByRole('combobox');

      fireEvent.change(textarea, {
        target: { value: 'Hello world @al', selectionStart: 15 },
      });

      await waitFor(() => {
        expect(mockSearchUsers).toHaveBeenCalledWith('al');
      });
    });

    it('stops mention detection at newline', async () => {
      render(<MentionInput value="" onChange={mockOnChange} />);
      const textarea = screen.getByRole('combobox');

      fireEvent.change(textarea, {
        target: { value: 'Hello\n@alice', selectionStart: 12 },
      });

      await waitFor(() => {
        expect(mockSearchUsers).toHaveBeenCalledWith('alice');
      });
    });
  });

  describe('Autocomplete Dropdown', () => {
    it('shows dropdown with search results', async () => {
      render(<MentionInput value="" onChange={mockOnChange} />);
      const textarea = screen.getByRole('combobox');

      fireEvent.change(textarea, { target: { value: '@al', selectionStart: 3 } });

      await waitFor(
        () => {
          expect(screen.getByRole('listbox')).toBeInTheDocument();
        },
        { timeout: 3000 }
      );

      // Check that results are rendered (text may be split across elements)
      const listbox = screen.getByRole('listbox');
      expect(listbox.textContent).toContain('Alice Anderson');
      expect(listbox.textContent).toContain('alice@test.com');
    });

    it('shows loading state during search', async () => {
      mockSearchUsers.mockImplementation(
        () => new Promise(resolve => setTimeout(() => resolve(mockUsers), 100))
      );

      render(<MentionInput value="" onChange={mockOnChange} />);
      const textarea = screen.getByRole('combobox');

      fireEvent.change(textarea, { target: { value: '@al', selectionStart: 3 } });

      await waitFor(() => {
        expect(screen.getByText('Searching users...')).toBeInTheDocument();
      });
    });

    it('shows "No users found" when API returns empty array', async () => {
      mockSearchUsers.mockResolvedValue([]);

      render(<MentionInput value="" onChange={mockOnChange} />);
      const textarea = screen.getByRole('combobox');

      fireEvent.change(textarea, { target: { value: '@xyz', selectionStart: 4 } });

      await waitFor(() => {
        expect(screen.getByText('No users found')).toBeInTheDocument();
      });
    });

    it('hides dropdown when no @ mention present', async () => {
      render(<MentionInput value="" onChange={mockOnChange} />);
      const textarea = screen.getByRole('combobox');

      // First show dropdown
      fireEvent.change(textarea, { target: { value: '@al', selectionStart: 3 } });
      await waitFor(() => {
        expect(screen.getByRole('listbox')).toBeInTheDocument();
      });

      // Then remove @ mention
      fireEvent.change(textarea, { target: { value: 'al', selectionStart: 2 } });

      await waitFor(() => {
        expect(screen.queryByRole('listbox')).not.toBeInTheDocument();
      });
    });

    it('handles API errors gracefully', async () => {
      mockSearchUsers.mockRejectedValue(new Error('Network error'));
      mockLoggerError.mockClear();

      render(<MentionInput value="" onChange={mockOnChange} />);
      const textarea = screen.getByRole('combobox');

      fireEvent.change(textarea, { target: { value: '@al', selectionStart: 3 } });

      await waitFor(() => {
        expect(screen.getByText('No users found')).toBeInTheDocument();
        expect(mockLoggerError).toHaveBeenCalledWith(
          'Failed to search users',
          expect.any(Error),
          expect.any(Object)
        );
      });
    });
  });

  describe('Keyboard Navigation', () => {
    beforeEach(async () => {
      render(<MentionInput value="" onChange={mockOnChange} />);
      const textarea = screen.getByRole('combobox');

      // Trigger dropdown
      fireEvent.change(textarea, { target: { value: '@al', selectionStart: 3 } });
      await waitFor(() => {
        expect(screen.getByRole('listbox')).toBeInTheDocument();
      });
    });

    it('navigates down with ArrowDown key', async () => {
      const textarea = screen.getByRole('combobox');
      const options = screen.getAllByRole('option');

      expect(options[0]).toHaveAttribute('aria-selected', 'true');

      fireEvent.keyDown(textarea, { key: 'ArrowDown' });

      await waitFor(() => {
        expect(options[1]).toHaveAttribute('aria-selected', 'true');
      });
    });

    it('navigates up with ArrowUp key', async () => {
      const textarea = screen.getByRole('combobox');

      // Navigate down first
      fireEvent.keyDown(textarea, { key: 'ArrowDown' });

      await waitFor(() => {
        expect(screen.getAllByRole('option')[1]).toHaveAttribute('aria-selected', 'true');
      });

      fireEvent.keyDown(textarea, { key: 'ArrowDown' });

      await waitFor(() => {
        expect(screen.getAllByRole('option')[2]).toHaveAttribute('aria-selected', 'true');
      });

      // Navigate up
      fireEvent.keyDown(textarea, { key: 'ArrowUp' });

      await waitFor(() => {
        expect(screen.getAllByRole('option')[1]).toHaveAttribute('aria-selected', 'true');
      });
    });

    it('does not go below first option with ArrowUp', async () => {
      const textarea = screen.getByRole('combobox');
      const options = screen.getAllByRole('option');

      expect(options[0]).toHaveAttribute('aria-selected', 'true');

      fireEvent.keyDown(textarea, { key: 'ArrowUp' });

      await waitFor(() => {
        expect(options[0]).toHaveAttribute('aria-selected', 'true');
      });
    });

    it('does not go beyond last option with ArrowDown', async () => {
      const textarea = screen.getByRole('combobox');

      // Navigate to last option with proper timing
      fireEvent.keyDown(textarea, { key: 'ArrowDown' });
      await waitFor(() => {
        expect(screen.getAllByRole('option')[1]).toHaveAttribute('aria-selected', 'true');
      });

      fireEvent.keyDown(textarea, { key: 'ArrowDown' });
      await waitFor(() => {
        expect(screen.getAllByRole('option')[2]).toHaveAttribute('aria-selected', 'true');
      });

      // Try to go beyond last option
      fireEvent.keyDown(textarea, { key: 'ArrowDown' });
      fireEvent.keyDown(textarea, { key: 'ArrowDown' });

      await waitFor(() => {
        const options = screen.getAllByRole('option');
        expect(options[options.length - 1]).toHaveAttribute('aria-selected', 'true');
      });
    });
  });
});
