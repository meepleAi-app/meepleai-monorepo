import { describe, expect, it, vi } from 'vitest';
import { render, screen, fireEvent, waitFor } from '@testing-library/react';
import { axe, toHaveNoViolations } from 'jest-axe';
import { QueryClient, QueryClientProvider } from '@tanstack/react-query';
import type { JSX, ReactNode } from 'react';

import { KbEditorDesktop } from '../KbEditorDesktop';
import { api } from '@/lib/api';
import type { UserKbDocDto } from '@/lib/api/schemas/kb-docs.schemas';

expect.extend(toHaveNoViolations);

function makeWrapper(qc: QueryClient) {
  return function Wrapper({ children }: { children: ReactNode }): JSX.Element {
    return <QueryClientProvider client={qc}>{children}</QueryClientProvider>;
  };
}

const DOC_FIXTURE: UserKbDocDto = {
  id: '00000000-0000-0000-0000-000000000001',
  gameId: null,
  gameName: null,
  fileName: 'azul.pdf',
  processingState: 'Ready',
  pageCount: 24,
  processedAt: '2026-05-31T00:00:00Z',
  uploadedAt: '2026-05-31T00:00:00Z',
  updatedAt: '2026-05-31T00:00:00Z',
  title: 'Old Title',
  tags: ['family'],
  updatedBy: null,
};

const LABELS = {
  heading: 'Modifica metadati',
  titleLabel: 'Titolo',
  documentTypeLabel: 'Tipo documento',
  languageLabel: 'Lingua',
  tagsLabel: 'Tag',
  saveLabel: 'Salva',
  cancelLabel: 'Annulla',
  notFoundError: 'Documento non trovato',
  genericError: 'Errore generico, riprova.',
  documentTypeOptions: { Rulebook: 'Regolamento' },
  languageOptions: { it: 'Italiano', en: 'English' },
};

describe('KbEditorDesktop (Phase 3 #1737)', () => {
  it('renders form pre-filled with doc fields', () => {
    const qc = new QueryClient();
    render(<KbEditorDesktop doc={DOC_FIXTURE} onClose={vi.fn()} labels={LABELS} />, {
      wrapper: makeWrapper(qc),
    });
    expect((screen.getByLabelText('Titolo') as HTMLInputElement).value).toBe('Old Title');
  });

  // S4: PATCH success
  it('S4: submits PATCH with changed title and closes on success', async () => {
    const spy = vi
      .spyOn(api.kbDocs, 'patchKbDocMetadata')
      .mockResolvedValue({ ...DOC_FIXTURE, title: 'New Title' });
    const onClose = vi.fn();
    const qc = new QueryClient();
    render(<KbEditorDesktop doc={DOC_FIXTURE} onClose={onClose} labels={LABELS} />, {
      wrapper: makeWrapper(qc),
    });
    const titleInput = screen.getByLabelText('Titolo') as HTMLInputElement;
    fireEvent.change(titleInput, { target: { value: 'New Title' } });
    fireEvent.click(screen.getByRole('button', { name: /salva/i }));
    await waitFor(() => expect(spy).toHaveBeenCalled());
    expect(spy).toHaveBeenCalledWith(
      DOC_FIXTURE.id,
      expect.objectContaining({ title: 'New Title' })
    );
    await waitFor(() => expect(onClose).toHaveBeenCalled());
    spy.mockRestore();
  });

  // S5: PATCH 404
  it('S5: shows generic notFoundError on 404 (anti-info-leak)', async () => {
    const spy = vi.spyOn(api.kbDocs, 'patchKbDocMetadata').mockRejectedValue(new Error('HTTP 404'));
    const qc = new QueryClient({ defaultOptions: { mutations: { retry: false } } });
    render(<KbEditorDesktop doc={DOC_FIXTURE} onClose={vi.fn()} labels={LABELS} />, {
      wrapper: makeWrapper(qc),
    });
    fireEvent.change(screen.getByLabelText('Titolo'), { target: { value: 'X' } });
    fireEvent.click(screen.getByRole('button', { name: /salva/i }));
    await waitFor(() => expect(screen.getByText('Documento non trovato')).toBeInTheDocument());
    spy.mockRestore();
  });

  // S6: PATCH 422 field errors
  it('S6: shows inline field error on 422 (FluentValidation envelope)', async () => {
    const validationError = Object.assign(new Error('HTTP 422'), {
      status: 422,
      fieldErrors: { title: 'Title must not exceed 200 characters.' },
    });
    const spy = vi.spyOn(api.kbDocs, 'patchKbDocMetadata').mockRejectedValue(validationError);
    const qc = new QueryClient({ defaultOptions: { mutations: { retry: false } } });
    render(<KbEditorDesktop doc={DOC_FIXTURE} onClose={vi.fn()} labels={LABELS} />, {
      wrapper: makeWrapper(qc),
    });
    fireEvent.change(screen.getByLabelText('Titolo'), { target: { value: 'X' } });
    fireEvent.click(screen.getByRole('button', { name: /salva/i }));
    await waitFor(() => expect(screen.getByText(/title must not exceed 200/i)).toBeInTheDocument());
    // aria-describedby links input to error
    expect(screen.getByLabelText('Titolo')).toHaveAttribute('aria-describedby');
    spy.mockRestore();
  });

  it('omits unchanged fields from PATCH body (partial update D-4 #1687)', async () => {
    const spy = vi.spyOn(api.kbDocs, 'patchKbDocMetadata').mockResolvedValue(DOC_FIXTURE);
    const qc = new QueryClient();
    render(<KbEditorDesktop doc={DOC_FIXTURE} onClose={vi.fn()} labels={LABELS} />, {
      wrapper: makeWrapper(qc),
    });
    // Don't change anything, just click Save.
    fireEvent.click(screen.getByRole('button', { name: /salva/i }));
    await waitFor(() => expect(spy).toHaveBeenCalled());
    const body = spy.mock.calls[0]?.[1];
    expect(body).toEqual({}); // no-op patch
    spy.mockRestore();
  });

  it('cancel button calls onClose without submitting', () => {
    const onClose = vi.fn();
    const spy = vi.spyOn(api.kbDocs, 'patchKbDocMetadata');
    const qc = new QueryClient();
    render(<KbEditorDesktop doc={DOC_FIXTURE} onClose={onClose} labels={LABELS} />, {
      wrapper: makeWrapper(qc),
    });
    fireEvent.click(screen.getByRole('button', { name: /annulla/i }));
    expect(onClose).toHaveBeenCalled();
    expect(spy).not.toHaveBeenCalled();
    spy.mockRestore();
  });

  it('a11y: no jest-axe violations', async () => {
    const qc = new QueryClient();
    const { container } = render(
      <KbEditorDesktop doc={DOC_FIXTURE} onClose={vi.fn()} labels={LABELS} />,
      { wrapper: makeWrapper(qc) }
    );
    const results = await axe(container);
    expect(results).toHaveNoViolations();
  });
});
