/**
 * Tests for GlobalSearch component
 * Issue #3450: Global search with Cmd+K
 */

import React from 'react';

import { render, screen, fireEvent, waitFor } from '@testing-library/react';
import userEvent from '@testing-library/user-event';
import { describe, it, expect, vi, beforeEach, afterEach } from 'vitest';

// Mock cmdk module
vi.mock('cmdk', () => ({
  Command: ({ children, ...props }: React.PropsWithChildren<Record<string, unknown>>) => (
    <div data-testid="command" {...props}>{children}</div>
  ),
}));

// Mock the Command components from shadcn
vi.mock('@/components/ui/navigation/command', () => ({
  CommandDialog: ({ children, open }: { children: React.ReactNode; open: boolean }) =>
    open ? <div data-testid="command-dialog">{children}</div> : null,
  CommandInput: ({ placeholder, ...props }: { placeholder?: string }) => (
    <input
      data-testid="command-input"
      placeholder={placeholder}
      {...props}
    />
  ),
  CommandList: ({ children }: React.PropsWithChildren) => (
    <div data-testid="command-list">{children}</div>
  ),
  CommandEmpty: ({ children }: React.PropsWithChildren) => (
    <div data-testid="command-empty">{children}</div>
  ),
  CommandGroup: ({ heading, children }: { heading?: string; children: React.ReactNode }) => (
    <div data-testid="command-group">
      {heading && <span data-testid="command-group-heading">{heading}</span>}
      {children}
    </div>
  ),
  CommandItem: ({ children, onSelect, value }: { children: React.ReactNode; onSelect?: () => void; value?: string }) => (
    <button
      data-testid={`command-item-${value}`}
      onClick={onSelect}
    >
      {children}
    </button>
  ),
  CommandShortcut: ({ children }: React.PropsWithChildren) => (
    <span data-testid="command-shortcut">{children}</span>
  ),
}));

// Mock localStorage
const localStorageMock = (() => {
  let store: Record<string, string> = {};
  return {
    getItem: vi.fn((key: string) => store[key] || null),
    setItem: vi.fn((key: string, value: string) => {
      store[key] = value;
    }),
    removeItem: vi.fn((key: string) => {
      delete store[key];
    }),
    clear: vi.fn(() => {
      store = {};
    }),
  };
})();

// Import after mocks
import { GlobalSearch } from '../GlobalSearch';

describe('GlobalSearch', () => {
  beforeEach(() => {
    Object.defineProperty(window, 'localStorage', {
      value: localStorageMock,
      writable: true,
    });
    localStorageMock.clear();
    vi.clearAllMocks();
  });

  afterEach(() => {
    vi.clearAllMocks();
  });

  // =========================================================================
  // Rendering Tests
  // =========================================================================

  describe('Rendering', () => {
    it('should render search trigger button', () => {
      render(<GlobalSearch />);

      expect(screen.getByRole('button', { name: /open search/i })).toBeInTheDocument();
    });

    it('should show keyboard shortcut hint', () => {
      render(<GlobalSearch />);

      // The kbd element with ⌘K
      expect(screen.getByText('⌘')).toBeInTheDocument();
      expect(screen.getByText('K')).toBeInTheDocument();
    });

    it('should not show dialog initially', () => {
      render(<GlobalSearch />);

      expect(screen.queryByTestId('command-dialog')).not.toBeInTheDocument();
    });
  });

  // =========================================================================
  // Dialog Open/Close Tests
  // =========================================================================

  describe('Dialog Control', () => {
    it('should open dialog when trigger button is clicked', async () => {
      const user = userEvent.setup();
      render(<GlobalSearch />);

      const trigger = screen.getByRole('button', { name: /open search/i });
      await user.click(trigger);

      expect(screen.getByTestId('command-dialog')).toBeInTheDocument();
    });

    it('should open dialog with Cmd+K', async () => {
      render(<GlobalSearch />);

      fireEvent.keyDown(document, { key: 'k', metaKey: true });

      await waitFor(() => {
        expect(screen.getByTestId('command-dialog')).toBeInTheDocument();
      });
    });

    it('should open dialog with Ctrl+K', async () => {
      render(<GlobalSearch />);

      fireEvent.keyDown(document, { key: 'k', ctrlKey: true });

      await waitFor(() => {
        expect(screen.getByTestId('command-dialog')).toBeInTheDocument();
      });
    });

    it('should toggle dialog with repeated Cmd+K', async () => {
      render(<GlobalSearch />);

      // Open
      fireEvent.keyDown(document, { key: 'k', metaKey: true });
      await waitFor(() => {
        expect(screen.getByTestId('command-dialog')).toBeInTheDocument();
      });

      // Close
      fireEvent.keyDown(document, { key: 'k', metaKey: true });
      await waitFor(() => {
        expect(screen.queryByTestId('command-dialog')).not.toBeInTheDocument();
      });
    });
  });

  // =========================================================================
  // Search Input Tests
  // =========================================================================

  describe('Search Input', () => {
    it('should show search input when dialog is open', async () => {
      const user = userEvent.setup();
      render(<GlobalSearch />);

      await user.click(screen.getByRole('button', { name: /open search/i }));

      expect(screen.getByTestId('command-input')).toBeInTheDocument();
    });

    it('should have correct placeholder', async () => {
      const user = userEvent.setup();
      render(<GlobalSearch />);

      await user.click(screen.getByRole('button', { name: /open search/i }));

      expect(screen.getByPlaceholderText(/search sections/i)).toBeInTheDocument();
    });
  });

  // =========================================================================
  // Results Display Tests
  // =========================================================================

  describe('Results Display', () => {
    it('should show command groups when dialog is open', async () => {
      const user = userEvent.setup();
      render(<GlobalSearch viewMode="technical" />);

      await user.click(screen.getByRole('button', { name: /open search/i }));

      // Should show group headings
      const headings = screen.getAllByTestId('command-group-heading');
      expect(headings.length).toBeGreaterThan(0);
    });

    it('should show section items', async () => {
      const user = userEvent.setup();
      render(<GlobalSearch viewMode="technical" />);

      await user.click(screen.getByRole('button', { name: /open search/i }));

      // Should show sections like overview, query-sim, etc.
      expect(screen.getByTestId('command-item-overview')).toBeInTheDocument();
      expect(screen.getByTestId('command-item-query-sim')).toBeInTheDocument();
    });
  });

  // =========================================================================
  // Section Selection Tests
  // =========================================================================

  describe('Section Selection', () => {
    it('should call onSelectSection when item is selected', async () => {
      const onSelectSection = vi.fn();
      const user = userEvent.setup();
      render(<GlobalSearch onSelectSection={onSelectSection} />);

      await user.click(screen.getByRole('button', { name: /open search/i }));
      await user.click(screen.getByTestId('command-item-overview'));

      expect(onSelectSection).toHaveBeenCalledWith('overview');
    });

    it('should call onOpenSection when item is selected', async () => {
      const onOpenSection = vi.fn();
      const user = userEvent.setup();
      render(<GlobalSearch onOpenSection={onOpenSection} />);

      await user.click(screen.getByRole('button', { name: /open search/i }));
      await user.click(screen.getByTestId('command-item-overview'));

      expect(onOpenSection).toHaveBeenCalledWith('overview');
    });

    it('should close dialog after selection', async () => {
      const user = userEvent.setup();
      render(<GlobalSearch />);

      await user.click(screen.getByRole('button', { name: /open search/i }));
      expect(screen.getByTestId('command-dialog')).toBeInTheDocument();

      await user.click(screen.getByTestId('command-item-overview'));

      await waitFor(() => {
        expect(screen.queryByTestId('command-dialog')).not.toBeInTheDocument();
      });
    });
  });

  // =========================================================================
  // View Mode Filtering Tests
  // =========================================================================

  describe('View Mode Filtering', () => {
    it('should show technical sections in technical view', async () => {
      const user = userEvent.setup();
      render(<GlobalSearch viewMode="technical" />);

      await user.click(screen.getByRole('button', { name: /open search/i }));

      // Architecture is technical only
      expect(screen.getByTestId('command-item-architecture')).toBeInTheDocument();
    });

    it('should not show technical-only sections in business view', async () => {
      const user = userEvent.setup();
      render(<GlobalSearch viewMode="business" />);

      await user.click(screen.getByRole('button', { name: /open search/i }));

      // Architecture is technical only - should not be visible
      expect(screen.queryByTestId('command-item-architecture')).not.toBeInTheDocument();
    });

    it('should show executive summary in business view', async () => {
      const user = userEvent.setup();
      render(<GlobalSearch viewMode="business" />);

      await user.click(screen.getByRole('button', { name: /open search/i }));

      // Executive summary is business only
      expect(screen.getByTestId('command-item-executive-summary')).toBeInTheDocument();
    });

    it('should not show business-only sections in technical view', async () => {
      const user = userEvent.setup();
      render(<GlobalSearch viewMode="technical" />);

      await user.click(screen.getByRole('button', { name: /open search/i }));

      // Executive summary is business only - should not be visible
      expect(screen.queryByTestId('command-item-executive-summary')).not.toBeInTheDocument();
    });
  });

  // =========================================================================
  // Recent Searches Tests
  // =========================================================================

  describe('Recent Searches', () => {
    it('should save recent search to localStorage', async () => {
      const user = userEvent.setup();
      render(<GlobalSearch />);

      await user.click(screen.getByRole('button', { name: /open search/i }));
      await user.click(screen.getByTestId('command-item-overview'));

      expect(localStorageMock.setItem).toHaveBeenCalledWith(
        'rag-dashboard-recent-searches',
        expect.stringContaining('overview')
      );
    });

    it('should load recent searches from localStorage', async () => {
      localStorageMock.getItem.mockReturnValueOnce(JSON.stringify(['query-sim', 'cost']));

      const user = userEvent.setup();
      render(<GlobalSearch />);

      await user.click(screen.getByRole('button', { name: /open search/i }));

      // Should show recent group
      const headings = screen.getAllByTestId('command-group-heading');
      const hasRecent = headings.some((h) => h.textContent?.includes('Recent'));
      expect(hasRecent).toBe(true);
    });
  });

  // =========================================================================
  // Keyboard Navigation Hint Tests
  // =========================================================================

  describe('Keyboard Hints', () => {
    it('should show navigation hints in footer', async () => {
      const user = userEvent.setup();
      render(<GlobalSearch />);

      await user.click(screen.getByRole('button', { name: /open search/i }));

      expect(screen.getByText('Navigate')).toBeInTheDocument();
      expect(screen.getByText('Select')).toBeInTheDocument();
      expect(screen.getByText('Close')).toBeInTheDocument();
    });

    it('should show current view mode in footer', async () => {
      const user = userEvent.setup();
      render(<GlobalSearch viewMode="technical" />);

      await user.click(screen.getByRole('button', { name: /open search/i }));

      expect(screen.getByText('Technical View')).toBeInTheDocument();
    });
  });
});
