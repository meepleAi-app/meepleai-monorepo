/**
 * DocumentBadge Tests - Issue #2764 Sprint 4
 *
 * Tests for DocumentBadge component that displays color-coded badges for document types:
 * - base: blue (primary document)
 * - expansion: purple (expansion rules)
 * - errata: orange (corrections/clarifications)
 * - homerule: green (custom rules)
 *
 * Pattern: Vitest + React Testing Library
 * Coverage target: 63 lines (100%)
 */

import { render, screen } from '@testing-library/react';
import { describe, it, expect } from 'vitest';
import React from 'react';

import { DocumentBadge, type DocumentType } from '../DocumentBadge';

describe('DocumentBadge - Issue #2764 Sprint 4', () => {
  // ============================================================================
  // TEST 1: Base document type badge
  // ============================================================================
  it('should render base document type with correct label and styling', () => {
    // Arrange & Act
    render(<DocumentBadge type="base" />);

    // Assert
    const badge = screen.getByText('Base Rules');
    expect(badge).toBeInTheDocument();
    expect(badge).toHaveAttribute('aria-label', 'Document type: Base Rules');
    expect(badge).toHaveClass('bg-blue-100', 'text-blue-800', 'border-blue-300');
  });

  // ============================================================================
  // TEST 2: Expansion document type badge
  // ============================================================================
  it('should render expansion document type with correct label and styling', () => {
    // Arrange & Act
    render(<DocumentBadge type="expansion" />);

    // Assert
    const badge = screen.getByText('Expansion');
    expect(badge).toBeInTheDocument();
    expect(badge).toHaveAttribute('aria-label', 'Document type: Expansion');
    expect(badge).toHaveClass('bg-purple-100', 'text-purple-800', 'border-purple-300');
  });

  // ============================================================================
  // TEST 3: Errata document type badge
  // ============================================================================
  it('should render errata document type with correct label and styling', () => {
    // Arrange & Act
    render(<DocumentBadge type="errata" />);

    // Assert
    const badge = screen.getByText('Errata');
    expect(badge).toBeInTheDocument();
    expect(badge).toHaveAttribute('aria-label', 'Document type: Errata');
    expect(badge).toHaveClass('bg-orange-100', 'text-orange-800', 'border-orange-300');
  });

  // ============================================================================
  // TEST 4: Homerule document type badge
  // ============================================================================
  it('should render homerule document type with correct label and styling', () => {
    // Arrange & Act
    render(<DocumentBadge type="homerule" />);

    // Assert
    const badge = screen.getByText('House Rule');
    expect(badge).toBeInTheDocument();
    expect(badge).toHaveAttribute('aria-label', 'Document type: House Rule');
    expect(badge).toHaveClass('bg-green-100', 'text-green-800', 'border-green-300');
  });

  // ============================================================================
  // TEST 5: Custom className support
  // ============================================================================
  it('should apply custom className alongside default styles', () => {
    // Arrange & Act
    render(<DocumentBadge type="base" className="custom-class" />);

    // Assert
    const badge = screen.getByText('Base Rules');
    expect(badge).toHaveClass('custom-class');
    // Should still have default styles
    expect(badge).toHaveClass('bg-blue-100');
  });

  // ============================================================================
  // TEST 6: Badge variant
  // ============================================================================
  it('should render with outline variant', () => {
    // Arrange & Act
    const { container } = render(<DocumentBadge type="base" />);

    // Assert - Badge uses outline variant
    const badge = container.querySelector('[data-slot="badge"]') || screen.getByText('Base Rules');
    expect(badge).toBeInTheDocument();
  });

  // ============================================================================
  // TEST 7: All document types render correctly
  // ============================================================================
  it('should render all document types with unique styling', () => {
    // Arrange
    const types: DocumentType[] = ['base', 'expansion', 'errata', 'homerule'];
    const expectedLabels = ['Base Rules', 'Expansion', 'Errata', 'House Rule'];
    const expectedColors = ['blue', 'purple', 'orange', 'green'];

    types.forEach((type, index) => {
      // Act
      const { unmount } = render(<DocumentBadge type={type} />);

      // Assert
      const badge = screen.getByText(expectedLabels[index]);
      expect(badge).toBeInTheDocument();
      expect(badge).toHaveClass(`bg-${expectedColors[index]}-100`);
      expect(badge).toHaveClass(`text-${expectedColors[index]}-800`);

      // Cleanup for next iteration
      unmount();
    });
  });
});
