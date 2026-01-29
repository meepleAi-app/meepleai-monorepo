/**
 * CatalogPagination Component Tests
 *
 * Issue #2763: Sprint 3 - Catalog & Shared Games Components (0% → 85%)
 * Issue #2876: Pagination Component - Results display and URL params
 *
 * Tests:
 * - Navigation (first/prev/next/last)
 * - Page number display and ellipsis logic
 * - Boundary conditions (disabled states)
 * - Accessibility (aria labels, current page)
 * - Results info display (#2876)
 */

import { render, screen, fireEvent } from '@testing-library/react';
import { describe, it, expect, vi, beforeEach } from 'vitest';

import { CatalogPagination } from '../CatalogPagination';

describe('CatalogPagination', () => {
  const mockOnPageChange = vi.fn();

  beforeEach(() => {
    vi.clearAllMocks();
  });

  // ==========================================================================
  // Basic Rendering
  // ==========================================================================

  describe('Basic Rendering', () => {
    it('renders all navigation buttons', () => {
      render(
        <CatalogPagination
          currentPage={1}
          totalPages={10}
          onPageChange={mockOnPageChange}
        />
      );

      expect(screen.getByLabelText('First page')).toBeInTheDocument();
      expect(screen.getByLabelText('Previous page')).toBeInTheDocument();
      expect(screen.getByLabelText('Next page')).toBeInTheDocument();
      expect(screen.getByLabelText('Last page')).toBeInTheDocument();
    });

    it('displays current page number', () => {
      render(
        <CatalogPagination
          currentPage={5}
          totalPages={10}
          onPageChange={mockOnPageChange}
        />
      );

      const currentPageButton = screen.getByLabelText('Page 5');
      expect(currentPageButton).toBeInTheDocument();
      expect(currentPageButton).toHaveAttribute('aria-current', 'page');
    });
  });

  // ==========================================================================
  // Page Number Display Logic
  // ==========================================================================

  describe('Page Number Display', () => {
    it('shows all pages when total is 5 or less', () => {
      render(
        <CatalogPagination
          currentPage={3}
          totalPages={5}
          onPageChange={mockOnPageChange}
        />
      );

      for (let i = 1; i <= 5; i++) {
        expect(screen.getByLabelText(`Page ${i}`)).toBeInTheDocument();
      }
      // No ellipsis should be present
      expect(screen.queryByText('...')).not.toBeInTheDocument();
    });

    it('shows ellipsis at end when near start (page 2)', () => {
      render(
        <CatalogPagination
          currentPage={2}
          totalPages={10}
          onPageChange={mockOnPageChange}
        />
      );

      // Should show 1, 2, 3, 4, ..., 10
      expect(screen.getByLabelText('Page 1')).toBeInTheDocument();
      expect(screen.getByLabelText('Page 2')).toBeInTheDocument();
      expect(screen.getByLabelText('Page 3')).toBeInTheDocument();
      expect(screen.getByLabelText('Page 4')).toBeInTheDocument();
      expect(screen.getByLabelText('Page 10')).toBeInTheDocument();
      expect(screen.getByText('...')).toBeInTheDocument();
    });

    it('shows ellipsis at start when near end (page 9)', () => {
      render(
        <CatalogPagination
          currentPage={9}
          totalPages={10}
          onPageChange={mockOnPageChange}
        />
      );

      // Should show 1, ..., 7, 8, 9, 10
      expect(screen.getByLabelText('Page 1')).toBeInTheDocument();
      expect(screen.getByLabelText('Page 7')).toBeInTheDocument();
      expect(screen.getByLabelText('Page 8')).toBeInTheDocument();
      expect(screen.getByLabelText('Page 9')).toBeInTheDocument();
      expect(screen.getByLabelText('Page 10')).toBeInTheDocument();
      expect(screen.getByText('...')).toBeInTheDocument();
    });

    it('shows ellipsis on both sides when in middle (page 5)', () => {
      render(
        <CatalogPagination
          currentPage={5}
          totalPages={10}
          onPageChange={mockOnPageChange}
        />
      );

      // Should show 1, ..., 4, 5, 6, ..., 10
      expect(screen.getByLabelText('Page 1')).toBeInTheDocument();
      expect(screen.getByLabelText('Page 4')).toBeInTheDocument();
      expect(screen.getByLabelText('Page 5')).toBeInTheDocument();
      expect(screen.getByLabelText('Page 6')).toBeInTheDocument();
      expect(screen.getByLabelText('Page 10')).toBeInTheDocument();
      expect(screen.getAllByText('...')).toHaveLength(2);
    });
  });

  // ==========================================================================
  // Navigation
  // ==========================================================================

  describe('Navigation', () => {
    it('calls onPageChange with correct page when clicking page number', () => {
      render(
        <CatalogPagination
          currentPage={1}
          totalPages={5}
          onPageChange={mockOnPageChange}
        />
      );

      fireEvent.click(screen.getByLabelText('Page 3'));
      expect(mockOnPageChange).toHaveBeenCalledWith(3);
    });

    it('navigates to first page', () => {
      render(
        <CatalogPagination
          currentPage={5}
          totalPages={10}
          onPageChange={mockOnPageChange}
        />
      );

      fireEvent.click(screen.getByLabelText('First page'));
      expect(mockOnPageChange).toHaveBeenCalledWith(1);
    });

    it('navigates to previous page', () => {
      render(
        <CatalogPagination
          currentPage={5}
          totalPages={10}
          onPageChange={mockOnPageChange}
        />
      );

      fireEvent.click(screen.getByLabelText('Previous page'));
      expect(mockOnPageChange).toHaveBeenCalledWith(4);
    });

    it('navigates to next page', () => {
      render(
        <CatalogPagination
          currentPage={5}
          totalPages={10}
          onPageChange={mockOnPageChange}
        />
      );

      fireEvent.click(screen.getByLabelText('Next page'));
      expect(mockOnPageChange).toHaveBeenCalledWith(6);
    });

    it('navigates to last page', () => {
      render(
        <CatalogPagination
          currentPage={5}
          totalPages={10}
          onPageChange={mockOnPageChange}
        />
      );

      fireEvent.click(screen.getByLabelText('Last page'));
      expect(mockOnPageChange).toHaveBeenCalledWith(10);
    });
  });

  // ==========================================================================
  // Boundary Conditions
  // ==========================================================================

  describe('Boundary Conditions', () => {
    it('disables first and previous buttons on first page', () => {
      render(
        <CatalogPagination
          currentPage={1}
          totalPages={10}
          onPageChange={mockOnPageChange}
        />
      );

      expect(screen.getByLabelText('First page')).toBeDisabled();
      expect(screen.getByLabelText('Previous page')).toBeDisabled();
      expect(screen.getByLabelText('Next page')).not.toBeDisabled();
      expect(screen.getByLabelText('Last page')).not.toBeDisabled();
    });

    it('disables next and last buttons on last page', () => {
      render(
        <CatalogPagination
          currentPage={10}
          totalPages={10}
          onPageChange={mockOnPageChange}
        />
      );

      expect(screen.getByLabelText('First page')).not.toBeDisabled();
      expect(screen.getByLabelText('Previous page')).not.toBeDisabled();
      expect(screen.getByLabelText('Next page')).toBeDisabled();
      expect(screen.getByLabelText('Last page')).toBeDisabled();
    });

    it('disables all navigation when only one page', () => {
      render(
        <CatalogPagination
          currentPage={1}
          totalPages={1}
          onPageChange={mockOnPageChange}
        />
      );

      expect(screen.getByLabelText('First page')).toBeDisabled();
      expect(screen.getByLabelText('Previous page')).toBeDisabled();
      expect(screen.getByLabelText('Next page')).toBeDisabled();
      expect(screen.getByLabelText('Last page')).toBeDisabled();
    });
  });

  // ==========================================================================
  // Accessibility
  // ==========================================================================

  describe('Accessibility', () => {
    it('has accessible button labels', () => {
      render(
        <CatalogPagination
          currentPage={3}
          totalPages={5}
          onPageChange={mockOnPageChange}
        />
      );

      // All buttons should have aria-label
      expect(screen.getByLabelText('First page')).toBeInTheDocument();
      expect(screen.getByLabelText('Previous page')).toBeInTheDocument();
      expect(screen.getByLabelText('Next page')).toBeInTheDocument();
      expect(screen.getByLabelText('Last page')).toBeInTheDocument();
      expect(screen.getByLabelText('Page 1')).toBeInTheDocument();
      expect(screen.getByLabelText('Page 3')).toBeInTheDocument();
    });

    it('marks current page with aria-current', () => {
      render(
        <CatalogPagination
          currentPage={3}
          totalPages={5}
          onPageChange={mockOnPageChange}
        />
      );

      const currentButton = screen.getByLabelText('Page 3');
      expect(currentButton).toHaveAttribute('aria-current', 'page');

      // Other pages should not have aria-current
      const otherButton = screen.getByLabelText('Page 1');
      expect(otherButton).not.toHaveAttribute('aria-current');
    });

    it('applies different variant to current page button', () => {
      render(
        <CatalogPagination
          currentPage={3}
          totalPages={5}
          onPageChange={mockOnPageChange}
        />
      );

      // Current page should have the default variant (visually distinct)
      const currentButton = screen.getByLabelText('Page 3');
      const otherButton = screen.getByLabelText('Page 1');

      // The current button and other buttons should have different classes
      expect(currentButton.className).not.toBe(otherButton.className);
    });
  });

  // ==========================================================================
  // Results Info Display (Issue #2876)
  // ==========================================================================

  describe('Results Info Display', () => {
    it('displays page info without results when totalResults not provided', () => {
      render(
        <CatalogPagination
          currentPage={3}
          totalPages={10}
          onPageChange={mockOnPageChange}
        />
      );

      expect(screen.getByText('Pagina 3 di 10')).toBeInTheDocument();
    });

    it('displays page info with results count when totalResults provided', () => {
      render(
        <CatalogPagination
          currentPage={3}
          totalPages={10}
          totalResults={195}
          onPageChange={mockOnPageChange}
        />
      );

      expect(screen.getByText(/Pagina 3 di 10/)).toBeInTheDocument();
      expect(screen.getByText(/195 risultati/)).toBeInTheDocument();
    });

    it('formats large result numbers with locale formatting', () => {
      render(
        <CatalogPagination
          currentPage={1}
          totalPages={100}
          totalResults={12345}
          onPageChange={mockOnPageChange}
        />
      );

      // Should use locale formatting (e.g., 12,345 or 12.345 depending on locale)
      const resultsInfo = screen.getByText(/12.*345 risultati/);
      expect(resultsInfo).toBeInTheDocument();
    });

    it('displays zero results correctly', () => {
      render(
        <CatalogPagination
          currentPage={1}
          totalPages={1}
          totalResults={0}
          onPageChange={mockOnPageChange}
        />
      );

      expect(screen.getByText(/0 risultati/)).toBeInTheDocument();
    });

    it('has aria-live on results info for screen readers', () => {
      render(
        <CatalogPagination
          currentPage={1}
          totalPages={5}
          totalResults={100}
          onPageChange={mockOnPageChange}
        />
      );

      const resultsInfo = screen.getByText(/Pagina 1 di 5/).closest('div');
      expect(resultsInfo).toHaveAttribute('aria-live', 'polite');
    });

    it('updates results display when page changes', () => {
      const { rerender } = render(
        <CatalogPagination
          currentPage={1}
          totalPages={5}
          totalResults={100}
          onPageChange={mockOnPageChange}
        />
      );

      expect(screen.getByText(/Pagina 1 di 5/)).toBeInTheDocument();

      rerender(
        <CatalogPagination
          currentPage={3}
          totalPages={5}
          totalResults={100}
          onPageChange={mockOnPageChange}
        />
      );

      expect(screen.getByText(/Pagina 3 di 5/)).toBeInTheDocument();
    });
  });
});
