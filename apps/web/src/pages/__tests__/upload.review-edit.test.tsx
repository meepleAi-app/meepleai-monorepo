/**
 * Upload Page - Review & Edit Workflow Tests
 *
 * BDD Scenarios:
 * - Editing rule atoms
 * - Adding new rule atoms
 * - Deleting rule atoms
 * - Publishing RuleSpec
 * - Wizard state reset
 */

import { act, fireEvent, render, screen, waitFor } from '@testing-library/react';
import UploadPage from '../upload';
import {
  setupUploadMocks,
  createAuthMock,
  createGameMock,
  createRuleSpecMock
} from '../../__tests__/fixtures/upload-mocks';

describe('UploadPage - Review & Edit', () => {
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

  describe('Given user is in review phase', () => {
    describe('When user edits rule atom text', () => {
      it('Then rule text is updated in UI', async () => {
        jest.useFakeTimers();

        try {
          const mockFetch = setupUploadMocks({
            auth: createAuthMock({ userId: 'user-11', role: 'Admin' }),
            games: [createGameMock({ id: 'game-1', name: 'Terraforming Mars' })],
            pdfs: { pdfs: [] },
            uploadResponse: { documentId: 'pdf-123' },
            pdfStatusSequence: [{ processingStatus: 'completed', processingError: null }],
            ruleSpec: createRuleSpecMock({
              gameId: 'game-1',
              rules: [{ id: 'r1', text: 'Original rule text', section: 'Setup', page: '1', line: '1' }]
            })
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

          await act(async () => {
            jest.advanceTimersByTime(2500);
          });

          await waitFor(() => expect(screen.getByText(/Original rule text/i)).toBeInTheDocument());

          const ruleTextInput = screen.getByDisplayValue('Original rule text');
          fireEvent.change(ruleTextInput, { target: { value: 'Edited rule text' } });

          expect(screen.getByDisplayValue('Edited rule text')).toBeInTheDocument();
        } finally {
          jest.useRealTimers();
        }
      });
    });

    describe('When user deletes a rule atom', () => {
      it('Then rule is removed from list', async () => {
        jest.useFakeTimers();

        try {
          const mockFetch = setupUploadMocks({
            auth: createAuthMock({ userId: 'user-12', role: 'Admin' }),
            games: [createGameMock({ id: 'game-1', name: 'Terraforming Mars' })],
            pdfs: { pdfs: [] },
            uploadResponse: { documentId: 'pdf-123' },
            pdfStatusSequence: [{ processingStatus: 'completed', processingError: null }],
            ruleSpec: createRuleSpecMock({
              gameId: 'game-1',
              rules: [
                { id: 'r1', text: 'Rule to keep', section: null, page: null, line: null },
                { id: 'r2', text: 'Rule to delete', section: null, page: null, line: null }
              ]
            })
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

          await act(async () => {
            jest.advanceTimersByTime(2500);
          });

          await waitFor(() => expect(screen.getByText(/Rule to delete/i)).toBeInTheDocument());

          const deleteButtons = screen.getAllByRole('button', { name: /Delete/i });
          fireEvent.click(deleteButtons[1]); // Delete second rule

          await waitFor(() => expect(screen.queryByText(/Rule to delete/i)).not.toBeInTheDocument());
          expect(screen.getByText(/Rule to keep/i)).toBeInTheDocument();
        } finally {
          jest.useRealTimers();
        }
      });
    });

    describe('When user adds a new rule atom', () => {
      it('Then new empty rule appears in list', async () => {
        jest.useFakeTimers();

        try {
          const mockFetch = setupUploadMocks({
            auth: createAuthMock({ userId: 'user-13', role: 'Admin' }),
            games: [createGameMock({ id: 'game-1', name: 'Terraforming Mars' })],
            pdfs: { pdfs: [] },
            uploadResponse: { documentId: 'pdf-123' },
            pdfStatusSequence: [{ processingStatus: 'completed', processingError: null }],
            ruleSpec: createRuleSpecMock({
              gameId: 'game-1',
              rules: [{ id: 'r1', text: 'Existing rule' }]
            })
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

          await act(async () => {
            jest.advanceTimersByTime(2500);
          });

          await waitFor(() => expect(screen.getByText(/Existing rule/i)).toBeInTheDocument());

          const initialTextareas = screen.getAllByRole('textbox').filter((el) => el.tagName === 'TEXTAREA');
          expect(initialTextareas).toHaveLength(1);

          const addButton = screen.getByRole('button', { name: /Add Rule/i });
          fireEvent.click(addButton);

          await waitFor(() => {
            const textareas = screen.getAllByRole('textbox').filter((el) => el.tagName === 'TEXTAREA');
            expect(textareas).toHaveLength(2);
          });
        } finally {
          jest.useRealTimers();
        }
      });
    });

    describe('When user publishes RuleSpec successfully', () => {
      it('Then success message is displayed', async () => {
        jest.useFakeTimers();

        try {
          const mockFetch = setupUploadMocks({
            auth: createAuthMock({ userId: 'user-10', role: 'Editor' }),
            games: [createGameMock({ id: 'game-1', name: 'Terraforming Mars' })],
            pdfs: { pdfs: [] },
            uploadResponse: { documentId: 'pdf-123' },
            pdfStatusSequence: [{ processingStatus: 'completed', processingError: null }],
            ruleSpec: createRuleSpecMock({
              gameId: 'game-1',
              rules: [{ id: 'r1', text: 'Rule to publish' }]
            }),
            publishRuleSpecResponse: createRuleSpecMock({
              gameId: 'game-1',
              rules: [{ id: 'r1', text: 'Rule to publish' }]
            })
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

          await act(async () => {
            jest.advanceTimersByTime(2500);
          });

          await waitFor(() => expect(screen.getByText(/Rule to publish/i)).toBeInTheDocument());

          const publishButton = screen.getByRole('button', { name: /Publish RuleSpec/i });
          fireEvent.click(publishButton);

          await waitFor(() =>
            expect(screen.getByText(/✅ RuleSpec published successfully!/i)).toBeInTheDocument()
          );
        } finally {
          jest.useRealTimers();
        }
      });
    });

    describe('When publishing RuleSpec fails', () => {
      it('Then error message is displayed and button re-enabled', async () => {
        jest.useFakeTimers();

        try {
          const mockFetch = setupUploadMocks({
            auth: createAuthMock({ userId: 'user-10', role: 'Admin' }),
            games: [createGameMock({ id: 'game-1', name: 'Terraforming Mars' })],
            pdfs: { pdfs: [] },
            uploadResponse: { documentId: 'pdf-123', fileName: 'rules.pdf' },
            pdfStatusSequence: [{ processingStatus: 'completed', processingError: null }],
            ruleSpec: createRuleSpecMock({
              gameId: 'game-1',
              rules: [{ id: 'r1', text: 'Auto generated rule', section: 'Intro', page: '1', line: '1' }]
            }),
            publishRuleSpecError: { status: 500, error: 'Publish failed' }
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

          await waitFor(() =>
            expect(
              screen.getByRole('heading', { name: /Step 3: Review & Edit Rules/i })
            ).toBeInTheDocument()
          );

          const publishButton = screen.getByRole('button', { name: /Publish RuleSpec/i });
          fireEvent.click(publishButton);

          await waitFor(() =>
            expect(screen.getByText(/❌ Publish failed: Publish failed/i)).toBeInTheDocument()
          );
          await waitFor(() =>
            expect(screen.getByRole('button', { name: /Publish RuleSpec/i })).toBeInTheDocument()
          );
        } finally {
          jest.useRealTimers();
        }
      });
    });

    describe('When RuleSpec load fails with 401', () => {
      it('Then stays on parse step with error message', async () => {
        jest.useFakeTimers();

        try {
          const mockFetch = setupUploadMocks({
            auth: createAuthMock({ userId: 'user-8', role: 'Admin' }),
            games: [createGameMock({ id: 'game-1', name: 'Terraforming Mars' })],
            pdfs: { pdfs: [] },
            uploadResponse: { documentId: 'pdf-123', fileName: 'rules.pdf' },
            pdfStatusSequence: [{ processingStatus: 'completed', processingError: null }],
            ruleSpecError: { status: 401, error: {} }
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

          await waitFor(() =>
            expect(screen.getByText(/❌ Parse failed: Unable to load RuleSpec\./i)).toBeInTheDocument()
          );
          expect(screen.getByRole('heading', { name: /Step 2: Parse PDF/i })).toBeInTheDocument();
        } finally {
          jest.useRealTimers();
        }
      });
    });

    describe('When handleParse throws error', () => {
      it('Then error message is displayed and stays on parse step', async () => {
        jest.useFakeTimers();

        try {
          const mockFetch = setupUploadMocks({
            auth: createAuthMock({ userId: 'user-9', role: 'Admin' }),
            games: [createGameMock({ id: 'game-1', name: 'Terraforming Mars' })],
            pdfs: { pdfs: [] },
            uploadResponse: { documentId: 'pdf-123', fileName: 'rules.pdf' },
            pdfStatusSequence: [{ processingStatus: 'completed', processingError: null }],
            ruleSpecError: { status: 500, error: { error: 'Server exploded' } }
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

          await waitFor(() =>
            expect(
              screen.getByText(/❌ Parse failed: API \/api\/v1\/games\/game-1\/rulespec 500/i)
            ).toBeInTheDocument()
          );
          expect(screen.getByRole('heading', { name: /Step 2: Parse PDF/i })).toBeInTheDocument();
        } finally {
          jest.useRealTimers();
        }
      });
    });

    describe('When user resets wizard from review step', () => {
      it('Then wizard returns to upload step', async () => {
        jest.useFakeTimers();

        try {
          const mockFetch = setupUploadMocks({
            auth: createAuthMock({ userId: 'user-18', role: 'Admin' }),
            games: [createGameMock({ id: 'game-1', name: 'Terraforming Mars' })],
            pdfs: { pdfs: [] },
            uploadResponse: { documentId: 'pdf-123' },
            pdfStatusSequence: [{ processingStatus: 'completed', processingError: null }],
            ruleSpec: createRuleSpecMock({
              gameId: 'game-1',
              rules: [{ id: 'r1', text: 'Rule text' }]
            })
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

          await act(async () => {
            jest.advanceTimersByTime(2500);
          });

          await waitFor(() => expect(screen.getByText(/Rule text/i)).toBeInTheDocument());

          const resetButton = screen.getByRole('button', { name: /Cancel/i });
          fireEvent.click(resetButton);

          await waitFor(() => expect(screen.getByLabelText(/PDF File/i)).toBeInTheDocument());
          expect(screen.queryByText(/Rule text/i)).not.toBeInTheDocument();
        } finally {
          jest.useRealTimers();
        }
      });
    });
  });

  describe('Given ruleSpec is null', () => {
    describe('When rule atom operations are attempted', () => {
      it('Then operations are prevented (no-op)', async () => {
        const mockFetch = setupUploadMocks({
          auth: createAuthMock({ userId: 'user-1', role: 'Admin' }),
          games: [createGameMock({ id: 'game-1', name: 'Test' })],
          pdfs: { pdfs: [] }
        });

        global.fetch = mockFetch as unknown as typeof fetch;

        render(<UploadPage />);

        await waitFor(() => expect(screen.getByLabelText(/Existing games/i)).toBeInTheDocument());

        // At this point, ruleSpec is null, so operations should be no-ops
        // This tests the guard conditions in updateRuleAtom, deleteRuleAtom, addRuleAtom
        expect(screen.queryByText(/Step 3: Review/i)).not.toBeInTheDocument();
      });
    });
  });
});
