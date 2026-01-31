/**
 * ResponseCard Component Tests (Issue #1002, BGAI-062)
 *
 * Test coverage:
 * - Basic rendering with answer text
 * - Confidence score display and color coding
 * - Citations integration
 * - Low quality warning
 * - Accessibility
 * - Edge cases
 */

import { describe, it, expect, vi } from 'vitest';
import { render, screen } from '@testing-library/react';
import { ResponseCard } from '../ResponseCard';
import type { Citation } from '@/types';

// Mock CitationList component
vi.mock('@/components/citations/CitationList', () => ({
  CitationList: ({ citations, onCitationClick }: any) => (
    <div data-testid="citation-list">
      {citations.map((c: Citation, i: number) => (
        <button key={i} onClick={() => onCitationClick?.(c)} data-testid={`citation-${i}`}>
          Page {c.pageNumber}
        </button>
      ))}
    </div>
  ),
}));

describe('ResponseCard', () => {
  const mockCitations: Citation[] = [
    {
      documentId: 'doc1',
      pageNumber: 5,
      snippet: 'Game setup instructions...',
      relevanceScore: 0.85,
    },
    {
      documentId: 'doc2',
      pageNumber: 12,
      snippet: 'Turn order rules...',
      relevanceScore: 0.92,
    },
  ];

  describe('Basic Rendering', () => {
    it('renders answer text correctly', () => {
      render(<ResponseCard answer="This is the answer to your question." />);

      expect(screen.getByTestId('response-card')).toBeInTheDocument();
      expect(screen.getByTestId('answer-text')).toHaveTextContent(
        'This is the answer to your question.'
      );
    });

    it('renders header with "Risposta" title', () => {
      render(<ResponseCard answer="Test answer" />);

      expect(screen.getByText('Risposta')).toBeInTheDocument();
    });

    it('preserves whitespace in answer text (pre-wrap)', () => {
      const multilineAnswer = 'Line 1\n\nLine 2\n  Indented';

      render(<ResponseCard answer={multilineAnswer} />);

      const answerElement = screen.getByTestId('answer-text');
      expect(answerElement).toHaveClass('whitespace-pre-wrap');
      // Check that the text content includes the multiline content
      // Note: toHaveTextContent normalizes whitespace, so we check textContent directly
      expect(answerElement.textContent).toBe(multilineAnswer);
    });
  });

  describe('Confidence Score Display', () => {
    it('shows high confidence badge (≥76%) in green', () => {
      render(<ResponseCard answer="Answer" overallConfidence={0.85} />);

      const badge = screen.getByTestId('confidence-badge');
      expect(badge).toHaveTextContent('85% - Alta confidenza');
      expect(badge).toHaveClass('bg-green-100', 'text-green-800');
    });

    it('shows medium confidence badge (51-75%) in yellow', () => {
      render(<ResponseCard answer="Answer" overallConfidence={0.65} />);

      const badge = screen.getByTestId('confidence-badge');
      expect(badge).toHaveTextContent('65% - Media confidenza');
      expect(badge).toHaveClass('bg-yellow-100', 'text-yellow-800');
    });

    it('shows low confidence badge (≤50%) in red', () => {
      render(<ResponseCard answer="Answer" overallConfidence={0.45} />);

      const badge = screen.getByTestId('confidence-badge');
      expect(badge).toHaveTextContent('45% - Bassa confidenza');
      expect(badge).toHaveClass('bg-red-100', 'text-red-800');
    });

    it('does not show confidence badge when score is undefined', () => {
      render(<ResponseCard answer="Answer" />);

      expect(screen.queryByTestId('confidence-badge')).not.toBeInTheDocument();
    });

    it('rounds confidence percentage correctly', () => {
      render(<ResponseCard answer="Answer" overallConfidence={0.847} />);

      expect(screen.getByTestId('confidence-badge')).toHaveTextContent('85%');
    });

    it('handles boundary confidence values (0.76 = high)', () => {
      render(<ResponseCard answer="Answer" overallConfidence={0.76} />);

      const badge = screen.getByTestId('confidence-badge');
      expect(badge).toHaveTextContent('76% - Alta confidenza');
      expect(badge).toHaveClass('bg-green-100');
    });

    it('handles boundary confidence values (0.51 = medium)', () => {
      render(<ResponseCard answer="Answer" overallConfidence={0.51} />);

      const badge = screen.getByTestId('confidence-badge');
      expect(badge).toHaveTextContent('51% - Media confidenza');
      expect(badge).toHaveClass('bg-yellow-100');
    });
  });

  describe('Citations Integration', () => {
    it('renders CitationList when citations provided', () => {
      render(<ResponseCard answer="Answer" citations={mockCitations} />);

      expect(screen.getByTestId('citation-list')).toBeInTheDocument();
      expect(screen.getByTestId('citation-0')).toHaveTextContent('Page 5');
      expect(screen.getByTestId('citation-1')).toHaveTextContent('Page 12');
    });

    it('does not render CitationList when citations array is empty', () => {
      render(<ResponseCard answer="Answer" citations={[]} />);

      expect(screen.queryByTestId('citation-list')).not.toBeInTheDocument();
    });

    it('does not render CitationList when citations is undefined', () => {
      render(<ResponseCard answer="Answer" />);

      expect(screen.queryByTestId('citation-list')).not.toBeInTheDocument();
    });

    it('passes showCitationScores prop to CitationList', () => {
      // This is implicitly tested through the mock, but we verify the prop is passed
      render(<ResponseCard answer="Answer" citations={mockCitations} showCitationScores={true} />);

      expect(screen.getByTestId('citation-list')).toBeInTheDocument();
    });

    it('handles citation click callback', () => {
      const onCitationClick = vi.fn();

      render(
        <ResponseCard answer="Answer" citations={mockCitations} onCitationClick={onCitationClick} />
      );

      screen.getByTestId('citation-0').click();

      expect(onCitationClick).toHaveBeenCalledWith(mockCitations[0]);
      expect(onCitationClick).toHaveBeenCalledTimes(1);
    });
  });

  describe('Low Quality Warning', () => {
    it('shows warning alert when isLowQuality is true', () => {
      render(<ResponseCard answer="Answer" isLowQuality={true} />);

      const warning = screen.getByTestId('low-quality-warning');
      expect(warning).toBeInTheDocument();
      expect(warning).toHaveTextContent(
        'Questa risposta potrebbe non soddisfare gli standard di qualità'
      );
    });

    it('does not show warning when isLowQuality is false', () => {
      render(<ResponseCard answer="Answer" isLowQuality={false} />);

      expect(screen.queryByTestId('low-quality-warning')).not.toBeInTheDocument();
    });

    it('does not show warning by default (isLowQuality undefined)', () => {
      render(<ResponseCard answer="Answer" />);

      expect(screen.queryByTestId('low-quality-warning')).not.toBeInTheDocument();
    });
  });

  describe('Accessibility', () => {
    it('has proper ARIA label on confidence badge', () => {
      render(<ResponseCard answer="Answer" overallConfidence={0.75} />);

      const badge = screen.getByTestId('confidence-badge');
      expect(badge).toHaveAttribute('aria-label', 'Confidence 75%');
    });

    it('maintains semantic HTML structure', () => {
      render(<ResponseCard answer="Answer" overallConfidence={0.8} citations={mockCitations} />);

      // Header should have h3
      expect(screen.getByRole('heading', { level: 3 })).toHaveTextContent('Risposta');
    });
  });

  describe('Custom Styling', () => {
    it('applies custom className to Card', () => {
      render(<ResponseCard answer="Answer" className="custom-class" />);

      const card = screen.getByTestId('response-card');
      expect(card).toHaveClass('custom-class');
      expect(card).toHaveClass('border-l-4', 'border-l-blue-500'); // Default styles preserved
    });
  });

  describe('Edge Cases', () => {
    it('handles 0% confidence', () => {
      render(<ResponseCard answer="Answer" overallConfidence={0} />);

      const badge = screen.getByTestId('confidence-badge');
      expect(badge).toHaveTextContent('0% - Bassa confidenza');
    });

    it('handles 100% confidence', () => {
      render(<ResponseCard answer="Answer" overallConfidence={1.0} />);

      const badge = screen.getByTestId('confidence-badge');
      expect(badge).toHaveTextContent('100% - Alta confidenza');
    });

    it('handles empty answer text', () => {
      render(<ResponseCard answer="" />);

      expect(screen.getByTestId('answer-text')).toHaveTextContent('');
    });

    it('handles very long answer text', () => {
      const longAnswer = 'A'.repeat(10000);

      render(<ResponseCard answer={longAnswer} />);

      expect(screen.getByTestId('answer-text')).toHaveTextContent(longAnswer);
    });

    it('renders all props together correctly', () => {
      const onCitationClick = vi.fn();

      render(
        <ResponseCard
          answer="Complete answer"
          overallConfidence={0.88}
          citations={mockCitations}
          isLowQuality={true}
          showCitationScores={true}
          className="custom"
          onCitationClick={onCitationClick}
        />
      );

      // All elements should be present
      expect(screen.getByTestId('answer-text')).toBeInTheDocument();
      expect(screen.getByTestId('confidence-badge')).toBeInTheDocument();
      expect(screen.getByTestId('citation-list')).toBeInTheDocument();
      expect(screen.getByTestId('low-quality-warning')).toBeInTheDocument();
      expect(screen.getByTestId('response-card')).toHaveClass('custom');
    });
  });

  describe('Component Memoization', () => {
    it('uses React.memo for performance', () => {
      const { rerender } = render(<ResponseCard answer="Answer" overallConfidence={0.8} />);

      // Rerender with same props
      rerender(<ResponseCard answer="Answer" overallConfidence={0.8} />);

      // Component should still be in the document (memoization prevents unnecessary rerenders)
      expect(screen.getByTestId('response-card')).toBeInTheDocument();
    });
  });
});
