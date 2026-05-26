import { fireEvent, render, screen } from '@testing-library/react';
import { describe, expect, it, vi } from 'vitest';

import { DeleteDialog } from '../DeleteDialog';

const baseLabels = {
  title: 'Confermi eliminazione PDF?',
  subtitlePrefix: 'PDF:',
  listHeader: 'Verrà eliminato definitivamente:',
  warning: 'Operazione irreversibile — nessun backup disponibile',
  deleteCta: 'Elimina definitivamente',
  cancelCta: 'Annulla',
};

describe('DeleteDialog (Issue #1481)', () => {
  it('does not render content when open=false', () => {
    render(
      <DeleteDialog
        open={false}
        pdfName="Rulebook v2"
        labels={baseLabels}
        onConfirm={() => {}}
        onCancel={() => {}}
      />
    );
    expect(screen.queryByText('Confermi eliminazione PDF?')).not.toBeInTheDocument();
  });

  it('renders title, pdfName, warning, and both CTAs when open', () => {
    render(
      <DeleteDialog
        open
        pdfName="Rulebook v2"
        labels={baseLabels}
        onConfirm={() => {}}
        onCancel={() => {}}
      />
    );
    expect(screen.getByText('Confermi eliminazione PDF?')).toBeInTheDocument();
    expect(screen.getByText('Rulebook v2')).toBeInTheDocument();
    expect(screen.getByText(/Operazione irreversibile/)).toBeInTheDocument();
    expect(screen.getByRole('button', { name: 'Annulla' })).toBeInTheDocument();
    expect(screen.getByRole('button', { name: 'Elimina definitivamente' })).toBeInTheDocument();
  });

  it('hides cleanup list when cleanupItems undefined (P83)', () => {
    render(
      <DeleteDialog
        open
        pdfName="Rulebook v2"
        labels={baseLabels}
        onConfirm={() => {}}
        onCancel={() => {}}
      />
    );
    // DialogContent renders through a Radix Portal — query document, not container.
    expect(
      document.querySelector('[data-slot="kb-hub-delete-cleanup-list"]')
    ).not.toBeInTheDocument();
  });

  it('renders cleanup list when cleanupItems provided', () => {
    const cleanupItems = [
      { key: 'pdf', icon: '📄', label: 'PDF file — 45 MB' },
      { key: 'chunks', icon: '🧩', label: '312 chunk embeddings' },
    ];
    render(
      <DeleteDialog
        open
        pdfName="Rulebook v2"
        labels={baseLabels}
        cleanupItems={cleanupItems}
        onConfirm={() => {}}
        onCancel={() => {}}
      />
    );
    expect(document.querySelector('[data-slot="kb-hub-delete-cleanup-list"]')).toBeInTheDocument();
    expect(screen.getByText('PDF file — 45 MB')).toBeInTheDocument();
    expect(screen.getByText('312 chunk embeddings')).toBeInTheDocument();
  });

  it('invokes onConfirm when destructive CTA clicked', () => {
    const onConfirm = vi.fn();
    render(
      <DeleteDialog
        open
        pdfName="Rulebook v2"
        labels={baseLabels}
        onConfirm={onConfirm}
        onCancel={() => {}}
      />
    );
    fireEvent.click(screen.getByRole('button', { name: 'Elimina definitivamente' }));
    expect(onConfirm).toHaveBeenCalledTimes(1);
  });

  it('invokes onCancel when cancel CTA clicked', () => {
    const onCancel = vi.fn();
    render(
      <DeleteDialog
        open
        pdfName="Rulebook v2"
        labels={baseLabels}
        onConfirm={() => {}}
        onCancel={onCancel}
      />
    );
    fireEvent.click(screen.getByRole('button', { name: 'Annulla' }));
    expect(onCancel).toHaveBeenCalledTimes(1);
  });
});
