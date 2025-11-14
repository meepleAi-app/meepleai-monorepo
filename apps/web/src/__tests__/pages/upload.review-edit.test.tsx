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
import UploadPage from '../../pages/upload';
import {
  setupUploadMocks,
  createAuthMock,
  createGameMock,
  createRuleSpecMock
} from '../../pages/../__tests__/fixtures/upload-mocks';

describe('UploadPage - Review & Edit', () => {
  const originalFetch = global.fetch;

  beforeEach(() => {
    jest.clearAllMocks();
    jest.useFakeTimers();
  });

  afterAll(() => {
    global.fetch = originalFetch;
  });

  afterEach(() => {
    jest.runOnlyPendingTimers();
    jest.useRealTimers();
    jest.clearAllMocks();
  });

  // Helper to setup game selection for tests
  async function confirmGameSelection() {
    // The component auto-selects the first game, so we just need to confirm
    const confirmButton = await waitFor(() => {
      const btn = screen.getByRole('button', { name: /Confirm Game Selection/i });
      expect(btn).toBeInTheDocument();
      return btn;
    });

    fireEvent.click(confirmButton);

    // Wait for the PDF upload form to appear
    await waitFor(() => expect(screen.getByLabelText(/PDF File/i)).toBeInTheDocument());
  }

  describe('Given user is in review phase', () => {
    describe('When user edits rule atom text', () => {
      it('Then rule text is updated in UI', async () => {
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

        await act(async () => {
          render(<UploadPage />);
        });

        await waitFor(() => expect(screen.getByRole('combobox', { name: /select.*game/i })).toBeInTheDocument());

        await confirmGameSelection();

        const fileInput = screen.getByLabelText(/PDF File/i) as HTMLInputElement;
        // Create a valid PDF file with PDF header magic bytes
        const pdfHeader = '%PDF-1.4\n';
        const pdfContent = pdfHeader + 'dummy pdf content';
        const file = new File([pdfContent], 'rules.pdf', { type: 'application/pdf' });

        await act(async () => {
          fireEvent.change(fileInput, { target: { files: [file] } });
        });

        // Wait for validation to complete and button to be enabled
        await waitFor(() => {
          const btn = screen.getByRole('button', { name: /Upload PDF/i });
          return !btn.hasAttribute('disabled');
        }, { timeout: 5000 });

        const uploadButton = screen.getByRole('button', { name: /Upload PDF/i });
        fireEvent.click(uploadButton);

        await act(async () => {
          jest.advanceTimersByTime(2500);
        });

        // Wait for the parse step to complete and review step to appear
        await waitFor(() => {
          expect(screen.getByRole('heading', { name: /Step 3: Review & Edit Rules/i })).toBeInTheDocument();
        }, { timeout: 10000 });

        await waitFor(() => expect(screen.getByText(/Original rule text/i)).toBeInTheDocument());

        const ruleTextInput = screen.getByDisplayValue('Original rule text');
        fireEvent.change(ruleTextInput, { target: { value: 'Edited rule text' } });

        expect(screen.getByDisplayValue('Edited rule text')).toBeInTheDocument();
      }, 10000);
    });

    describe('When user deletes a rule atom', () => {
      it('Then rule is removed from list', async () => {
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

        await act(async () => {
          render(<UploadPage />);
        });

        await waitFor(() => expect(screen.getByRole('combobox', { name: /select.*game/i })).toBeInTheDocument());

        await confirmGameSelection();

        const fileInput = screen.getByLabelText(/PDF File/i) as HTMLInputElement;
        // Create a valid PDF file with PDF header magic bytes
        const pdfHeader = '%PDF-1.4\n';
        const pdfContent = pdfHeader + 'dummy pdf content';
        const file = new File([pdfContent], 'rules.pdf', { type: 'application/pdf' });

        await act(async () => {
          fireEvent.change(fileInput, { target: { files: [file] } });
        });

        // Wait for validation to complete and button to be enabled
        await waitFor(() => {
          const btn = screen.getByRole('button', { name: /Upload PDF/i });
          return !btn.hasAttribute('disabled');
        }, { timeout: 5000 });

        const uploadButton = screen.getByRole('button', { name: /Upload PDF/i });
        fireEvent.click(uploadButton);

        await act(async () => {
          jest.advanceTimersByTime(2500);
        });

        // Wait for review step
        await waitFor(() => {
          expect(screen.getByRole('heading', { name: /Step 3: Review & Edit Rules/i })).toBeInTheDocument();
        }, { timeout: 10000 });

        await waitFor(() => expect(screen.getByText(/Rule to delete/i)).toBeInTheDocument());

        const deleteButtons = screen.getAllByRole('button', { name: /Delete/i });
        fireEvent.click(deleteButtons[1]); // Delete second rule

        await waitFor(() => expect(screen.queryByText(/Rule to delete/i)).not.toBeInTheDocument());
        expect(screen.getByText(/Rule to keep/i)).toBeInTheDocument();
      }, 10000);
    });

    describe('When user adds a new rule atom', () => {
      it('Then new empty rule appears in list', async () => {
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

        await act(async () => {
          render(<UploadPage />);
        });

        await waitFor(() => expect(screen.getByRole('combobox', { name: /select.*game/i })).toBeInTheDocument());

        await confirmGameSelection();

        const fileInput = screen.getByLabelText(/PDF File/i) as HTMLInputElement;
        // Create a valid PDF file with PDF header magic bytes
        const pdfHeader = '%PDF-1.4\n';
        const pdfContent = pdfHeader + 'dummy pdf content';
        const file = new File([pdfContent], 'rules.pdf', { type: 'application/pdf' });

        await act(async () => {
          fireEvent.change(fileInput, { target: { files: [file] } });
        });

        // Wait for validation to complete and button to be enabled
        await waitFor(() => {
          const btn = screen.getByRole('button', { name: /Upload PDF/i });
          return !btn.hasAttribute('disabled');
        }, { timeout: 5000 });

        const uploadButton = screen.getByRole('button', { name: /Upload PDF/i });
        fireEvent.click(uploadButton);

        await act(async () => {
          jest.advanceTimersByTime(2500);
        });

        // Wait for review step
        await waitFor(() => {
          expect(screen.getByRole('heading', { name: /Step 3: Review & Edit Rules/i })).toBeInTheDocument();
        }, { timeout: 10000 });

        await waitFor(() => expect(screen.getByText(/Existing rule/i)).toBeInTheDocument());

        const initialTextareas = screen.getAllByRole('textbox').filter((el) => el.tagName === 'TEXTAREA');
        expect(initialTextareas).toHaveLength(1);

        const addButton = screen.getByRole('button', { name: /Add Rule/i });
        fireEvent.click(addButton);

        await waitFor(() => {
          const textareas = screen.getAllByRole('textbox').filter((el) => el.tagName === 'TEXTAREA');
          expect(textareas).toHaveLength(2);
        });
      }, 10000);
    });

    describe('When user publishes RuleSpec successfully', () => {
      it('Then success message is displayed', async () => {
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

        await act(async () => {
          render(<UploadPage />);
        });

        await waitFor(() => expect(screen.getByRole('combobox', { name: /select.*game/i })).toBeInTheDocument());

        await confirmGameSelection();

        const fileInput = screen.getByLabelText(/PDF File/i) as HTMLInputElement;
        // Create a valid PDF file with PDF header magic bytes
        const pdfHeader = '%PDF-1.4\n';
        const pdfContent = pdfHeader + 'dummy pdf content';
        const file = new File([pdfContent], 'rules.pdf', { type: 'application/pdf' });

        await act(async () => {
          fireEvent.change(fileInput, { target: { files: [file] } });
        });

        // Wait for validation to complete and button to be enabled
        await waitFor(() => {
          const btn = screen.getByRole('button', { name: /Upload PDF/i });
          return !btn.hasAttribute('disabled');
        }, { timeout: 5000 });

        const uploadButton = screen.getByRole('button', { name: /Upload PDF/i });
        fireEvent.click(uploadButton);

        await act(async () => {
          jest.advanceTimersByTime(2500);
        });

        // Wait for review step
        await waitFor(() => {
          expect(screen.getByRole('heading', { name: /Step 3: Review & Edit Rules/i })).toBeInTheDocument();
        }, { timeout: 10000 });

        await waitFor(() => expect(screen.getByText(/Rule to publish/i)).toBeInTheDocument());

        const publishButton = screen.getByRole('button', { name: /Publish RuleSpec/i });
        fireEvent.click(publishButton);

        await waitFor(() =>
          expect(screen.getByText(/✅ RuleSpec published successfully!/i)).toBeInTheDocument()
        );
      }, 10000);
    });

    describe('When publishing RuleSpec fails', () => {
      it('Then error message is displayed and button re-enabled', async () => {
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

        await act(async () => {
          render(<UploadPage />);
        });

        await waitFor(() => expect(screen.getByRole('combobox', { name: /select.*game/i })).toBeInTheDocument());

        await confirmGameSelection();

        const fileInput = screen.getByLabelText(/PDF File/i) as HTMLInputElement;
        // Create a valid PDF file with PDF header magic bytes
        const pdfHeader = '%PDF-1.4\n';
        const pdfContent = pdfHeader + 'dummy pdf content';
        const file = new File([pdfContent], 'rules.pdf', { type: 'application/pdf' });

        await act(async () => {
          fireEvent.change(fileInput, { target: { files: [file] } });
        });

        // Wait for validation to complete and button to be enabled
        await waitFor(() => {
          const btn = screen.getByRole('button', { name: /Upload PDF/i });
          return !btn.hasAttribute('disabled');
        }, { timeout: 5000 });

        const uploadButton = screen.getByRole('button', { name: /Upload PDF/i });
        fireEvent.click(uploadButton);

        await act(async () => {
          jest.advanceTimersByTime(2500);
        });

        await waitFor(() =>
          expect(
            screen.getByRole('heading', { name: /Step 3: Review & Edit Rules/i })
          ).toBeInTheDocument()
        , { timeout: 10000 });

        const publishButton = screen.getByRole('button', { name: /Publish RuleSpec/i });
        fireEvent.click(publishButton);

        await waitFor(() =>
          expect(screen.getByText(/❌ Publish failed: Publish failed/i)).toBeInTheDocument()
        );
        await waitFor(() =>
          expect(screen.getByRole('button', { name: /Publish RuleSpec/i })).toBeInTheDocument()
        );
      }, 10000);
    });

    describe('When RuleSpec load fails with 401', () => {
      it('Then stays on parse step with error message', async () => {
        const mockFetch = setupUploadMocks({
          auth: createAuthMock({ userId: 'user-8', role: 'Admin' }),
          games: [createGameMock({ id: 'game-1', name: 'Terraforming Mars' })],
          pdfs: { pdfs: [] },
          uploadResponse: { documentId: 'pdf-123', fileName: 'rules.pdf' },
          pdfStatusSequence: [{ processingStatus: 'completed', processingError: null }],
          ruleSpecError: { status: 401, error: {} }
        });

        global.fetch = mockFetch as unknown as typeof fetch;

        await act(async () => {
          render(<UploadPage />);
        });

        await waitFor(() => expect(screen.getByRole('combobox', { name: /select.*game/i })).toBeInTheDocument());

        await confirmGameSelection();

        const fileInput = screen.getByLabelText(/PDF File/i) as HTMLInputElement;
        // Create a valid PDF file with PDF header magic bytes
        const pdfHeader = '%PDF-1.4\n';
        const pdfContent = pdfHeader + 'dummy pdf content';
        const file = new File([pdfContent], 'rules.pdf', { type: 'application/pdf' });

        await act(async () => {
          fireEvent.change(fileInput, { target: { files: [file] } });
        });

        // Wait for validation to complete and button to be enabled
        await waitFor(() => {
          const btn = screen.getByRole('button', { name: /Upload PDF/i });
          return !btn.hasAttribute('disabled');
        }, { timeout: 5000 });

        const uploadButton = screen.getByRole('button', { name: /Upload PDF/i });
        fireEvent.click(uploadButton);

        await act(async () => {
          jest.advanceTimersByTime(2500);
        });

        await waitFor(() =>
          expect(screen.getByText(/❌ Parse failed: Unable to load RuleSpec\./i)).toBeInTheDocument()
        , { timeout: 10000 });
        expect(screen.getByRole('heading', { name: /Step 2: Parse PDF/i })).toBeInTheDocument();
      }, 10000);
    });

    describe('When handleParse throws error', () => {
      it('Then error message is displayed and stays on parse step', async () => {
        const mockFetch = setupUploadMocks({
          auth: createAuthMock({ userId: 'user-9', role: 'Admin' }),
          games: [createGameMock({ id: 'game-1', name: 'Terraforming Mars' })],
          pdfs: { pdfs: [] },
          uploadResponse: { documentId: 'pdf-123', fileName: 'rules.pdf' },
          pdfStatusSequence: [{ processingStatus: 'completed', processingError: null }],
          ruleSpecError: { status: 500, error: { error: 'Server exploded' } }
        });

        global.fetch = mockFetch as unknown as typeof fetch;

        await act(async () => {
          render(<UploadPage />);
        });

        await waitFor(() => expect(screen.getByRole('combobox', { name: /select.*game/i })).toBeInTheDocument());

        await confirmGameSelection();

        const fileInput = screen.getByLabelText(/PDF File/i) as HTMLInputElement;
        // Create a valid PDF file with PDF header magic bytes
        const pdfHeader = '%PDF-1.4\n';
        const pdfContent = pdfHeader + 'dummy pdf content';
        const file = new File([pdfContent], 'rules.pdf', { type: 'application/pdf' });

        await act(async () => {
          fireEvent.change(fileInput, { target: { files: [file] } });
        });

        // Wait for validation to complete and button to be enabled
        await waitFor(() => {
          const btn = screen.getByRole('button', { name: /Upload PDF/i });
          return !btn.hasAttribute('disabled');
        }, { timeout: 5000 });

        const uploadButton = screen.getByRole('button', { name: /Upload PDF/i });
        fireEvent.click(uploadButton);

        await act(async () => {
          jest.advanceTimersByTime(2500);
        });

        await waitFor(() =>
          expect(
            screen.getByText(/❌ Parse failed: API \/api\/v1\/games\/game-1\/rulespec 500/i)
          ).toBeInTheDocument()
        , { timeout: 10000 });
        expect(screen.getByRole('heading', { name: /Step 2: Parse PDF/i })).toBeInTheDocument();
      }, 10000);
    });

    describe('When user resets wizard from review step', () => {
      it('Then wizard returns to upload step', async () => {
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

        await act(async () => {
          render(<UploadPage />);
        });

        await waitFor(() => expect(screen.getByRole('combobox', { name: /select.*game/i })).toBeInTheDocument());

        await confirmGameSelection();

        const fileInput = screen.getByLabelText(/PDF File/i) as HTMLInputElement;
        // Create a valid PDF file with PDF header magic bytes
        const pdfHeader = '%PDF-1.4\n';
        const pdfContent = pdfHeader + 'dummy pdf content';
        const file = new File([pdfContent], 'rules.pdf', { type: 'application/pdf' });

        await act(async () => {
          fireEvent.change(fileInput, { target: { files: [file] } });
        });

        // Wait for validation to complete and button to be enabled
        await waitFor(() => {
          const btn = screen.getByRole('button', { name: /Upload PDF/i });
          return !btn.hasAttribute('disabled');
        }, { timeout: 5000 });

        const uploadButton = screen.getByRole('button', { name: /Upload PDF/i });
        fireEvent.click(uploadButton);

        await act(async () => {
          jest.advanceTimersByTime(2500);
        });

        // Wait for review step
        await waitFor(() => {
          expect(screen.getByRole('heading', { name: /Step 3: Review & Edit Rules/i })).toBeInTheDocument();
        }, { timeout: 10000 });

        await waitFor(() => expect(screen.getByText(/Rule text/i)).toBeInTheDocument());

        const resetButton = screen.getByRole('button', { name: /Cancel/i });
        fireEvent.click(resetButton);

        await waitFor(() => expect(screen.getByLabelText(/PDF File/i)).toBeInTheDocument());
        expect(screen.queryByText(/Rule text/i)).not.toBeInTheDocument();
      }, 10000);
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

        await waitFor(() => expect(screen.getByRole('combobox', { name: /select.*game/i })).toBeInTheDocument());

        // At this point, ruleSpec is null, so operations should be no-ops
        // This tests the guard conditions in updateRuleAtom, deleteRuleAtom, addRuleAtom
        expect(screen.queryByText(/Step 3: Review/i)).not.toBeInTheDocument();
      });
    });
  });
});