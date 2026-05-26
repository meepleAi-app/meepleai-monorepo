import { fireEvent, render, screen } from '@testing-library/react';
import { describe, expect, it, vi } from 'vitest';

import { PdfRow, type KbPdf } from '../PdfRow';

const baseLabels = {
  openCta: 'Apri dettaglio',
  openAria: 'Apri dettaglio {pdfName}',
  chunksLabel: '{count} chunks',
  status: {
    ready: 'Ready',
    indexing: 'Indexing',
    stale: 'Stale',
    failed: 'Failed',
  },
};

const basePdf: KbPdf = {
  id: 'pdf-1',
  name: 'Rulebook v2',
  sizeFormatted: '45 MB',
  uploadedAtRelative: '2 gg fa',
};

describe('PdfRow (Issue #1481)', () => {
  it('renders name, size, date and CTA with aria-label', () => {
    render(<PdfRow pdf={basePdf} labels={baseLabels} onActionClick={() => {}} />);
    expect(screen.getByText('Rulebook v2')).toBeInTheDocument();
    expect(screen.getByText('45 MB')).toBeInTheDocument();
    expect(screen.getByText('2 gg fa')).toBeInTheDocument();
    expect(screen.getByRole('button', { name: 'Apri dettaglio Rulebook v2' })).toBeInTheDocument();
  });

  it('hides status badge when status is undefined (P83 deferred)', () => {
    const { container } = render(
      <PdfRow pdf={basePdf} labels={baseLabels} onActionClick={() => {}} />
    );
    expect(container.querySelector('[data-slot="kb-hub-pdf-status"]')).not.toBeInTheDocument();
    expect(container.querySelector('[data-slot="kb-hub-pdf-status-empty"]')).toBeInTheDocument();
  });

  it('renders status badge with localized label when status provided', () => {
    render(
      <PdfRow
        pdf={{ ...basePdf, status: 'indexing' }}
        labels={baseLabels}
        onActionClick={() => {}}
      />
    );
    expect(screen.getByText('Indexing')).toBeInTheDocument();
  });

  it('renders chunks suffix when chunks > 0', () => {
    render(
      <PdfRow pdf={{ ...basePdf, chunks: 312 }} labels={baseLabels} onActionClick={() => {}} />
    );
    expect(screen.getByText('312 chunks')).toBeInTheDocument();
  });

  it('omits chunks suffix when chunks is undefined or zero', () => {
    const { rerender } = render(
      <PdfRow pdf={basePdf} labels={baseLabels} onActionClick={() => {}} />
    );
    expect(screen.queryByText(/chunks$/)).not.toBeInTheDocument();
    rerender(
      <PdfRow pdf={{ ...basePdf, chunks: 0 }} labels={baseLabels} onActionClick={() => {}} />
    );
    expect(screen.queryByText(/chunks$/)).not.toBeInTheDocument();
  });

  it('invokes onActionClick with pdf.id', () => {
    const onActionClick = vi.fn();
    render(<PdfRow pdf={basePdf} labels={baseLabels} onActionClick={onActionClick} />);
    fireEvent.click(screen.getByRole('button', { name: /Apri dettaglio Rulebook v2/ }));
    expect(onActionClick).toHaveBeenCalledWith('pdf-1');
  });
});
