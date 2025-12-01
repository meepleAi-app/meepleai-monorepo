/**
 * CitationLink Component Tests
 *
 * Comprehensive unit tests for CitationLink component.
 * Tests rendering, interactions, accessibility, and edge cases.
 *
 * @see Issue #1833 (UI-006)
 */

import { render, screen } from '@testing-library/react';
import userEvent from '@testing-library/user-event';
import { describe, it, expect, vi } from 'vitest';
import {
  CitationLink,
  validatePageNumber,
  formatCitationLabel,
  CITATION_LINK_STYLES,
} from '@/components/ui/citation-link';

// ============================================================================
// Helper Functions Tests
// ============================================================================

describe('validatePageNumber', () => {
  it('returns true for positive integers', () => {
    expect(validatePageNumber(1)).toBe(true);
    expect(validatePageNumber(5)).toBe(true);
    expect(validatePageNumber(999)).toBe(true);
  });

  it('returns false for invalid page numbers', () => {
    expect(validatePageNumber(0)).toBe(false);
    expect(validatePageNumber(-1)).toBe(false);
    expect(validatePageNumber(-999)).toBe(false);
  });

  it('returns false for non-integers', () => {
    expect(validatePageNumber(1.5)).toBe(false);
    expect(validatePageNumber(3.14)).toBe(false);
  });
});

describe('formatCitationLabel', () => {
  it('formats default citation label without document name', () => {
    expect(formatCitationLabel(5)).toBe('Regolamento p.5');
    expect(formatCitationLabel(12)).toBe('Regolamento p.12');
  });

  it('formats citation label with document name', () => {
    expect(formatCitationLabel(5, 'Catan')).toBe('Catan p.5');
    expect(formatCitationLabel(12, 'Terraforming Mars')).toBe('Terraforming Mars p.12');
  });
});

// ============================================================================
// Component Rendering Tests
// ============================================================================

describe('CitationLink', () => {
  describe('Rendering', () => {
    it('renders citation link with page number', () => {
      render(<CitationLink pageNumber={5} />);
      expect(screen.getByTestId('citation-link')).toBeInTheDocument();
      expect(screen.getByText(/Regolamento p\.5/i)).toBeInTheDocument();
    });

    it('renders citation link with document name', () => {
      render(<CitationLink pageNumber={12} documentName="Catan" />);
      expect(screen.getByText(/Catan p\.12/i)).toBeInTheDocument();
    });

    it('renders emoji icon', () => {
      render(<CitationLink pageNumber={5} />);
      expect(screen.getByText(CITATION_LINK_STYLES.icon)).toBeInTheDocument();
    });

    it('renders null for invalid page number (0)', () => {
      const { container } = render(<CitationLink pageNumber={0} />);
      expect(container.firstChild).toBeNull();
    });

    it('renders null for negative page number', () => {
      const { container } = render(<CitationLink pageNumber={-5} />);
      expect(container.firstChild).toBeNull();
    });

    it('renders null for non-integer page number', () => {
      const { container } = render(<CitationLink pageNumber={1.5} />);
      expect(container.firstChild).toBeNull();
    });

    it('applies custom className', () => {
      render(<CitationLink pageNumber={5} className="custom-class" />);
      const link = screen.getByTestId('citation-link');
      expect(link).toHaveClass('custom-class');
    });
  });

  // ============================================================================
  // Interaction Tests
  // ============================================================================

  describe('Interactions', () => {
    it('calls onClick handler when clicked', async () => {
      const user = userEvent.setup();
      const handleClick = vi.fn();

      render(<CitationLink pageNumber={5} onClick={handleClick} />);
      const link = screen.getByTestId('citation-link');

      await user.click(link);

      expect(handleClick).toHaveBeenCalledTimes(1);
      expect(handleClick).toHaveBeenCalledWith(5);
    });

    it('calls onClick with correct page number', async () => {
      const user = userEvent.setup();
      const handleClick = vi.fn();

      render(<CitationLink pageNumber={42} onClick={handleClick} />);
      await user.click(screen.getByTestId('citation-link'));

      expect(handleClick).toHaveBeenCalledWith(42);
    });

    it('does not call onClick when not clickable', async () => {
      const user = userEvent.setup();
      const handleClick = vi.fn();

      render(<CitationLink pageNumber={5} />);
      const link = screen.getByTestId('citation-link');

      await user.click(link);

      expect(handleClick).not.toHaveBeenCalled();
    });

    it('handles keyboard Enter key', async () => {
      const user = userEvent.setup();
      const handleClick = vi.fn();

      render(<CitationLink pageNumber={5} onClick={handleClick} />);
      const link = screen.getByTestId('citation-link');

      link.focus();
      await user.keyboard('{Enter}');

      expect(handleClick).toHaveBeenCalledWith(5);
    });

    it('handles keyboard Space key', async () => {
      const user = userEvent.setup();
      const handleClick = vi.fn();

      render(<CitationLink pageNumber={5} onClick={handleClick} />);
      const link = screen.getByTestId('citation-link');

      link.focus();
      await user.keyboard(' ');

      expect(handleClick).toHaveBeenCalledWith(5);
    });

    it('does not trigger onClick for other keys', async () => {
      const user = userEvent.setup();
      const handleClick = vi.fn();

      render(<CitationLink pageNumber={5} onClick={handleClick} />);
      const link = screen.getByTestId('citation-link');

      link.focus();
      await user.keyboard('a');
      await user.keyboard('{Escape}');

      expect(handleClick).not.toHaveBeenCalled();
    });
  });

  // ============================================================================
  // Accessibility Tests
  // ============================================================================

  describe('Accessibility', () => {
    it('has button role when clickable', () => {
      render(<CitationLink pageNumber={5} onClick={vi.fn()} />);
      const link = screen.getByTestId('citation-link');
      expect(link).toHaveAttribute('role', 'button');
    });

    it('has status role when not clickable', () => {
      render(<CitationLink pageNumber={5} />);
      const link = screen.getByTestId('citation-link');
      expect(link).toHaveAttribute('role', 'status');
    });

    it('is focusable when clickable (tabIndex=0)', () => {
      render(<CitationLink pageNumber={5} onClick={vi.fn()} />);
      const link = screen.getByTestId('citation-link');
      expect(link).toHaveAttribute('tabIndex', '0');
    });

    it('is not focusable when not clickable', () => {
      render(<CitationLink pageNumber={5} />);
      const link = screen.getByTestId('citation-link');
      expect(link).not.toHaveAttribute('tabIndex');
    });

    it('has correct aria-label for clickable link', () => {
      render(<CitationLink pageNumber={5} onClick={vi.fn()} />);
      const link = screen.getByTestId('citation-link');
      expect(link).toHaveAttribute('aria-label', 'Jump to Regolamento p.5 in PDF');
    });

    it('has correct aria-label for non-clickable link', () => {
      render(<CitationLink pageNumber={5} />);
      const link = screen.getByTestId('citation-link');
      expect(link).toHaveAttribute('aria-label', 'Reference to Regolamento p.5');
    });

    it('has correct aria-label with document name', () => {
      render(<CitationLink pageNumber={12} documentName="Catan" onClick={vi.fn()} />);
      const link = screen.getByTestId('citation-link');
      expect(link).toHaveAttribute('aria-label', 'Jump to Catan p.12 in PDF');
    });

    it('has aria-hidden on emoji icon', () => {
      render(<CitationLink pageNumber={5} />);
      const icon = screen.getByText(CITATION_LINK_STYLES.icon);
      expect(icon).toHaveAttribute('aria-hidden', 'true');
    });

    it('has data-page attribute for testing', () => {
      render(<CitationLink pageNumber={42} />);
      const link = screen.getByTestId('citation-link');
      expect(link).toHaveAttribute('data-page', '42');
    });
  });

  // ============================================================================
  // Styling Tests
  // ============================================================================

  describe('Styling', () => {
    it('applies orange background for clickable link', () => {
      render(<CitationLink pageNumber={5} onClick={vi.fn()} />);
      const link = screen.getByTestId('citation-link');
      expect(link.className).toContain('bg-orange-500');
      expect(link.className).toContain('hover:bg-orange-600');
    });

    it('applies disabled styling for non-clickable link', () => {
      render(<CitationLink pageNumber={5} />);
      const link = screen.getByTestId('citation-link');
      expect(link.className).toContain('bg-orange-300');
      expect(link.className).toContain('cursor-not-allowed');
    });

    it('applies cursor-pointer for clickable link', () => {
      render(<CitationLink pageNumber={5} onClick={vi.fn()} />);
      const link = screen.getByTestId('citation-link');
      expect(link.className).toContain('cursor-pointer');
    });
  });

  // ============================================================================
  // Event Propagation Tests
  // ============================================================================

  describe('Event Propagation', () => {
    it('prevents event propagation on click', async () => {
      const user = userEvent.setup();
      const handleClick = vi.fn();
      const containerClick = vi.fn();

      const { container } = render(
        <div onClick={containerClick}>
          <CitationLink pageNumber={5} onClick={handleClick} />
        </div>
      );

      await user.click(screen.getByTestId('citation-link'));

      expect(handleClick).toHaveBeenCalledTimes(1);
      expect(containerClick).not.toHaveBeenCalled();
    });

    it('prevents event propagation on keyboard', async () => {
      const user = userEvent.setup();
      const handleClick = vi.fn();
      const containerKeyDown = vi.fn();

      render(
        <div onKeyDown={containerKeyDown}>
          <CitationLink pageNumber={5} onClick={handleClick} />
        </div>
      );

      const link = screen.getByTestId('citation-link');
      link.focus();
      await user.keyboard('{Enter}');

      expect(handleClick).toHaveBeenCalledTimes(1);
      expect(containerKeyDown).not.toHaveBeenCalled();
    });
  });
});
