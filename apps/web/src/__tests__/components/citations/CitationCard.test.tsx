/**
 * CitationCard Component Tests (Issue #859)
 *
 * Tests for individual citation display component
 */

import React from 'react';
import { render, screen } from '@testing-library/react';
import { CitationCard } from '@/components/citations/CitationCard';
import { Citation } from '@/types';

describe('CitationCard', () => {
  const mockCitation: Citation = {
    documentId: 'doc-123',
    pageNumber: 42,
    snippet: 'This is a test citation snippet from the PDF document.',
    relevanceScore: 0.85,
  };

  it('renders citation with page number', () => {
    render(<CitationCard citation={mockCitation} />);

    expect(screen.getByTestId('citation-card')).toBeInTheDocument();
    expect(screen.getByTestId('citation-page')).toHaveTextContent('Pag. 42');
  });

  it('renders citation snippet text', () => {
    render(<CitationCard citation={mockCitation} />);

    const snippet = screen.getByTestId('citation-snippet');
    expect(snippet).toHaveTextContent(mockCitation.snippet);
    expect(snippet).toHaveTextContent('"This is a test citation snippet');
  });

  it('shows relevance score when showRelevanceScore is true', () => {
    render(<CitationCard citation={mockCitation} showRelevanceScore={true} />);

    const score = screen.getByTestId('citation-score');
    expect(score).toBeInTheDocument();
    expect(score).toHaveTextContent('85% rilevante');
  });

  it('hides relevance score by default', () => {
    render(<CitationCard citation={mockCitation} />);

    expect(screen.queryByTestId('citation-score')).not.toBeInTheDocument();
  });

  it('applies custom className', () => {
    render(<CitationCard citation={mockCitation} className="custom-class" />);

    const card = screen.getByTestId('citation-card');
    expect(card).toHaveClass('custom-class');
  });

  it('formats relevance score correctly', () => {
    const citation: Citation = {
      ...mockCitation,
      relevanceScore: 0.9234,
    };

    render(<CitationCard citation={citation} showRelevanceScore={true} />);

    expect(screen.getByTestId('citation-score')).toHaveTextContent('92% rilevante');
  });

  it('has correct accessibility attributes', () => {
    render(<CitationCard citation={mockCitation} showRelevanceScore={true} />);

    const pageBadge = screen.getByTestId('citation-page');
    expect(pageBadge).toHaveAttribute('aria-label', 'Page 42');

    const score = screen.getByTestId('citation-score');
    expect(score).toHaveAttribute('aria-label', 'Relevance 85%');
  });

  it('truncates long snippets with line-clamp', () => {
    const longSnippet = 'A'.repeat(500);
    const citation: Citation = {
      ...mockCitation,
      snippet: longSnippet,
    };

    render(<CitationCard citation={citation} />);

    const snippetElement = screen.getByTestId('citation-snippet');
    expect(snippetElement).toHaveClass('line-clamp-3');
  });
});
