/**
 * CitationBadge Tests (Issue #3244)
 */

import { describe, it, expect, vi } from 'vitest';
import { render, screen } from '@testing-library/react';
import userEvent from '@testing-library/user-event';

import { CitationBadge } from '../CitationBadge';
import type { Citation } from '@/lib/api/schemas/streaming.schemas';

describe('CitationBadge', () => {
  const mockCitation: Citation = {
    source: 'Chess Rulebook',
    pageNumber: 5,
    score: 0.95,
    snippet: 'The king can move one square in any direction...',
  };

  describe('Rendering', () => {
    it('renders citation with source and page number', () => {
      render(<CitationBadge citation={mockCitation} />);

      expect(screen.getByText('Chess Rulebook')).toBeInTheDocument();
      expect(screen.getByText('p.5')).toBeInTheDocument();
    });

    it('renders score percentage', () => {
      render(<CitationBadge citation={mockCitation} />);

      expect(screen.getByText('(95%)')).toBeInTheDocument();
    });

    it('renders icon', () => {
      const { container } = render(<CitationBadge citation={mockCitation} />);

      const icon = container.querySelector('svg');
      expect(icon).toBeInTheDocument();
    });

    it('renders without page number when not provided', () => {
      const citationNoPage: Citation = {
        source: 'Manual',
        score: 0.8,
      };

      render(<CitationBadge citation={citationNoPage} />);

      expect(screen.getByText('Manual')).toBeInTheDocument();
      expect(screen.queryByText(/p\./)).not.toBeInTheDocument();
    });

    it('renders without score when not provided', () => {
      const citationNoScore: Citation = {
        source: 'Guide',
        pageNumber: 3,
      };

      render(<CitationBadge citation={citationNoScore} />);

      expect(screen.getByText('Guide')).toBeInTheDocument();
      expect(screen.queryByText(/\(\d+%\)/)).not.toBeInTheDocument();
    });

    it('uses fallback "Source" when source is empty', () => {
      const citationNoSource: Citation = {
        source: '',
        pageNumber: 1,
      };

      render(<CitationBadge citation={citationNoSource} />);

      expect(screen.getByText('Source')).toBeInTheDocument();
    });

    it('handles alternative field names (page vs pageNumber)', () => {
      const citationAltFields: Citation = {
        source: 'Rules',
        page: 7, // Using 'page' instead of 'pageNumber'
      };

      render(<CitationBadge citation={citationAltFields} />);

      expect(screen.getByText('p.7')).toBeInTheDocument();
    });

    it('handles alternative score field (relevanceScore)', () => {
      const citationAltScore: Citation = {
        source: 'Manual',
        relevanceScore: 0.88,
      };

      render(<CitationBadge citation={citationAltScore} />);

      expect(screen.getByText('(88%)')).toBeInTheDocument();
    });
  });

  describe('Tooltip', () => {
    it('wraps badge in Tooltip when snippet is provided', () => {
      const { container } = render(<CitationBadge citation={mockCitation} />);

      // Component should render (tooltip may be in portal)
      const button = container.querySelector('button');
      expect(button).toBeInTheDocument();
    });

    it('renders without snippet field when not provided', () => {
      const citationNoSnippet: Citation = {
        source: 'Rules',
        pageNumber: 1,
      };

      const { container } = render(<CitationBadge citation={citationNoSnippet} />);

      const button = container.querySelector('button');
      expect(button).toBeInTheDocument();
    });
  });

  describe('Click Handling', () => {
    it('calls onClick with citation when clicked', async () => {
      const user = userEvent.setup();
      const handleClick = vi.fn();

      render(<CitationBadge citation={mockCitation} onClick={handleClick} />);

      const button = screen.getByRole('button');
      await user.click(button);

      expect(handleClick).toHaveBeenCalledWith(mockCitation);
      expect(handleClick).toHaveBeenCalledTimes(1);
    });

    it('does not error when onClick is not provided', async () => {
      const user = userEvent.setup();

      render(<CitationBadge citation={mockCitation} />);

      const button = screen.getByRole('button');
      await user.click(button);

      // Should not throw error
      expect(button).toBeInTheDocument();
    });
  });

  describe('Styling', () => {
    it('has correct hover styles', () => {
      const { container } = render(<CitationBadge citation={mockCitation} />);

      const button = container.querySelector('button');
      expect(button).toHaveClass('dark:hover:bg-gray-600');
      expect(button).toHaveClass('transition-all');
    });

    it('has focus ring for accessibility', () => {
      const { container } = render(<CitationBadge citation={mockCitation} />);

      const button = container.querySelector('button');
      expect(button).toHaveClass('focus:ring-2');
      expect(button).toHaveClass('focus:ring-cyan-400');
    });

    it('uses correct color for source text', () => {
      const { container } = render(<CitationBadge citation={mockCitation} />);

      // The text color is on the button element, inherited by the source span
      const button = container.querySelector('button');
      expect(button).toHaveClass('dark:text-cyan-400');
    });
  });
});
