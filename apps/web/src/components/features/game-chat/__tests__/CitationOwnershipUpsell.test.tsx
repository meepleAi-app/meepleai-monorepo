/**
 * CitationOwnershipUpsell — pure component tests
 * Spec: docs/superpowers/specs/2026-05-10-citation-pdf-viewer-design.md §3.3
 */
import { render, screen } from '@testing-library/react';
import { describe, it, expect } from 'vitest';

import { CitationOwnershipUpsell } from '../CitationOwnershipUpsell';

describe('CitationOwnershipUpsell', () => {
  it('renders title and copyright message', () => {
    render(<CitationOwnershipUpsell />);
    expect(screen.getByText(/PDF originale protetto da copyright/i)).toBeInTheDocument();
    expect(screen.getByText(/carica.*tua copia/i)).toBeInTheDocument();
  });

  it('renders CTA with gameId in href when provided', () => {
    render(<CitationOwnershipUpsell gameId="wingspan" />);
    const cta = screen.getByRole('link', { name: /carica.*pdf/i });
    expect(cta).toHaveAttribute('href', '/upload?gameId=wingspan');
  });

  it('renders CTA without gameId param when missing', () => {
    render(<CitationOwnershipUpsell />);
    const cta = screen.getByRole('link', { name: /carica.*pdf/i });
    expect(cta).toHaveAttribute('href', '/upload');
  });

  it('renders anti-duplication footnote', () => {
    render(<CitationOwnershipUpsell />);
    expect(screen.getByText(/anti-duplicazione|hash identico/i)).toBeInTheDocument();
  });
});
