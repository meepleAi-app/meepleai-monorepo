import { fireEvent, render, screen } from '@testing-library/react';
import { describe, expect, it, vi } from 'vitest';

import { EmptyState } from '../EmptyState';

const baseLabels = {
  title: 'Nessun PDF indicizzato',
  description: 'Carica il primo documento PDF per indicizzare le regole di {gameTitle}.',
  ctaLabel: 'Carica primo documento',
  supportedFormats: 'Formati supportati: PDF · Max 200 MB per file',
};

describe('EmptyState (Issue #1481)', () => {
  it('renders title, description (with gameTitle), CTA and supported formats', () => {
    render(<EmptyState gameTitle="Gloomhaven" labels={baseLabels} onUpload={() => {}} />);
    expect(screen.getByText('Nessun PDF indicizzato')).toBeInTheDocument();
    expect(
      screen.getByText('Carica il primo documento PDF per indicizzare le regole di Gloomhaven.')
    ).toBeInTheDocument();
    expect(screen.getByRole('button', { name: /Carica primo documento/i })).toBeInTheDocument();
    expect(screen.getByText(/Formati supportati: PDF/)).toBeInTheDocument();
  });

  it('invokes onUpload when CTA clicked', () => {
    const onUpload = vi.fn();
    render(<EmptyState gameTitle="Test" labels={baseLabels} onUpload={onUpload} />);
    fireEvent.click(screen.getByRole('button', { name: /Carica primo documento/i }));
    expect(onUpload).toHaveBeenCalledTimes(1);
  });

  it('uses data-slot attribute for regression guards', () => {
    const { container } = render(
      <EmptyState gameTitle="Test" labels={baseLabels} onUpload={() => {}} />
    );
    expect(container.querySelector('[data-slot="kb-hub-empty-state"]')).toBeInTheDocument();
    expect(container.querySelector('[data-slot="kb-hub-empty-upload-cta"]')).toBeInTheDocument();
  });
});
