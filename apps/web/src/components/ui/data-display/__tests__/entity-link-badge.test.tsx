/**
 * EntityLinkBadge Tests - Issue #5197
 *
 * Tests for EntityLinkBadge component (Issue #5194):
 * - KB Card handler: teal badge with FileText icon and count
 * - Generic handler: fallback badge with link icon and label
 * - Both handlers: label override, size variants, aria-label
 *
 * Pattern: Vitest + React Testing Library
 */

import { describe, it, expect } from 'vitest';
import { render, screen } from '@testing-library/react';

import { EntityLinkBadge } from '../entity-link-badge';

// ============================================================================
// Tests
// ============================================================================

describe('EntityLinkBadge - Issue #5197', () => {
  // --------------------------------------------------------------------------
  // KB Card handler
  // --------------------------------------------------------------------------

  describe('KbCard handler', () => {
    it('renders kb badge with data-testid entity-link-badge-kb', () => {
      render(<EntityLinkBadge linkType="PartOf" sourceEntityType="KbCard" />);
      expect(screen.getByTestId('entity-link-badge-kb')).toBeInTheDocument();
    });

    it('renders default label "Documenti KB"', () => {
      render(<EntityLinkBadge linkType="PartOf" sourceEntityType="KbCard" />);
      expect(screen.getByTestId('entity-link-badge-kb')).toHaveTextContent('Documenti KB');
    });

    it('renders count when provided', () => {
      render(<EntityLinkBadge linkType="PartOf" sourceEntityType="KbCard" count={5} />);
      const badge = screen.getByTestId('entity-link-badge-kb');
      expect(badge).toHaveTextContent('5');
    });

    it('renders custom label override', () => {
      render(<EntityLinkBadge linkType="PartOf" sourceEntityType="KbCard" label="PDF" />);
      expect(screen.getByTestId('entity-link-badge-kb')).toHaveTextContent('PDF');
    });

    it('has correct aria-label with count', () => {
      render(<EntityLinkBadge linkType="PartOf" sourceEntityType="KbCard" count={3} />);
      expect(screen.getByTestId('entity-link-badge-kb')).toHaveAttribute(
        'aria-label',
        'Documenti KB: 3 collegate'
      );
    });

    it('has aria-label with 0 when no count provided', () => {
      render(<EntityLinkBadge linkType="PartOf" sourceEntityType="KbCard" />);
      expect(screen.getByTestId('entity-link-badge-kb')).toHaveAttribute(
        'aria-label',
        'Documenti KB: 0 collegate'
      );
    });

    it('does NOT render generic badge testid', () => {
      render(<EntityLinkBadge linkType="PartOf" sourceEntityType="KbCard" />);
      expect(screen.queryByTestId('entity-link-badge-generic')).not.toBeInTheDocument();
    });
  });

  // --------------------------------------------------------------------------
  // Generic handler
  // --------------------------------------------------------------------------

  describe('generic handler', () => {
    it('renders generic badge with data-testid entity-link-badge-generic', () => {
      render(<EntityLinkBadge linkType="RelatedTo" />);
      expect(screen.getByTestId('entity-link-badge-generic')).toBeInTheDocument();
    });

    it('renders label from LINK_TYPE_LABELS map for RelatedTo', () => {
      render(<EntityLinkBadge linkType="RelatedTo" />);
      expect(screen.getByTestId('entity-link-badge-generic')).toHaveTextContent('Correlato');
    });

    it('renders label for PartOf without KbCard source', () => {
      render(<EntityLinkBadge linkType="PartOf" />);
      expect(screen.getByTestId('entity-link-badge-generic')).toHaveTextContent('Parte di');
    });

    it('renders custom label override', () => {
      render(<EntityLinkBadge linkType="RelatedTo" label="My Label" />);
      expect(screen.getByTestId('entity-link-badge-generic')).toHaveTextContent('My Label');
    });

    it('renders count when provided', () => {
      render(<EntityLinkBadge linkType="ExpansionOf" count={2} />);
      expect(screen.getByTestId('entity-link-badge-generic')).toHaveTextContent('2');
    });

    it('has correct aria-label without count', () => {
      render(<EntityLinkBadge linkType="RelatedTo" />);
      expect(screen.getByTestId('entity-link-badge-generic')).toHaveAttribute(
        'aria-label',
        'Correlato'
      );
    });

    it('has correct aria-label with count', () => {
      render(<EntityLinkBadge linkType="RelatedTo" count={4} />);
      expect(screen.getByTestId('entity-link-badge-generic')).toHaveAttribute(
        'aria-label',
        'Correlato: 4'
      );
    });

    it('renders all defined link types without error', () => {
      const types = [
        'ExpansionOf',
        'SequelOf',
        'Reimplements',
        'CompanionTo',
        'RelatedTo',
        'CollaboratesWith',
        'PartOf',
        'SpecializedBy',
      ] as const;
      types.forEach(linkType => {
        const { unmount } = render(<EntityLinkBadge linkType={linkType} />);
        expect(screen.getByTestId('entity-link-badge-generic')).toBeInTheDocument();
        unmount();
      });
    });
  });

  // --------------------------------------------------------------------------
  // Size variants
  // --------------------------------------------------------------------------

  describe('size prop', () => {
    it('defaults to sm size for KbCard badge', () => {
      render(<EntityLinkBadge linkType="PartOf" sourceEntityType="KbCard" />);
      // sm: px-2 py-0.5 text-[10px]
      expect(screen.getByTestId('entity-link-badge-kb')).toHaveClass('px-2');
    });

    it('applies md size for KbCard badge', () => {
      render(<EntityLinkBadge linkType="PartOf" sourceEntityType="KbCard" size="md" />);
      // md: px-2.5 py-1 text-xs
      expect(screen.getByTestId('entity-link-badge-kb')).toHaveClass('px-2.5');
    });

    it('defaults to sm size for generic badge', () => {
      render(<EntityLinkBadge linkType="RelatedTo" />);
      expect(screen.getByTestId('entity-link-badge-generic')).toHaveClass('px-2');
    });

    it('applies md size for generic badge', () => {
      render(<EntityLinkBadge linkType="RelatedTo" size="md" />);
      expect(screen.getByTestId('entity-link-badge-generic')).toHaveClass('px-2.5');
    });
  });

  // --------------------------------------------------------------------------
  // Custom className
  // --------------------------------------------------------------------------

  it('applies custom className to KB badge', () => {
    render(<EntityLinkBadge linkType="PartOf" sourceEntityType="KbCard" className="custom-kb" />);
    expect(screen.getByTestId('entity-link-badge-kb')).toHaveClass('custom-kb');
  });

  it('applies custom className to generic badge', () => {
    render(<EntityLinkBadge linkType="RelatedTo" className="custom-generic" />);
    expect(screen.getByTestId('entity-link-badge-generic')).toHaveClass('custom-generic');
  });
});
