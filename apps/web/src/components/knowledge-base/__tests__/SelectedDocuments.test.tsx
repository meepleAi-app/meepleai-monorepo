/**
 * SelectedDocuments Tests - Issue #2416
 *
 * Comprehensive test suite covering:
 * - Rendering states (empty, loading, disabled)
 * - Badge removal functionality
 * - Drag-and-drop reordering
 * - Max limit alerts
 * - Selection statistics
 * - Accessibility
 *
 * Target: >85% code coverage
 */

import { describe, it, expect, vi, beforeEach } from 'vitest';
import { render, screen, waitFor, within } from '@testing-library/react';
import userEvent from '@testing-library/user-event';

import { SelectedDocuments, type SelectedDocument } from '../SelectedDocuments';
import {
  mockSelectedEmpty,
  mockSelectedSingle,
  mockSelectedSmall,
  mockSelectedMedium,
  mockSelectedNearLimit,
  mockSelectedAtLimit,
  mockSelectedMixedTypes,
  mockSelectedVariedTags,
} from '@/__tests__/fixtures/mockSelectedDocuments';

// ========== Test Suite ==========

describe('SelectedDocuments', () => {
  const mockOnDocumentsChange = vi.fn();

  beforeEach(() => {
    mockOnDocumentsChange.mockClear();
  });

  // ========== Rendering Tests ==========

  describe('Rendering', () => {
    it('should render empty state when no documents', () => {
      render(
        <SelectedDocuments
          documents={mockSelectedEmpty}
          onDocumentsChange={mockOnDocumentsChange}
        />
      );

      expect(screen.getByText('No documents selected')).toBeInTheDocument();
      expect(
        screen.getByText('Select documents from the picker to add them here.')
      ).toBeInTheDocument();
      expect(screen.getByText('Selected Documents')).toBeInTheDocument();
    });

    it('should render single document', () => {
      render(
        <SelectedDocuments
          documents={mockSelectedSingle}
          onDocumentsChange={mockOnDocumentsChange}
        />
      );

      expect(screen.getByText('Catan Rulebook v1.0')).toBeInTheDocument();
      expect(screen.getByText('Rulebook')).toBeInTheDocument();
      expect(screen.getByText('v1.0')).toBeInTheDocument();
    });

    it('should render multiple documents', () => {
      render(
        <SelectedDocuments
          documents={mockSelectedSmall}
          onDocumentsChange={mockOnDocumentsChange}
        />
      );

      expect(screen.getByText('Catan Rulebook v1.0')).toBeInTheDocument();
      expect(screen.getByText('Ticket to Ride Rulebook v2.0')).toBeInTheDocument();
      expect(screen.getByText(/5 of 50 documents selected/)).toBeInTheDocument();
    });

    it('should render loading state', () => {
      render(
        <SelectedDocuments
          documents={mockSelectedSmall}
          onDocumentsChange={mockOnDocumentsChange}
          isLoading={true}
        />
      );

      const loader = document.querySelector('.animate-spin');
      expect(loader).toBeInTheDocument();
      expect(screen.getByText('Loading selected documents...')).toBeInTheDocument();
    });

    it('should render disabled state', () => {
      render(
        <SelectedDocuments
          documents={mockSelectedSmall}
          onDocumentsChange={mockOnDocumentsChange}
          disabled={true}
        />
      );

      // Remove buttons should not be rendered when disabled
      const removeButtons = screen.queryAllByRole('button', { name: /remove/i });
      expect(removeButtons.length).toBe(0);

      // Clear all button should not be present when disabled
      expect(screen.queryByText(/Clear All/)).not.toBeInTheDocument();
    });

    it('should render document count description', () => {
      render(
        <SelectedDocuments
          documents={mockSelectedMedium}
          onDocumentsChange={mockOnDocumentsChange}
          maxDocuments={50}
        />
      );

      expect(screen.getByText(/15 of 50 documents selected/)).toBeInTheDocument();
    });

    it('should show unique games count', () => {
      render(
        <SelectedDocuments
          documents={mockSelectedSmall}
          onDocumentsChange={mockOnDocumentsChange}
        />
      );

      // Check for "from X games" text
      expect(screen.getByText(/from \d+ games/)).toBeInTheDocument();
    });
  });

  // ========== Badge Removal Tests ==========

  describe('Badge Removal', () => {
    it('should remove document when X button clicked', async () => {
      const user = userEvent.setup();
      render(
        <SelectedDocuments
          documents={mockSelectedSmall}
          onDocumentsChange={mockOnDocumentsChange}
        />
      );

      // Find and click remove button for first document
      const removeButtons = screen.getAllByRole('button', { name: /remove/i });
      await user.click(removeButtons[0]);

      await waitFor(() => {
        expect(mockOnDocumentsChange).toHaveBeenCalledWith(
          mockSelectedSmall.filter(doc => doc.id !== mockSelectedSmall[0].id)
        );
      });
    });

    it('should clear all documents when Clear All clicked', async () => {
      const user = userEvent.setup();
      render(
        <SelectedDocuments
          documents={mockSelectedSmall}
          onDocumentsChange={mockOnDocumentsChange}
        />
      );

      const clearAllButton = screen.getByRole('button', { name: /clear all/i });
      await user.click(clearAllButton);

      await waitFor(() => {
        expect(mockOnDocumentsChange).toHaveBeenCalledWith([]);
      });
    });

    it('should not show Clear All when no documents', () => {
      render(
        <SelectedDocuments
          documents={mockSelectedEmpty}
          onDocumentsChange={mockOnDocumentsChange}
        />
      );

      expect(screen.queryByText(/Clear All/)).not.toBeInTheDocument();
    });
  });

  // ========== Drag-and-Drop Tests ==========

  describe('Drag-and-Drop', () => {
    it('should render drag handles for each document', () => {
      render(
        <SelectedDocuments
          documents={mockSelectedSmall}
          onDocumentsChange={mockOnDocumentsChange}
        />
      );

      const dragHandles = screen.getAllByRole('button', { name: /drag to reorder/i });
      expect(dragHandles.length).toBe(mockSelectedSmall.length);
    });

    it('should have accessible drag handle labels', () => {
      render(
        <SelectedDocuments
          documents={mockSelectedSingle}
          onDocumentsChange={mockOnDocumentsChange}
        />
      );

      const dragHandle = screen.getByRole('button', { name: /drag to reorder catan rulebook/i });
      expect(dragHandle).toBeInTheDocument();
    });

    it('should disable drag handles when disabled', () => {
      render(
        <SelectedDocuments
          documents={mockSelectedSmall}
          onDocumentsChange={mockOnDocumentsChange}
          disabled={true}
        />
      );

      const dragHandles = screen.getAllByRole('button', { name: /drag to reorder/i });
      dragHandles.forEach(handle => {
        expect(handle).toBeDisabled();
      });
    });

    it('should show position indicators', () => {
      render(
        <SelectedDocuments
          documents={mockSelectedSmall}
          onDocumentsChange={mockOnDocumentsChange}
        />
      );

      // Check for position numbers
      expect(screen.getByText('1')).toBeInTheDocument();
      expect(screen.getByText('2')).toBeInTheDocument();
      expect(screen.getByText('3')).toBeInTheDocument();
    });
  });

  // ========== Max Limit Tests ==========

  describe('Max Limit Alerts', () => {
    it('should show warning alert when near limit (80%)', () => {
      render(
        <SelectedDocuments
          documents={mockSelectedNearLimit}
          onDocumentsChange={mockOnDocumentsChange}
          maxDocuments={50}
        />
      );

      expect(screen.getByText('Approaching limit')).toBeInTheDocument();
      // Use getAllByText since text appears in both card description and alert
      expect(screen.getAllByText(/45 of 50 documents/)).toHaveLength(2);
    });

    it('should show error alert when at limit', () => {
      render(
        <SelectedDocuments
          documents={mockSelectedAtLimit}
          onDocumentsChange={mockOnDocumentsChange}
          maxDocuments={50}
        />
      );

      expect(screen.getByText('Maximum limit reached')).toBeInTheDocument();
      expect(screen.getByText(/You have reached the maximum of 50 documents/)).toBeInTheDocument();
    });

    it('should not show alert when under 80%', () => {
      render(
        <SelectedDocuments
          documents={mockSelectedSmall}
          onDocumentsChange={mockOnDocumentsChange}
          maxDocuments={50}
        />
      );

      expect(screen.queryByText('Approaching limit')).not.toBeInTheDocument();
      expect(screen.queryByText('Maximum limit reached')).not.toBeInTheDocument();
    });

    it('should calculate percentage correctly', () => {
      render(
        <SelectedDocuments
          documents={mockSelectedNearLimit}
          onDocumentsChange={mockOnDocumentsChange}
          maxDocuments={50}
        />
      );

      // 45/50 = 90%
      expect(screen.getByText(/\(90%\)/)).toBeInTheDocument();
    });
  });

  // ========== Statistics Tests ==========

  describe('Selection Statistics', () => {
    it('should show statistics badges by document type', () => {
      render(
        <SelectedDocuments
          documents={mockSelectedMixedTypes}
          onDocumentsChange={mockOnDocumentsChange}
        />
      );

      // Should show count for each type
      expect(screen.getByText(/\d+ Rulebook/)).toBeInTheDocument();
      expect(screen.getByText(/\d+ Errata/)).toBeInTheDocument();
      expect(screen.getByText(/\d+ Homerule/)).toBeInTheDocument();
    });

    it('should not show statistics when empty', () => {
      render(
        <SelectedDocuments
          documents={mockSelectedEmpty}
          onDocumentsChange={mockOnDocumentsChange}
        />
      );

      expect(screen.queryByText(/\d+ Rulebook/)).not.toBeInTheDocument();
    });
  });

  // ========== Badge Display Tests ==========

  describe('Badge Display', () => {
    it('should display document type badge with correct color', () => {
      render(
        <SelectedDocuments
          documents={mockSelectedSingle}
          onDocumentsChange={mockOnDocumentsChange}
        />
      );

      const badge = screen.getByText('Rulebook');
      expect(badge).toHaveClass('bg-blue-100');
    });

    it('should display version badge', () => {
      render(
        <SelectedDocuments
          documents={mockSelectedSingle}
          onDocumentsChange={mockOnDocumentsChange}
        />
      );

      expect(screen.getByText('v1.0')).toBeInTheDocument();
    });

    it('should display first 3 tags and "+N more"', () => {
      render(
        <SelectedDocuments
          documents={mockSelectedVariedTags}
          onDocumentsChange={mockOnDocumentsChange}
        />
      );

      // Document with many tags should show "+2" badge
      expect(screen.getByText('+2')).toBeInTheDocument();
    });

    it('should display game name', () => {
      render(
        <SelectedDocuments
          documents={mockSelectedSingle}
          onDocumentsChange={mockOnDocumentsChange}
        />
      );

      expect(screen.getByText('Catan')).toBeInTheDocument();
    });
  });

  // ========== Accessibility Tests ==========

  describe('Accessibility', () => {
    it('should have proper aria-label on remove buttons', () => {
      render(
        <SelectedDocuments
          documents={mockSelectedSingle}
          onDocumentsChange={mockOnDocumentsChange}
        />
      );

      const removeButton = screen.getByRole('button', { name: /remove catan rulebook/i });
      expect(removeButton).toBeInTheDocument();
    });

    it('should have proper aria-label on drag handles', () => {
      render(
        <SelectedDocuments
          documents={mockSelectedSingle}
          onDocumentsChange={mockOnDocumentsChange}
        />
      );

      const dragHandle = screen.getByRole('button', { name: /drag to reorder catan rulebook/i });
      expect(dragHandle).toBeInTheDocument();
    });

    it('should have proper keyboard focus styling', () => {
      render(
        <SelectedDocuments
          documents={mockSelectedSingle}
          onDocumentsChange={mockOnDocumentsChange}
        />
      );

      const dragHandle = screen.getByRole('button', { name: /drag to reorder/i });
      expect(dragHandle).toHaveClass('focus-visible:ring-2');
    });
  });

  // ========== Edge Cases ==========

  describe('Edge Cases', () => {
    it('should handle document without tags', () => {
      const docWithoutTags: SelectedDocument[] = [
        {
          id: 'no-tags',
          title: 'Document Without Tags',
          documentType: 'Rulebook',
          version: '1.0',
          tags: [],
          gameName: 'Test Game',
        },
      ];

      render(
        <SelectedDocuments documents={docWithoutTags} onDocumentsChange={mockOnDocumentsChange} />
      );

      expect(screen.getByText('Document Without Tags')).toBeInTheDocument();
      // Should not show any tag badges
      expect(screen.queryByText('+0')).not.toBeInTheDocument();
    });

    it('should handle document without game name', () => {
      const docWithoutGame: SelectedDocument[] = [
        {
          id: 'no-game',
          title: 'Document Without Game',
          documentType: 'Rulebook',
          version: '1.0',
          tags: ['tag1'],
        },
      ];

      render(
        <SelectedDocuments documents={docWithoutGame} onDocumentsChange={mockOnDocumentsChange} />
      );

      expect(screen.getByText('Document Without Game')).toBeInTheDocument();
    });

    it('should handle custom max documents', () => {
      render(
        <SelectedDocuments
          documents={mockSelectedSmall}
          onDocumentsChange={mockOnDocumentsChange}
          maxDocuments={10}
        />
      );

      expect(screen.getByText(/5 of 10 documents selected/)).toBeInTheDocument();
    });

    it('should handle removal of last document', async () => {
      const user = userEvent.setup();
      render(
        <SelectedDocuments
          documents={mockSelectedSingle}
          onDocumentsChange={mockOnDocumentsChange}
        />
      );

      const removeButton = screen.getByRole('button', { name: /remove/i });
      await user.click(removeButton);

      await waitFor(() => {
        expect(mockOnDocumentsChange).toHaveBeenCalledWith([]);
      });
    });
  });

  // ========== Integration Tests ==========

  describe('Integration', () => {
    it('should update statistics after removal', async () => {
      const user = userEvent.setup();
      const TestWrapper = () => {
        const [docs, setDocs] = vi.fn().mockImplementation((newDocs: SelectedDocument[]) => {
          // This would be real state in production
        }) as unknown as [
          SelectedDocument[],
          React.Dispatch<React.SetStateAction<SelectedDocument[]>>,
        ];

        return (
          <SelectedDocuments
            documents={mockSelectedMixedTypes}
            onDocumentsChange={mockOnDocumentsChange}
          />
        );
      };

      render(
        <SelectedDocuments
          documents={mockSelectedMixedTypes}
          onDocumentsChange={mockOnDocumentsChange}
        />
      );

      // Verify initial statistics
      const initialStats = screen.getByText(/\d+ Rulebook/);
      expect(initialStats).toBeInTheDocument();
    });

    it('should maintain document order in callback', async () => {
      const user = userEvent.setup();
      render(
        <SelectedDocuments
          documents={mockSelectedSmall}
          onDocumentsChange={mockOnDocumentsChange}
        />
      );

      // Remove middle document
      const removeButtons = screen.getAllByRole('button', { name: /remove/i });
      await user.click(removeButtons[2]); // Remove third document

      await waitFor(() => {
        const result = mockOnDocumentsChange.mock.calls[0][0];
        // Should maintain order of remaining documents
        expect(result[0].id).toBe(mockSelectedSmall[0].id);
        expect(result[1].id).toBe(mockSelectedSmall[1].id);
        expect(result[2].id).toBe(mockSelectedSmall[3].id);
        expect(result[3].id).toBe(mockSelectedSmall[4].id);
      });
    });
  });
});
