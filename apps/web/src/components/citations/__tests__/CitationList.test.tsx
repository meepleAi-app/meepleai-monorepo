/**
 * CitationList Tests - Issue #2308 Week 4
 *
 * Branch coverage tests for CitationList component:
 * 1. Returns null when no citations (empty state)
 * 2. Displays citations count in header
 * 3. Renders grid of CitationCard components
 * 4. Collapses/expands when collapsible prop enabled
 * 5. Passes onClick handler to CitationCards
 *
 * Pattern: Vitest + React Testing Library
 * Coverage target: 91 lines (~1.5% of total)
 */

import { render, screen } from '@testing-library/react';
import userEvent from '@testing-library/user-event';
import { describe, it, expect, vi } from 'vitest';
import React from 'react';

import { CitationList } from '../CitationList';
import { Citation } from '@/types';

const mockCitations: Citation[] = [
  {
    documentId: 'doc-1',
    pageNumber: 5,
    snippet: 'First citation text',
    relevanceScore: 0.95,
    copyrightTier: 'full',
  },
  {
    documentId: 'doc-2',
    pageNumber: 12,
    snippet: 'Second citation text',
    relevanceScore: 0.88,
    copyrightTier: 'full',
  },
  {
    documentId: 'doc-1',
    pageNumber: 7,
    snippet: 'Third citation from same doc',
    relevanceScore: 0.82,
    copyrightTier: 'full',
  },
];

describe('CitationList - Issue #2308', () => {
  // ============================================================================
  // TEST 1: Empty state (null when no citations)
  // ============================================================================
  it('should return null when citations array is empty', () => {
    // Arrange & Act
    const { container } = render(<CitationList citations={[]} />);

    // Assert - Component returns null (no DOM rendered)
    expect(container.firstChild).toBeNull();
  });

  it('should return null when citations is undefined', () => {
    // Arrange & Act
    const { container } = render(<CitationList citations={undefined as any} />);

    // Assert
    expect(container.firstChild).toBeNull();
  });

  // ============================================================================
  // TEST 2: Citation count display
  // ============================================================================
  it('should display correct citation count in header', () => {
    // Act
    render(<CitationList citations={mockCitations} />);

    // Assert - Header shows count
    const header = screen.getByTestId('citations-header');
    expect(header).toHaveTextContent('📚 Fonti (3)');
  });

  // ============================================================================
  // TEST 3: Grid rendering
  // ============================================================================
  it('should render grid with all citations as CitationCards', () => {
    // Act
    render(<CitationList citations={mockCitations} />);

    // Assert - Grid container
    const grid = screen.getByTestId('citations-content');
    expect(grid).toBeInTheDocument();
    expect(grid).toHaveClass('grid', 'gap-2');

    // Assert - All citation cards rendered
    const cards = screen.getAllByTestId('citation-card');
    expect(cards).toHaveLength(3);
  });

  // ============================================================================
  // TEST 4: Collapsible behavior
  // ============================================================================
  it('should toggle collapse when collapsible prop is true', async () => {
    const user = userEvent.setup();

    // Act
    render(<CitationList citations={mockCitations} collapsible />);

    // Assert - Initially expanded
    expect(screen.getByTestId('citations-content')).toBeInTheDocument();
    const header = screen.getByTestId('citations-header');
    expect(header).toHaveTextContent('▼'); // Expanded arrow

    // Act - Click to collapse
    await user.click(header);

    // Assert - Collapsed
    expect(screen.queryByTestId('citations-content')).not.toBeInTheDocument();
    expect(header).toHaveTextContent('▶'); // Collapsed arrow
  });

  it('should not toggle when collapsible is false or undefined', async () => {
    const user = userEvent.setup();

    // Act
    render(<CitationList citations={mockCitations} collapsible={false} />);

    const header = screen.getByTestId('citations-header');

    // Assert - Button disabled
    expect(header).toBeDisabled();

    // Assert - No arrow indicators
    expect(header).not.toHaveTextContent('▼');
    expect(header).not.toHaveTextContent('▶');

    // Act - Click (should not collapse)
    await user.click(header);

    // Assert - Still expanded
    expect(screen.getByTestId('citations-content')).toBeInTheDocument();
  });

  // ============================================================================
  // TEST 5: Click handler propagation
  // ============================================================================
  it('should pass onCitationClick to all CitationCards', () => {
    const mockOnClick = vi.fn();

    // Act
    render(<CitationList citations={mockCitations} onCitationClick={mockOnClick} />);

    // Assert - All cards have clickable styling (presence of onClick handler)
    const cards = screen.getAllByTestId('citation-card');
    cards.forEach(card => {
      expect(card).toHaveAttribute('role', 'button');
      expect(card).toHaveAttribute('tabindex', '0');
    });
  });

  // ============================================================================
  // TEST 6: Relevance scores display
  // ============================================================================
  it('should show relevance scores when showRelevanceScores is true', () => {
    // Act
    render(<CitationList citations={mockCitations} showRelevanceScores />);

    // Assert - Relevance scores visible
    const scores = screen.getAllByTestId('citation-score');
    expect(scores).toHaveLength(3);
    expect(scores[0]).toHaveTextContent('95% rilevante');
    expect(scores[1]).toHaveTextContent('88% rilevante');
    expect(scores[2]).toHaveTextContent('82% rilevante');
  });

  it('should hide relevance scores by default', () => {
    // Act
    render(<CitationList citations={mockCitations} />);

    // Assert - No scores displayed
    expect(screen.queryByTestId('citation-score')).not.toBeInTheDocument();
  });
});
