/**
 * CitationCard Tests - Issue #2308 Week 4
 *
 * Branch coverage tests for CitationCard component:
 * 1. Displays page number badge and snippet
 * 2. Shows relevance score when enabled
 * 3. Handles click and keyboard interactions when clickable
 * 4. Applies hover styles only when clickable
 *
 * Pattern: Vitest + React Testing Library
 * Coverage target: 104 lines (~2% of total)
 */

import { render, screen } from '@testing-library/react';
import userEvent from '@testing-library/user-event';
import { describe, it, expect, vi } from 'vitest';
import React from 'react';

import { CitationCard } from '../CitationCard';
import { Citation } from '@/types';

const mockCitation: Citation = {
  documentId: 'doc-123',
  pageNumber: 5,
  snippet:
    'Development cards can be played at any time during your turn except on the turn they were purchased.',
  relevanceScore: 0.95,
  copyrightTier: 'full',
};

describe('CitationCard - Issue #2308', () => {
  // ============================================================================
  // TEST 1: Basic rendering with page number and snippet
  // ============================================================================
  it('should display page number badge and snippet text', () => {
    // Act
    render(<CitationCard citation={mockCitation} />);

    // Assert - Page number badge
    const pageBadge = screen.getByTestId('citation-page');
    expect(pageBadge).toHaveTextContent('Pag. 5');
    expect(pageBadge).toHaveAttribute('aria-label', 'Page 5');

    // Assert - Snippet text with quotes
    const snippet = screen.getByTestId('citation-snippet');
    expect(snippet).toHaveTextContent('"Development cards can be played at any time');
    expect(snippet).toHaveTextContent('except on the turn they were purchased."');
  });

  // ============================================================================
  // TEST 2: Relevance score display (conditional rendering)
  // ============================================================================
  it('should show relevance score when showRelevanceScore is true', () => {
    // Act
    render(<CitationCard citation={mockCitation} showRelevanceScore />);

    // Assert - Relevance score visible
    const score = screen.getByTestId('citation-score');
    expect(score).toBeInTheDocument();
    expect(score).toHaveTextContent('95% rilevante');
    expect(score).toHaveAttribute('aria-label', 'Relevance 95%');
    expect(score).toHaveAttribute('title', 'Relevance score: 95%');
  });

  it('should hide relevance score when showRelevanceScore is false or undefined', () => {
    // Act
    render(<CitationCard citation={mockCitation} showRelevanceScore={false} />);

    // Assert - Relevance score hidden
    expect(screen.queryByTestId('citation-score')).not.toBeInTheDocument();
  });

  // ============================================================================
  // TEST 3: Click handler and keyboard interactions
  // ============================================================================
  it('should call onClick with citation when clicked', async () => {
    const user = userEvent.setup();
    const mockOnClick = vi.fn();

    // Act
    render(<CitationCard citation={mockCitation} onClick={mockOnClick} />);

    const card = screen.getByTestId('citation-card');
    await user.click(card);

    // Assert - onClick called with citation object
    expect(mockOnClick).toHaveBeenCalledTimes(1);
    expect(mockOnClick).toHaveBeenCalledWith(mockCitation);
  });

  it('should call onClick when Enter key pressed', async () => {
    const user = userEvent.setup();
    const mockOnClick = vi.fn();

    // Act
    render(<CitationCard citation={mockCitation} onClick={mockOnClick} />);

    const card = screen.getByTestId('citation-card');
    card.focus();
    await user.keyboard('{Enter}');

    // Assert
    expect(mockOnClick).toHaveBeenCalledTimes(1);
    expect(mockOnClick).toHaveBeenCalledWith(mockCitation);
  });

  it('should call onClick when Space key pressed', async () => {
    const user = userEvent.setup();
    const mockOnClick = vi.fn();

    // Act
    render(<CitationCard citation={mockCitation} onClick={mockOnClick} />);

    const card = screen.getByTestId('citation-card');
    card.focus();
    await user.keyboard(' ');

    // Assert
    expect(mockOnClick).toHaveBeenCalledTimes(1);
  });

  // ============================================================================
  // TEST 4: Accessibility and styling based on clickable state
  // ============================================================================
  it('should have button role and cursor-pointer when onClick provided', () => {
    const mockOnClick = vi.fn();

    // Act
    render(<CitationCard citation={mockCitation} onClick={mockOnClick} />);

    // Assert - Interactive attributes
    const card = screen.getByTestId('citation-card');
    expect(card).toHaveAttribute('role', 'button');
    expect(card).toHaveAttribute('tabIndex', '0');
    expect(card).toHaveAttribute('aria-label', 'View citation from page 5');
    expect(card).toHaveClass('cursor-pointer');

    // Assert - PDF icon in page badge
    expect(screen.getByText('📄')).toBeInTheDocument();

    // Assert - Click hint text
    expect(screen.getByText('Clicca per visualizzare nel PDF')).toBeInTheDocument();
  });

  it('should not have button role or interactive styles when onClick not provided', () => {
    // Act
    render(<CitationCard citation={mockCitation} />);

    // Assert - No interactive attributes
    const card = screen.getByTestId('citation-card');
    expect(card).not.toHaveAttribute('role');
    expect(card).not.toHaveAttribute('tabIndex');
    expect(card).not.toHaveAttribute('aria-label');
    expect(card).not.toHaveClass('cursor-pointer');

    // Assert - No PDF icon
    expect(screen.queryByText('📄')).not.toBeInTheDocument();

    // Assert - No click hint
    expect(screen.queryByText('Clicca per visualizzare nel PDF')).not.toBeInTheDocument();
  });

  // ============================================================================
  // TEST 5: Custom className propagation
  // ============================================================================
  it('should apply custom className while preserving default styles', () => {
    // Act
    render(<CitationCard citation={mockCitation} className="custom-class" />);

    // Assert - Custom class applied
    const card = screen.getByTestId('citation-card');
    expect(card).toHaveClass('custom-class');

    // Assert - Default styles preserved
    expect(card).toHaveClass('border-l-4', 'border-l-blue-500', 'bg-gray-50');
  });
});
