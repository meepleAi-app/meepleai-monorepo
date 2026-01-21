/**
 * CollectionSourceFilter Tests - Issue #2764 Sprint 4
 *
 * Tests for CollectionSourceFilter component:
 * - Multi-select dropdown for document collections
 * - "All documents" default selection
 * - Individual collection toggles
 * - Document type badges and counts
 *
 * Pattern: Vitest + React Testing Library
 * Coverage target: 170 lines (85%+)
 */

import { render, screen, within } from '@testing-library/react';
import userEvent from '@testing-library/user-event';
import { describe, it, expect, vi } from 'vitest';
import React from 'react';

import {
  CollectionSourceFilter,
  type DocumentCollection,
} from '../CollectionSourceFilter';

// Test fixtures
const createMockCollections = (): DocumentCollection[] => [
  {
    id: 'coll-1',
    name: 'Base Rules Collection',
    documentType: 'base',
    documentCount: 3,
  },
  {
    id: 'coll-2',
    name: 'Expansion Rules',
    documentType: 'expansion',
    documentCount: 2,
  },
  {
    id: 'coll-3',
    name: 'Errata Documents',
    documentType: 'errata',
    documentCount: 1,
  },
];

describe('CollectionSourceFilter - Issue #2764 Sprint 4', () => {
  // ============================================================================
  // TEST 1: Default "All documents" label
  // ============================================================================
  it('should display "All documents" when no specific selection', () => {
    // Arrange
    const collections = createMockCollections();

    // Act
    render(
      <CollectionSourceFilter
        collections={collections}
        selectedDocIds={[]}
        onChange={vi.fn()}
      />
    );

    // Assert
    const button = screen.getByRole('button', { name: /filter by document source/i });
    expect(button).toBeInTheDocument();
    expect(screen.getByText('All documents')).toBeInTheDocument();
  });

  // ============================================================================
  // TEST 2: Display single document name when one selected
  // ============================================================================
  it('should display collection name when single collection selected', () => {
    // Arrange
    const collections = createMockCollections();

    // Act
    render(
      <CollectionSourceFilter
        collections={collections}
        selectedDocIds={['coll-1']}
        onChange={vi.fn()}
      />
    );

    // Assert
    expect(screen.getByText('Base Rules Collection')).toBeInTheDocument();
  });

  // ============================================================================
  // TEST 3: Display count when multiple selected
  // ============================================================================
  it('should display count when multiple collections selected', () => {
    // Arrange
    const collections = createMockCollections();

    // Act
    render(
      <CollectionSourceFilter
        collections={collections}
        selectedDocIds={['coll-1', 'coll-2']}
        onChange={vi.fn()}
      />
    );

    // Assert
    expect(screen.getByText('2 documents')).toBeInTheDocument();
  });

  // ============================================================================
  // TEST 4: All selected shows "All documents"
  // ============================================================================
  it('should display "All documents" when all collections selected', () => {
    // Arrange
    const collections = createMockCollections();

    // Act
    render(
      <CollectionSourceFilter
        collections={collections}
        selectedDocIds={['coll-1', 'coll-2', 'coll-3']}
        onChange={vi.fn()}
      />
    );

    // Assert
    expect(screen.getByText('All documents')).toBeInTheDocument();
  });

  // ============================================================================
  // TEST 5: Open dropdown and show collections
  // ============================================================================
  it('should show all collections when dropdown opened', async () => {
    // Arrange
    const user = userEvent.setup();
    const collections = createMockCollections();

    render(
      <CollectionSourceFilter
        collections={collections}
        selectedDocIds={[]}
        onChange={vi.fn()}
      />
    );

    // Act
    const button = screen.getByRole('button', { name: /filter by document source/i });
    await user.click(button);

    // Assert - All collections visible
    expect(screen.getByText('Base Rules Collection')).toBeInTheDocument();
    expect(screen.getByText('Expansion Rules')).toBeInTheDocument();
    expect(screen.getByText('Errata Documents')).toBeInTheDocument();
  });

  // ============================================================================
  // TEST 6: Show document type badges
  // ============================================================================
  it('should display document type badges for each collection', async () => {
    // Arrange
    const user = userEvent.setup();
    const collections = createMockCollections();

    render(
      <CollectionSourceFilter
        collections={collections}
        selectedDocIds={[]}
        onChange={vi.fn()}
      />
    );

    // Act
    const button = screen.getByRole('button', { name: /filter by document source/i });
    await user.click(button);

    // Assert - Badges visible
    expect(screen.getByText('Base Rules')).toBeInTheDocument();
    expect(screen.getByText('Expansion')).toBeInTheDocument();
    expect(screen.getByText('Errata')).toBeInTheDocument();
  });

  // ============================================================================
  // TEST 7: Show document counts
  // ============================================================================
  it('should display document counts for each collection', async () => {
    // Arrange
    const user = userEvent.setup();
    const collections = createMockCollections();

    render(
      <CollectionSourceFilter
        collections={collections}
        selectedDocIds={[]}
        onChange={vi.fn()}
      />
    );

    // Act
    const button = screen.getByRole('button', { name: /filter by document source/i });
    await user.click(button);

    // Assert - Counts visible
    expect(screen.getByText('3 docs')).toBeInTheDocument();
    expect(screen.getByText('2 docs')).toBeInTheDocument();
    expect(screen.getByText('1 doc')).toBeInTheDocument();
  });

  // ============================================================================
  // TEST 8: Toggle individual collection
  // ============================================================================
  it('should call onChange when individual collection toggled', async () => {
    // Arrange
    const user = userEvent.setup();
    const collections = createMockCollections();
    const mockOnChange = vi.fn();

    render(
      <CollectionSourceFilter
        collections={collections}
        selectedDocIds={[]}
        onChange={mockOnChange}
      />
    );

    // Act - Open dropdown and click on collection
    const button = screen.getByRole('button', { name: /filter by document source/i });
    await user.click(button);

    // Find the checkbox for Base Rules Collection
    const baseRulesCheckbox = screen.getByRole('checkbox', { name: /toggle base rules collection/i });
    await user.click(baseRulesCheckbox);

    // Assert
    expect(mockOnChange).toHaveBeenCalledWith(['coll-1']);
  });

  // ============================================================================
  // TEST 9: Toggle all documents - select all from partial selection
  // ============================================================================
  it('should call onChange with all collection IDs when "All documents" toggled from partial selection', async () => {
    // Arrange
    const user = userEvent.setup();
    const collections = createMockCollections();
    const mockOnChange = vi.fn();

    // Start with partial selection (not empty, not all)
    render(
      <CollectionSourceFilter
        collections={collections}
        selectedDocIds={['coll-1']}
        onChange={mockOnChange}
      />
    );

    // Act - Open dropdown and click on "Select all documents"
    const button = screen.getByRole('button', { name: /filter by document source/i });
    await user.click(button);

    const allDocsCheckbox = screen.getByRole('checkbox', { name: /select all documents/i });
    await user.click(allDocsCheckbox);

    // Assert - Should select all when clicking "All documents" from partial selection
    expect(mockOnChange).toHaveBeenCalledWith(['coll-1', 'coll-2', 'coll-3']);
  });

  // ============================================================================
  // TEST 9b: Toggle all documents - deselect from empty (all selected state)
  // ============================================================================
  it('should call onChange with empty array when "All documents" toggled from empty selection', async () => {
    // Arrange
    const user = userEvent.setup();
    const collections = createMockCollections();
    const mockOnChange = vi.fn();

    // Empty selection means "all selected" in this component's logic
    render(
      <CollectionSourceFilter
        collections={collections}
        selectedDocIds={[]}
        onChange={mockOnChange}
      />
    );

    // Act - Open dropdown and click on "Select all documents"
    const button = screen.getByRole('button', { name: /filter by document source/i });
    await user.click(button);

    const allDocsCheckbox = screen.getByRole('checkbox', { name: /select all documents/i });
    await user.click(allDocsCheckbox);

    // Assert - Empty selection = all selected, so toggling deselects to empty array
    expect(mockOnChange).toHaveBeenCalledWith([]);
  });

  // ============================================================================
  // TEST 10: Deselect from multiple
  // ============================================================================
  it('should remove collection from selection when toggled off', async () => {
    // Arrange
    const user = userEvent.setup();
    const collections = createMockCollections();
    const mockOnChange = vi.fn();

    render(
      <CollectionSourceFilter
        collections={collections}
        selectedDocIds={['coll-1', 'coll-2']}
        onChange={mockOnChange}
      />
    );

    // Act
    const button = screen.getByRole('button', { name: /filter by document source/i });
    await user.click(button);

    const baseRulesCheckbox = screen.getByRole('checkbox', { name: /toggle base rules collection/i });
    await user.click(baseRulesCheckbox);

    // Assert - Should remove coll-1
    expect(mockOnChange).toHaveBeenCalledWith(['coll-2']);
  });

  // ============================================================================
  // TEST 11: Empty collections message
  // ============================================================================
  it('should show "No collections available" when collections array is empty', async () => {
    // Arrange
    const user = userEvent.setup();

    render(
      <CollectionSourceFilter
        collections={[]}
        selectedDocIds={[]}
        onChange={vi.fn()}
      />
    );

    // Act
    const button = screen.getByRole('button', { name: /filter by document source/i });
    await user.click(button);

    // Assert
    expect(screen.getByText('No collections available')).toBeInTheDocument();
  });

  // ============================================================================
  // TEST 12: Check marks for selected items
  // ============================================================================
  it('should show check marks for selected collections', async () => {
    // Arrange
    const user = userEvent.setup();
    const collections = createMockCollections();

    render(
      <CollectionSourceFilter
        collections={collections}
        selectedDocIds={['coll-1']}
        onChange={vi.fn()}
      />
    );

    // Act
    const button = screen.getByRole('button', { name: /filter by document source/i });
    await user.click(button);

    // Assert - Check that checkbox is checked
    const baseRulesCheckbox = screen.getByRole('checkbox', { name: /toggle base rules collection/i });
    expect(baseRulesCheckbox).toHaveAttribute('data-state', 'checked');
  });

  // ============================================================================
  // TEST 13: Custom className support
  // ============================================================================
  it('should apply custom className to button', () => {
    // Arrange
    const collections = createMockCollections();

    // Act
    render(
      <CollectionSourceFilter
        collections={collections}
        selectedDocIds={[]}
        onChange={vi.fn()}
        className="custom-filter-class"
      />
    );

    // Assert
    const button = screen.getByRole('button', { name: /filter by document source/i });
    expect(button).toHaveClass('custom-filter-class');
  });

  // ============================================================================
  // TEST 14: Fallback for unknown single selection
  // ============================================================================
  it('should display "1 document" when single selected ID not found in collections', () => {
    // Arrange
    const collections = createMockCollections();

    // Act
    render(
      <CollectionSourceFilter
        collections={collections}
        selectedDocIds={['unknown-id']}
        onChange={vi.fn()}
      />
    );

    // Assert
    expect(screen.getByText('1 document')).toBeInTheDocument();
  });
});
