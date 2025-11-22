/**
 * CitationList Component Tests (Issue #859)
 *
 * Tests for citations collection display component
 */

import React from 'react';
import { render, screen, fireEvent } from '@testing-library/react';
import { CitationList } from '@/components/citations/CitationList';
import { Citation } from '@/types';

describe('CitationList', () => {
  const mockCitations: Citation[] = [
    {
      documentId: '880e8400-e29b-41d4-a716-000000000001',
      pageNumber: 10,
      snippet: 'First citation snippet',
      relevanceScore: 0.95,
    },
    {
      documentId: '880e8400-e29b-41d4-a716-000000000002',
      pageNumber: 25,
      snippet: 'Second citation snippet',
      relevanceScore: 0.80,
    },
    {
      documentId: '880e8400-e29b-41d4-a716-000000000003',
      pageNumber: 42,
      snippet: 'Third citation snippet',
      relevanceScore: 0.70,
    },
  ];

  it('renders nothing when citations array is empty', () => {
    const { container } = render(<CitationList citations={[]} />);
    expect(container.firstChild).toBeNull();
  });

  it('renders nothing when citations is undefined', () => {
    const { container } = render(<CitationList citations={null as any} />);
    expect(container.firstChild).toBeNull();
  });

  it('renders header with citation count', () => {
    render(<CitationList citations={mockCitations} />);

    const header = screen.getByTestId('citations-header');
    expect(header).toHaveTextContent('Fonti (3)');
  });

  it('renders all citations in grid', () => {
    render(<CitationList citations={mockCitations} />);

    const cards = screen.getAllByTestId('citation-card');
    expect(cards).toHaveLength(3);
  });

  it('shows citations expanded by default when not collapsible', () => {
    render(<CitationList citations={mockCitations} collapsible={false} />);

    expect(screen.getByTestId('citations-content')).toBeInTheDocument();
  });

  it('shows citations expanded by default when collapsible', () => {
    render(<CitationList citations={mockCitations} collapsible={true} />);

    expect(screen.getByTestId('citations-content')).toBeInTheDocument();
    expect(screen.getByTestId('citations-header')).toHaveAttribute('aria-expanded', 'true');
  });

  it('toggles citations visibility when collapsible', () => {
    render(<CitationList citations={mockCitations} collapsible={true} />);

    const header = screen.getByTestId('citations-header');

    // Initially expanded
    expect(screen.getByTestId('citations-content')).toBeInTheDocument();

    // Click to collapse
    fireEvent.click(header);
    expect(screen.queryByTestId('citations-content')).not.toBeInTheDocument();
    expect(header).toHaveAttribute('aria-expanded', 'false');

    // Click to expand again
    fireEvent.click(header);
    expect(screen.getByTestId('citations-content')).toBeInTheDocument();
    expect(header).toHaveAttribute('aria-expanded', 'true');
  });

  it('does not toggle when not collapsible', () => {
    render(<CitationList citations={mockCitations} collapsible={false} />);

    const header = screen.getByTestId('citations-header');
    expect(header).toBeDisabled();

    // Initially visible
    expect(screen.getByTestId('citations-content')).toBeInTheDocument();

    // Click should not collapse
    fireEvent.click(header);
    expect(screen.getByTestId('citations-content')).toBeInTheDocument();
  });

  it('passes showRelevanceScores to children', () => {
    render(<CitationList citations={mockCitations} showRelevanceScores={true} />);

    // All citation cards should show relevance scores
    const scores = screen.getAllByTestId('citation-score');
    expect(scores).toHaveLength(3);
    expect(scores[0]).toHaveTextContent('95% rilevante');
    expect(scores[1]).toHaveTextContent('80% rilevante');
    expect(scores[2]).toHaveTextContent('70% rilevante');
  });

  it('applies custom className', () => {
    render(<CitationList citations={mockCitations} className="custom-class" />);

    expect(screen.getByTestId('citation-list')).toHaveClass('custom-class');
  });

  it('uses responsive grid layout', () => {
    render(<CitationList citations={mockCitations} />);

    const grid = screen.getByTestId('citations-content');
    expect(grid).toHaveClass('grid');
    expect(grid).toHaveClass('sm:grid-cols-1');
    expect(grid).toHaveClass('md:grid-cols-2');
  });

  it('shows expand/collapse indicator when collapsible', () => {
    const { rerender } = render(
      <CitationList citations={mockCitations} collapsible={true} />
    );

    // Initially expanded (▼)
    expect(screen.getByTestId('citations-header')).toHaveTextContent('▼');

    // Click to collapse
    fireEvent.click(screen.getByTestId('citations-header'));

    // Should show collapsed indicator (▶)
    expect(screen.getByTestId('citations-header')).toHaveTextContent('▶');
  });
});
