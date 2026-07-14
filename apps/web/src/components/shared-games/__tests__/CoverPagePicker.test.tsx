/**
 * CoverPagePicker Component Tests
 *
 * Task 8 (Game Cover-da-PDF plan): proposes a PDF page as a game cover via
 * POST /api/v1/games/{gameId}/cover/propose-from-pdf, invoking onProposed
 * with the resulting shareRequestId. Also covers the non-blocking 503
 * ("cover_render_unavailable") failure mode from the backend render step.
 */

import { screen, waitFor } from '@testing-library/react';
import userEvent from '@testing-library/user-event';
import { describe, it, expect, vi, beforeEach } from 'vitest';
import { renderWithQuery } from '@/__tests__/utils/query-test-utils';
import { CoverPagePicker } from '../CoverPagePicker';

const proposeMock = vi.fn();
vi.mock('@/lib/api', () => ({
  api: {
    sharedGames: {
      getPdfPageImageUrl: (id: string, p: number) => `/img/${id}/${p}`,
      proposeCoverFromPdf: (...a: unknown[]) => proposeMock(...a),
    },
  },
}));

beforeEach(() => {
  vi.clearAllMocks();
  proposeMock.mockResolvedValue({ shareRequestId: 'sr-1' });
});

describe('CoverPagePicker', () => {
  it('proposes the selected page and calls onProposed', async () => {
    const onProposed = vi.fn();
    const user = userEvent.setup();
    renderWithQuery(<CoverPagePicker gameId="g-1" pdfDocumentId="pdf-1" onProposed={onProposed} />);

    const pageInput = screen.getByLabelText(/pagina/i);
    await user.clear(pageInput);
    await user.type(pageInput, '3');
    await user.click(screen.getByRole('button', { name: /proponi cover/i }));

    await waitFor(() => expect(proposeMock).toHaveBeenCalledWith('g-1', 'pdf-1', 3));
    await waitFor(() => expect(onProposed).toHaveBeenCalledWith('sr-1'));
  });

  it('shows a non-blocking message when cover rendering is unavailable (503)', async () => {
    const onProposed = vi.fn();
    const user = userEvent.setup();

    const error = new Error('cover_render_unavailable') as Error & {
      statusCode: number;
      code: string;
    };
    error.statusCode = 503;
    error.code = 'cover_render_unavailable';
    proposeMock.mockRejectedValueOnce(error);

    renderWithQuery(<CoverPagePicker gameId="g-1" pdfDocumentId="pdf-1" onProposed={onProposed} />);

    await user.click(screen.getByRole('button', { name: /proponi cover/i }));

    const alert = await screen.findByRole('alert');
    expect(alert).toHaveTextContent(/anteprima cover non disponibile/i);
    expect(onProposed).not.toHaveBeenCalled();
  });
});
