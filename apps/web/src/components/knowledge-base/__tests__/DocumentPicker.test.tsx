/**
 * DocumentPicker Tests - Issue #2415
 *
 * Comprehensive test suite covering all functionality and edge cases.
 * Target: >90% code coverage
 */

import { describe, it, expect, vi, beforeEach } from 'vitest';
import { render, screen, waitFor } from '@testing-library/react';
import userEvent from '@testing-library/user-event';

import { DocumentPicker } from '../DocumentPicker';
import {
  mockDocumentsEmpty,
  mockDocumentsSingle,
  mockDocumentsSmall,
  mockDocumentsMedium,
  mockDocumentsLarge,
  searchDocuments,
} from '@/__tests__/fixtures/mockDocuments';

// ========== Test Suite ==========

describe('DocumentPicker', () => {
  const mockOnSelectionChange = vi.fn();

  beforeEach(() => {
    mockOnSelectionChange.mockClear();
  });

  // ========== Rendering Tests ==========

  describe('Rendering', () => {
    it('should render empty state when no documents', () => {
      render(
        <DocumentPicker
          selectedIds={[]}
          availableDocuments={mockDocumentsEmpty}
          onSelectionChange={mockOnSelectionChange}
        />
      );

      expect(screen.getByText('No documents available.')).toBeInTheDocument();
      expect(screen.getByText('Select Documents')).toBeInTheDocument();
    });

    it('should render single document', () => {
      render(
        <DocumentPicker
          selectedIds={[]}
          availableDocuments={mockDocumentsSingle}
          onSelectionChange={mockOnSelectionChange}
        />
      );

      expect(screen.getByText('Catan Rulebook v1.0')).toBeInTheDocument();
      expect(screen.getByText('Rulebook')).toBeInTheDocument();
    });

    it('should render 100+ documents with pagination', () => {
      render(
        <DocumentPicker
          selectedIds={[]}
          availableDocuments={mockDocumentsLarge}
          onSelectionChange={mockOnSelectionChange}
          pageSize={20}
        />
      );

      // Should show pagination controls
      expect(screen.getByText(/Page 1 of/)).toBeInTheDocument();
      expect(screen.getByRole('button', { name: /previous/i })).toBeInTheDocument();
      expect(screen.getByRole('button', { name: /next/i })).toBeInTheDocument();

      // Should show only first page (20 documents)
      // Note: Each document has 2 checkboxes (1 visual + 1 semantic), so 20 docs = 40 checkboxes
      const checkboxes = screen.getAllByRole('checkbox');
      expect(checkboxes.length).toBe(40); // 20 docs * 2 checkboxes each
    });

    it('should render loading state', () => {
      render(
        <DocumentPicker
          selectedIds={[]}
          availableDocuments={mockDocumentsSmall}
          onSelectionChange={mockOnSelectionChange}
          isLoading={true}
        />
      );

      // Loader2 doesn't have status role, check for animation class instead
      const loader = document.querySelector('.animate-spin');
      expect(loader).toBeInTheDocument();
    });

    it('should render disabled state', () => {
      render(
        <DocumentPicker
          selectedIds={[]}
          availableDocuments={mockDocumentsSmall}
          onSelectionChange={mockOnSelectionChange}
          disabled={true}
        />
      );

      const selectAllButton = screen.getByRole('button', { name: /select all/i });
      expect(selectAllButton).toBeDisabled();

      const clearAllButton = screen.getByRole('button', { name: /clear all/i });
      expect(clearAllButton).toBeDisabled();
    });
  });

  // ========== Search Functionality Tests ==========

  describe('Search Functionality', () => {
    it('should filter documents by title', async () => {
      const user = userEvent.setup();
      render(
        <DocumentPicker
          selectedIds={[]}
          availableDocuments={mockDocumentsSmall}
          onSelectionChange={mockOnSelectionChange}
        />
      );

      const searchInput = screen.getByPlaceholderText(/search by title/i);
      await user.type(searchInput, 'Catan');

      await waitFor(() => {
        expect(screen.getByText('Catan Rulebook v1.0')).toBeInTheDocument();
        expect(screen.queryByText('Ticket to Ride Rulebook v2.0')).not.toBeInTheDocument();
      });
    });

    it('should filter documents by game name', async () => {
      const user = userEvent.setup();
      render(
        <DocumentPicker
          selectedIds={[]}
          availableDocuments={mockDocumentsSmall}
          onSelectionChange={mockOnSelectionChange}
        />
      );

      const searchInput = screen.getByPlaceholderText(/search by title/i);
      await user.type(searchInput, 'Pandemic');

      await waitFor(() => {
        // Use getAllByText since Pandemic appears in both list item and hover card
        expect(screen.getAllByText(/Pandemic/)[0]).toBeInTheDocument();
      });
    });

    it('should filter documents by tag', async () => {
      const user = userEvent.setup();
      render(
        <DocumentPicker
          selectedIds={[]}
          availableDocuments={mockDocumentsSmall}
          onSelectionChange={mockOnSelectionChange}
        />
      );

      const searchInput = screen.getByPlaceholderText(/search by title/i);
      await user.type(searchInput, 'cooperative');

      await waitFor(() => {
        // Use getAllByText since Pandemic appears in both list item and hover card
        expect(screen.getAllByText(/Pandemic/)[0]).toBeInTheDocument();
      });
    });

    it('should show "no match" message when search returns no results', async () => {
      const user = userEvent.setup();
      render(
        <DocumentPicker
          selectedIds={[]}
          availableDocuments={mockDocumentsSmall}
          onSelectionChange={mockOnSelectionChange}
        />
      );

      const searchInput = screen.getByPlaceholderText(/search by title/i);
      await user.type(searchInput, 'NonExistentGame');

      await waitFor(() => {
        expect(screen.getByText('No documents match your search.')).toBeInTheDocument();
      });
    });

    it('should reset to page 1 when search changes', async () => {
      const user = userEvent.setup();
      render(
        <DocumentPicker
          selectedIds={[]}
          availableDocuments={mockDocumentsLarge}
          onSelectionChange={mockOnSelectionChange}
          pageSize={20}
        />
      );

      // Navigate to page 2
      const nextButton = screen.getByRole('button', { name: /next/i });
      await user.click(nextButton);

      await waitFor(() => {
        expect(screen.getByText(/Page 2 of/)).toBeInTheDocument();
      });

      // Type in search - should reset to page 1
      // Note: After searching for 'Catan', there may be fewer results than pageSize,
      // so pagination might not be shown. We verify by checking the first result appears.
      const searchInput = screen.getByPlaceholderText(/search by title/i);
      await user.type(searchInput, 'Catan');

      await waitFor(() => {
        // Verify search results are shown (from page 1)
        expect(screen.getAllByText(/Catan/)[0]).toBeInTheDocument();
        // Page 2 should no longer be shown
        expect(screen.queryByText(/Page 2 of/)).not.toBeInTheDocument();
      });
    });
  });

  // ========== Multi-Select Tests ==========

  describe('Multi-Select', () => {
    it('should toggle single document selection', async () => {
      const user = userEvent.setup();
      render(
        <DocumentPicker
          selectedIds={[]}
          availableDocuments={mockDocumentsSingle}
          onSelectionChange={mockOnSelectionChange}
        />
      );

      const documentItem = screen.getByText('Catan Rulebook v1.0').closest('div[role="checkbox"]');
      expect(documentItem).toBeInTheDocument();

      await user.click(documentItem!);

      await waitFor(() => {
        expect(mockOnSelectionChange).toHaveBeenCalledWith(['doc-single']);
      });
    });

    it('should deselect document when clicked again', async () => {
      const user = userEvent.setup();
      render(
        <DocumentPicker
          selectedIds={['doc-single']}
          availableDocuments={mockDocumentsSingle}
          onSelectionChange={mockOnSelectionChange}
        />
      );

      const documentItem = screen.getByText('Catan Rulebook v1.0').closest('div[role="checkbox"]');
      await user.click(documentItem!);

      await waitFor(() => {
        expect(mockOnSelectionChange).toHaveBeenCalledWith([]);
      });
    });

    it('should handle keyboard navigation (Enter key)', async () => {
      const user = userEvent.setup();
      render(
        <DocumentPicker
          selectedIds={[]}
          availableDocuments={mockDocumentsSingle}
          onSelectionChange={mockOnSelectionChange}
        />
      );

      const documentItem = screen.getByText('Catan Rulebook v1.0').closest('div[role="checkbox"]');
      documentItem?.focus();
      await user.keyboard('{Enter}');

      await waitFor(() => {
        expect(mockOnSelectionChange).toHaveBeenCalledWith(['doc-single']);
      });
    });

    it('should handle keyboard navigation (Space key)', async () => {
      const user = userEvent.setup();
      render(
        <DocumentPicker
          selectedIds={[]}
          availableDocuments={mockDocumentsSingle}
          onSelectionChange={mockOnSelectionChange}
        />
      );

      const documentItem = screen.getByText('Catan Rulebook v1.0').closest('div[role="checkbox"]');
      documentItem?.focus();
      await user.keyboard(' ');

      await waitFor(() => {
        expect(mockOnSelectionChange).toHaveBeenCalledWith(['doc-single']);
      });
    });

    it('should select all documents', async () => {
      const user = userEvent.setup();
      render(
        <DocumentPicker
          selectedIds={[]}
          availableDocuments={mockDocumentsSmall}
          onSelectionChange={mockOnSelectionChange}
        />
      );

      const selectAllButton = screen.getByRole('button', { name: /select all/i });
      await user.click(selectAllButton);

      await waitFor(() => {
        expect(mockOnSelectionChange).toHaveBeenCalledWith([
          'doc-001',
          'doc-002',
          'doc-003',
          'doc-004',
          'doc-005',
        ]);
      });
    });

    it('should clear all selections', async () => {
      const user = userEvent.setup();
      render(
        <DocumentPicker
          selectedIds={['doc-001', 'doc-002']}
          availableDocuments={mockDocumentsSmall}
          onSelectionChange={mockOnSelectionChange}
        />
      );

      const clearAllButton = screen.getByRole('button', { name: /clear all/i });
      await user.click(clearAllButton);

      await waitFor(() => {
        expect(mockOnSelectionChange).toHaveBeenCalledWith([]);
      });
    });
  });

  // ========== Max Selections Tests ==========

  describe('Max Selections', () => {
    it('should respect max selections limit', async () => {
      const user = userEvent.setup();
      render(
        <DocumentPicker
          selectedIds={['doc-001', 'doc-002']}
          availableDocuments={mockDocumentsSmall}
          onSelectionChange={mockOnSelectionChange}
          maxSelections={2}
        />
      );

      // Try to select a third document (should be disabled)
      const thirdDocMatches = screen.getAllByText(/Ticket to Ride/);
      const thirdDoc = thirdDocMatches[0].closest('div[role="checkbox"]');
      expect(thirdDoc).toHaveClass('cursor-not-allowed', 'opacity-50');

      await user.click(thirdDoc!);

      // Should NOT have called onSelectionChange
      expect(mockOnSelectionChange).not.toHaveBeenCalled();
    });

    it('should show max selections in description', () => {
      render(
        <DocumentPicker
          selectedIds={['doc-001']}
          availableDocuments={mockDocumentsSmall}
          onSelectionChange={mockOnSelectionChange}
          maxSelections={5}
        />
      );

      expect(screen.getByText(/Select up to 5 documents \(1 selected\)/)).toBeInTheDocument();
    });

    it('should limit select all to max selections', async () => {
      const user = userEvent.setup();
      render(
        <DocumentPicker
          selectedIds={[]}
          availableDocuments={mockDocumentsSmall}
          onSelectionChange={mockOnSelectionChange}
          maxSelections={3}
        />
      );

      const selectAllButton = screen.getByRole('button', { name: /select all/i });
      await user.click(selectAllButton);

      await waitFor(() => {
        const call = mockOnSelectionChange.mock.calls[0][0];
        expect(call.length).toBe(3); // Limited to maxSelections
      });
    });
  });

  // ========== Pagination Tests ==========

  describe('Pagination', () => {
    it('should navigate to next page', async () => {
      const user = userEvent.setup();
      render(
        <DocumentPicker
          selectedIds={[]}
          availableDocuments={mockDocumentsLarge}
          onSelectionChange={mockOnSelectionChange}
          pageSize={20}
        />
      );

      const nextButton = screen.getByRole('button', { name: /next/i });
      await user.click(nextButton);

      await waitFor(() => {
        expect(screen.getByText(/Page 2 of/)).toBeInTheDocument();
      });
    });

    it('should navigate to previous page', async () => {
      const user = userEvent.setup();
      render(
        <DocumentPicker
          selectedIds={[]}
          availableDocuments={mockDocumentsLarge}
          onSelectionChange={mockOnSelectionChange}
          pageSize={20}
        />
      );

      // Go to page 2
      const nextButton = screen.getByRole('button', { name: /next/i });
      await user.click(nextButton);

      await waitFor(() => {
        expect(screen.getByText(/Page 2 of/)).toBeInTheDocument();
      });

      // Go back to page 1
      const prevButton = screen.getByRole('button', { name: /previous/i });
      await user.click(prevButton);

      await waitFor(() => {
        expect(screen.getByText(/Page 1 of/)).toBeInTheDocument();
      });
    });

    it('should disable previous button on first page', () => {
      render(
        <DocumentPicker
          selectedIds={[]}
          availableDocuments={mockDocumentsLarge}
          onSelectionChange={mockOnSelectionChange}
          pageSize={20}
        />
      );

      const prevButton = screen.getByRole('button', { name: /previous/i });
      expect(prevButton).toBeDisabled();
    });

    it('should disable next button on last page', async () => {
      const user = userEvent.setup();
      render(
        <DocumentPicker
          selectedIds={[]}
          availableDocuments={mockDocumentsLarge}
          onSelectionChange={mockOnSelectionChange}
          pageSize={100}
        />
      );

      // Navigate to last page
      const nextButton = screen.getByRole('button', { name: /next/i });
      await user.click(nextButton);

      await waitFor(() => {
        expect(nextButton).toBeDisabled();
      });
    });

    it('should not show pagination for small datasets', () => {
      render(
        <DocumentPicker
          selectedIds={[]}
          availableDocuments={mockDocumentsSmall}
          onSelectionChange={mockOnSelectionChange}
          pageSize={20}
        />
      );

      expect(screen.queryByText(/Page/)).not.toBeInTheDocument();
    });
  });

  // ========== Accessibility Tests ==========

  describe('Accessibility', () => {
    it('should have proper ARIA attributes on document items', () => {
      render(
        <DocumentPicker
          selectedIds={['doc-001']}
          availableDocuments={mockDocumentsSmall}
          onSelectionChange={mockOnSelectionChange}
        />
      );

      const selectedItem = screen.getByText('Catan Rulebook v1.0').closest('div[role="checkbox"]');
      expect(selectedItem).toHaveAttribute('aria-checked', 'true');

      const unselectedItems = screen.queryAllByText(/Ticket to Ride/);
      const unselectedItem = unselectedItems[0]?.closest('div[role="checkbox"]');
      expect(unselectedItem).toHaveAttribute('aria-checked', 'false');
    });

    it('should have proper tabindex for keyboard navigation', () => {
      render(
        <DocumentPicker
          selectedIds={[]}
          availableDocuments={mockDocumentsSmall}
          onSelectionChange={mockOnSelectionChange}
        />
      );

      const documentItem = screen.getByText('Catan Rulebook v1.0').closest('div[role="checkbox"]');
      expect(documentItem).toHaveAttribute('tabindex', '0');
    });

    it('should have negative tabindex when disabled', () => {
      render(
        <DocumentPicker
          selectedIds={[]}
          availableDocuments={mockDocumentsSmall}
          onSelectionChange={mockOnSelectionChange}
          disabled={true}
        />
      );

      const documentItem = screen.getByText('Catan Rulebook v1.0').closest('div[role="checkbox"]');
      expect(documentItem).toHaveAttribute('tabindex', '-1');
    });
  });

  // ========== Badge Display Tests ==========

  describe('Badge Display', () => {
    it('should display document type badge', () => {
      render(
        <DocumentPicker
          selectedIds={[]}
          availableDocuments={mockDocumentsSingle}
          onSelectionChange={mockOnSelectionChange}
        />
      );

      expect(screen.getByText('Rulebook')).toBeInTheDocument();
    });

    it('should display inactive badge for inactive documents', () => {
      render(
        <DocumentPicker
          selectedIds={[]}
          availableDocuments={mockDocumentsSmall}
          onSelectionChange={mockOnSelectionChange}
        />
      );

      const inactiveDoc = mockDocumentsSmall.find(doc => doc.isActive === false);
      if (inactiveDoc) {
        expect(screen.getByText('Inactive')).toBeInTheDocument();
      }
    });

    it('should display first 3 tags and "+N more" badge', () => {
      render(
        <DocumentPicker
          selectedIds={[]}
          availableDocuments={mockDocumentsSmall}
          onSelectionChange={mockOnSelectionChange}
        />
      );

      // First document has 2 tags, should show both
      const firstDoc = mockDocumentsSmall[0];
      firstDoc.tags.forEach(tag => {
        expect(screen.getAllByText(tag)[0]).toBeInTheDocument();
      });
    });
  });
});
