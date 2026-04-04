/**
 * @vitest-environment jsdom
 */

import { render, screen } from '@testing-library/react';
import { describe, expect, it, vi } from 'vitest';

import { MeeplePdfReferenceCard } from '../MeeplePdfReferenceCard';

describe('MeeplePdfReferenceCard', () => {
  const mockReference = {
    pdfId: 'pdf-1',
    pdfName: 'Catan Rules.pdf',
    pageNumber: 5,
    excerpt: 'Place the robber on the desert hex',
  };

  it('renders with correct entity type', () => {
    render(<MeeplePdfReferenceCard reference={mockReference} onJumpToPage={vi.fn()} />);
    const card = screen.getByTestId('meeple-pdf-reference-card');
    expect(card).toHaveAttribute('data-entity', 'kb');
  });

  it('displays PDF name as title', () => {
    render(<MeeplePdfReferenceCard reference={mockReference} onJumpToPage={vi.fn()} />);
    expect(screen.getByText('Catan Rules.pdf')).toBeInTheDocument();
  });
});
