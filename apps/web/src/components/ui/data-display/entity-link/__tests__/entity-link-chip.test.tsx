/**
 * EntityLinkChip — C3
 * Tests for Issue #5159 / #5165
 */

import React from 'react';
import { render, screen } from '@testing-library/react';
import { describe, it, expect } from 'vitest';

import { EntityLinkChip } from '../entity-link-chip';
import { LINK_TYPE_CONFIG } from '../entity-link-types';
import type { EntityLinkType } from '../entity-link-types';

const ALL_LINK_TYPES = Object.keys(LINK_TYPE_CONFIG) as EntityLinkType[];

describe('EntityLinkChip', () => {
  it('renders the label and direction icon', () => {
    render(<EntityLinkChip linkType="ExpansionOf" />);
    const config = LINK_TYPE_CONFIG.ExpansionOf;
    const chip = screen.getByText(new RegExp(config.label, 'i'));
    expect(chip).toBeInTheDocument();
  });

  it('renders all link types without crashing', () => {
    for (const lt of ALL_LINK_TYPES) {
      const { unmount } = render(<EntityLinkChip linkType={lt} />);
      unmount();
    }
  });

  it('has sm size class by default', () => {
    render(<EntityLinkChip linkType="RelatedTo" />);
    // sm size uses text-[10px]
    const el = screen.getByText(/related/i).closest('span');
    expect(el).toHaveClass('px-1.5');
  });

  it('has md size class when size="md"', () => {
    render(<EntityLinkChip linkType="RelatedTo" size="md" />);
    const el = screen.getByText(/related/i).closest('span');
    expect(el).toHaveClass('px-2');
  });

  it('has accessible aria-label with direction icon', () => {
    render(<EntityLinkChip linkType="CompanionTo" />);
    const config = LINK_TYPE_CONFIG.CompanionTo;
    const el = screen.getByText(new RegExp(config.directionIcon));
    expect(el).toBeInTheDocument();
  });

  it('applies custom className', () => {
    render(<EntityLinkChip linkType="RelatedTo" className="extra-class" />);
    const el = screen.getByText(/related/i).closest('span');
    expect(el).toHaveClass('extra-class');
  });
});
