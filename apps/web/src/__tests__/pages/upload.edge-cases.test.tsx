/**
 * Upload Page - Edge Cases & Error Handling Tests
 *
 * BDD Scenarios:
 * - Authorization and authentication edge cases
 * - Parse failure scenarios
 * - Unauthenticated user handling
 */

import { fireEvent, render, screen, waitFor } from '@testing-library/react';
import UploadPage from '../../pages/upload';
import {
  setupUploadMocks,
  createAuthMock,
  createGameMock
} from '../../pages/../__tests__/fixtures/upload-mocks';

describe('UploadPage - Edge Cases', () => {
  const originalFetch = global.fetch;

  beforeEach(() => {
    jest.clearAllMocks();
  });

  afterAll(() => {
    global.fetch = originalFetch;
  });

  afterEach(() => {
    jest.useRealTimers();
  });

  describe('Given user does not have required role', () => {
    describe('When user has Viewer role', () => {
      it('Then access is blocked with role requirement message', async () => {
        const mockFetch = setupUploadMocks({
          auth: createAuthMock({
            userId: 'viewer-1',
            email: 'viewer@example.com',
            role: 'Viewer',
            displayName: 'Viewer User'
          })
        });

        global.fetch = mockFetch as unknown as typeof fetch;

        render(<UploadPage />);

        await waitFor(() =>
          expect(screen.getByText(/You need an Editor or Admin role to manage PDF ingestion/i)).toBeInTheDocument()
        );
        expect(screen.getByRole('link', { name: /Back to Home/i })).toBeInTheDocument();
      });
    });
  });

  describe('Given user is not authenticated', () => {
    describe('When page initializes', () => {
      it('Then shows login requirement message', async () => {
        const mockFetch = setupUploadMocks({ auth: null });

        global.fetch = mockFetch as unknown as typeof fetch;

        render(<UploadPage />);

        await waitFor(() => {
          expect(screen.getByText(/You need to be logged in to manage games/i)).toBeInTheDocument();
        });
      });
    });

    describe('When trying to create game without authentication', () => {
      it('Then shows login requirement message', async () => {
        const mockFetch = setupUploadMocks({ auth: null });

        global.fetch = mockFetch as unknown as typeof fetch;

        render(<UploadPage />);

        await waitFor(() => {
          expect(screen.getByText(/You need to be logged in to manage games/i)).toBeInTheDocument();
        });
      });
    });
  });

  describe('Given PDF processing fails', () => {
    describe('When polling reports failed status', () => {
      it('Then shows parse failure message with error details', async () => {
        jest.useFakeTimers();

        try {
          const mockFetch = setupUploadMocks({
            auth: createAuthMock({ userId: 'user-5', role: 'Admin' }),
            games: [createGameMock({ id: 'game-1', name: 'Terraforming Mars' })],
            pdfs: { pdfs: [] },
            uploadResponse: { documentId: 'pdf-123', fileName: 'rules.pdf' },
            pdfStatusSequence: [
              { processingStatus: 'failed', processingError: 'OCR error' }
            ]
          });

          global.fetch = mockFetch as unknown as typeof fetch;

          render(<UploadPage />);

          await waitFor(() => expect(screen.getByLabelText(/Existing games/i)).toBeInTheDocument());

          fireEvent.click(screen.getByRole('button', { name: /Confirm selection/i }));

          const fileInput = screen.getByLabelText(/PDF File/i) as HTMLInputElement;
          const file = new File(['pdf'], 'rules.pdf', { type: 'application/pdf' });
          fireEvent.change(fileInput, { target: { files: [file] } });

          const uploadButton = screen.getByRole('button', { name: /Upload & Continue/i });
          await waitFor(() => expect(uploadButton).not.toBeDisabled());
          fireEvent.click(uploadButton);

          await waitFor(() => expect(screen.getByText(/Processing status/i)).toBeInTheDocument());

          await waitFor(() =>
            expect(screen.getByText(/❌ Parse failed: OCR error/i)).toBeInTheDocument()
          );
          expect(screen.getByText(/Processing error: OCR error/i)).toBeInTheDocument();
        } finally {
          jest.useRealTimers();
        }
      });
    });
  });
});
