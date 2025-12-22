/**
 * CitationLink Component - PDF Page Reference Badge
 *
 * Orange accent badge for inline PDF citations with jump-to-page functionality.
 * Part of the MeepleAI citation system (Issue #1833, UI-006).
 *
 * @see Issue #1833 (UI-006)
 */

import React from 'react';

import { cn } from '@/lib/utils';

import { Badge } from './badge';

// ============================================================================
// Types
// ============================================================================

export interface CitationLinkProps {
  /** PDF page number to reference */
  pageNumber: number;
  /** Optional document name for context */
  documentName?: string;
  /** Click handler for jump-to-page action */
  onClick?: (pageNumber: number) => void;
  /** Additional CSS classes */
  className?: string;
}

// ============================================================================
// Constants
// ============================================================================

const CITATION_LINK_STYLES = {
  base: 'bg-orange-500 hover:bg-orange-600 text-white border-transparent cursor-pointer transition-all duration-200',
  disabled: 'bg-orange-300 text-white border-transparent cursor-not-allowed opacity-60',
  icon: '📄',
} as const;

// ============================================================================
// Helper Functions
// ============================================================================

/**
 * Validates page number is positive integer
 */
function validatePageNumber(pageNumber: number): boolean {
  return Number.isInteger(pageNumber) && pageNumber > 0;
}

/**
 * Formats citation label text
 */
function formatCitationLabel(pageNumber: number, documentName?: string): string {
  const pageText = `p.${pageNumber}`;
  return documentName ? `${documentName} ${pageText}` : `Regolamento ${pageText}`;
}

// ============================================================================
// Main Component
// ============================================================================

/**
 * CitationLink - Clickable PDF page reference badge
 *
 * Displays page number with orange accent color and emoji icon.
 * Clicking triggers jump-to-page action for PDF navigation.
 *
 * **Accessibility**:
 * - WCAG 2.1 AA compliant color contrast
 * - Keyboard navigation (Enter/Space keys)
 * - Screen reader friendly aria-labels
 *
 * @example
 * ```tsx
 * // Basic citation link
 * <CitationLink pageNumber={5} onClick={(page) => jumpToPdfPage(page)} />
 *
 * // With document name
 * <CitationLink
 *   pageNumber={12}
 *   documentName="Catan"
 *   onClick={handlePageClick}
 * />
 *
 * // Non-clickable (display only)
 * <CitationLink pageNumber={3} />
 * ```
 */
export const CitationLink = React.memo<CitationLinkProps>(
  ({ pageNumber, documentName, onClick, className }) => {
    // Validate page number
    if (!validatePageNumber(pageNumber)) {
      console.warn(`[CitationLink] Invalid page number: ${pageNumber}`);
      return null;
    }

    const isClickable = Boolean(onClick);
    const citationLabel = formatCitationLabel(pageNumber, documentName);

    // Event handlers
    const handleClick = (e: React.MouseEvent) => {
      e.preventDefault();
      e.stopPropagation();
      if (isClickable && onClick) {
        onClick(pageNumber);
      }
    };

    const handleKeyDown = (e: React.KeyboardEvent) => {
      if (e.key === 'Enter' || e.key === ' ') {
        e.preventDefault();
        e.stopPropagation();
        if (isClickable && onClick) {
          onClick(pageNumber);
        }
      }
    };

    // Styles
    const badgeStyles = cn(
      'text-xs font-medium inline-flex items-center gap-1',
      isClickable ? CITATION_LINK_STYLES.base : CITATION_LINK_STYLES.disabled,
      className
    );

    // Aria labels
    const ariaLabel = isClickable
      ? `Jump to ${citationLabel} in PDF`
      : `Reference to ${citationLabel}`;

    return (
      <Badge
        className={badgeStyles}
        onClick={isClickable ? handleClick : undefined}
        onKeyDown={isClickable ? handleKeyDown : undefined}
        tabIndex={isClickable ? 0 : undefined}
        role={isClickable ? 'button' : 'status'}
        aria-label={ariaLabel}
        data-testid="citation-link"
        data-page={pageNumber}
      >
        <span aria-hidden="true">{CITATION_LINK_STYLES.icon}</span>
        <span>{citationLabel}</span>
      </Badge>
    );
  }
);

CitationLink.displayName = 'CitationLink';

// ============================================================================
// Exports
// ============================================================================

export { validatePageNumber, formatCitationLabel };
export { CITATION_LINK_STYLES };
