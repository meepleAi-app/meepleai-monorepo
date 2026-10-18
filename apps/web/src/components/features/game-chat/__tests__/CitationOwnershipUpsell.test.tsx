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

  // #4257: /upload era riservata ad Admin ed Editor (e ora e' dismessa). Chi vede questa
  // card e' un utente normale: il suo PDF lo carica dalla pagina KB del gioco in libreria.
  it('la CTA porta alla pagina KB del gioco in libreria', () => {
    render(<CitationOwnershipUpsell gameId="wingspan" />);
    const cta = screen.getByRole('link', { name: /carica.*pdf/i });
    expect(cta).toHaveAttribute('href', '/library/wingspan/kb');
  });

  it('senza gioco la CTA porta alla libreria', () => {
    render(<CitationOwnershipUpsell />);
    const cta = screen.getByRole('link', { name: /carica.*pdf/i });
    expect(cta).toHaveAttribute('href', '/library');
  });

  it("codifica l'id del gioco nel percorso", () => {
    render(<CitationOwnershipUpsell gameId="a/b c" />);
    const cta = screen.getByRole('link', { name: /carica.*pdf/i });
    expect(cta).toHaveAttribute('href', '/library/a%2Fb%20c/kb');
  });

  it('renders anti-duplication footnote', () => {
    render(<CitationOwnershipUpsell />);
    expect(screen.getByText(/anti-duplicazione|hash identico/i)).toBeInTheDocument();
  });
});
