/**
 * BulkImportJsonUploader Component Tests - Issue #4355 + #4356
 *
 * Test coverage:
 * - Initial render and UI elements
 * - JSON format example display
 * - Textarea input and validation
 * - File upload via input
 * - Client-side validation (format, size, entries)
 * - Preview step (validates then shows BulkImportPreview)
 * - API submission and result display (via preview confirm)
 * - Error handling
 * - Reset/clear functionality
 *
 * Target: >= 85% coverage
 */

import { render, screen, waitFor, fireEvent } from '@testing-library/react';
import userEvent from '@testing-library/user-event';
import { describe, it, expect, vi, beforeEach } from 'vitest';

import { BulkImportJsonUploader } from '@/app/(authenticated)/admin/games/import/bulk/client';

// Mock react-query
const mockMutate = vi.fn();
const mockReset = vi.fn();
const mockMutationState = {
  mutate: mockMutate,
  isPending: false,
  error: null as Error | null,
  reset: mockReset,
};

vi.mock('@tanstack/react-query', () => ({
  useMutation: vi.fn((opts: { onSuccess?: (data: unknown) => void; mutationFn?: unknown }) => {
    // Store the onSuccess callback for triggering in tests
    mockMutate.mockImplementation(() => {
      if (opts.onSuccess) {
        const mockResult = {
          total: 3,
          enqueued: 2,
          skipped: 1,
          failed: 0,
          errors: [
            { bggId: 174430, gameName: 'Gloomhaven', reason: 'Already exists', errorType: 'Duplicate' },
          ],
        };
        opts.onSuccess(mockResult);
      }
    });
    return mockMutationState;
  }),
}));

// Mock API client
vi.mock('@/lib/api', () => ({
  api: {
    admin: {
      bulkImportGames: vi.fn(),
    },
  },
}));

// Helper to set textarea value directly (avoids userEvent.type issues with special chars)
function setTextareaValue(textarea: HTMLTextAreaElement, value: string) {
  fireEvent.change(textarea, { target: { value } });
}

beforeEach(() => {
  vi.clearAllMocks();
  mockMutationState.isPending = false;
  mockMutationState.error = null;
});

describe('BulkImportJsonUploader', () => {
  describe('Initial Render', () => {
    it('renders the component with title and description', () => {
      render(<BulkImportJsonUploader />);

      expect(screen.getByText('Bulk Import Games from JSON')).toBeInTheDocument();
      expect(screen.getByText(/upload a json file or paste json content/i)).toBeInTheDocument();
    });

    it('displays JSON format example', () => {
      render(<BulkImportJsonUploader />);

      const exampleBlock = screen.getByTestId('json-format-example');
      expect(exampleBlock).toBeInTheDocument();
      expect(exampleBlock.textContent).toContain('bggId');
      expect(screen.getByText(/Expected JSON Format/i)).toBeInTheDocument();
    });

    it('renders drop zone with instructions', () => {
      render(<BulkImportJsonUploader />);

      expect(screen.getByTestId('drop-zone')).toBeInTheDocument();
      expect(screen.getByText(/drag & drop a .json file here/i)).toBeInTheDocument();
    });

    it('renders JSON textarea', () => {
      render(<BulkImportJsonUploader />);

      expect(screen.getByTestId('json-textarea')).toBeInTheDocument();
    });

    it('renders preview button disabled when no content', () => {
      render(<BulkImportJsonUploader />);

      const previewBtn = screen.getByTestId('preview-button');
      expect(previewBtn).toBeDisabled();
    });

    it('does not render clear button when no content', () => {
      render(<BulkImportJsonUploader />);

      expect(screen.queryByTestId('reset-button')).not.toBeInTheDocument();
    });
  });

  describe('Textarea Input', () => {
    it('enables preview button when content is entered', () => {
      render(<BulkImportJsonUploader />);

      const textarea = screen.getByTestId('json-textarea') as HTMLTextAreaElement;
      setTextareaValue(textarea, '[{"bggId": 123, "name": "Test Game"}]');

      const previewBtn = screen.getByTestId('preview-button');
      expect(previewBtn).not.toBeDisabled();
    });

    it('shows clear button when content is entered', () => {
      render(<BulkImportJsonUploader />);

      const textarea = screen.getByTestId('json-textarea') as HTMLTextAreaElement;
      setTextareaValue(textarea, 'some content');

      expect(screen.getByTestId('reset-button')).toBeInTheDocument();
    });

    it('clears content when clear button is clicked', async () => {
      const user = userEvent.setup();
      render(<BulkImportJsonUploader />);

      const textarea = screen.getByTestId('json-textarea') as HTMLTextAreaElement;
      setTextareaValue(textarea, 'some content');

      const clearBtn = screen.getByTestId('reset-button');
      await user.click(clearBtn);

      expect(textarea).toHaveValue('');
    });

    it('shows entry count badge when valid JSON array is entered', () => {
      render(<BulkImportJsonUploader />);

      const textarea = screen.getByTestId('json-textarea') as HTMLTextAreaElement;
      setTextareaValue(textarea, '[{"bggId": 1, "name": "A"}, {"bggId": 2, "name": "B"}]');

      expect(screen.getByText('2 entries')).toBeInTheDocument();
    });
  });

  describe('Client-Side Validation (on Preview click)', () => {
    it('shows error for invalid JSON syntax', async () => {
      const user = userEvent.setup();
      render(<BulkImportJsonUploader />);

      const textarea = screen.getByTestId('json-textarea') as HTMLTextAreaElement;
      setTextareaValue(textarea, 'not valid json');

      await user.click(screen.getByTestId('preview-button'));

      expect(screen.getByTestId('validation-errors')).toBeInTheDocument();
      expect(screen.getByText(/invalid json format/i)).toBeInTheDocument();
    });

    it('shows error for non-array JSON', async () => {
      const user = userEvent.setup();
      render(<BulkImportJsonUploader />);

      const textarea = screen.getByTestId('json-textarea') as HTMLTextAreaElement;
      setTextareaValue(textarea, '{"key": "value"}');

      await user.click(screen.getByTestId('preview-button'));

      expect(screen.getByTestId('validation-errors')).toBeInTheDocument();
      expect(screen.getByText(/json must be an array/i)).toBeInTheDocument();
    });

    it('shows error for empty array', async () => {
      const user = userEvent.setup();
      render(<BulkImportJsonUploader />);

      const textarea = screen.getByTestId('json-textarea') as HTMLTextAreaElement;
      setTextareaValue(textarea, '[]');

      await user.click(screen.getByTestId('preview-button'));

      expect(screen.getByTestId('validation-errors')).toBeInTheDocument();
      expect(screen.getByText(/json array is empty/i)).toBeInTheDocument();
    });

    it('shows error for entries with invalid bggId', async () => {
      const user = userEvent.setup();
      render(<BulkImportJsonUploader />);

      const textarea = screen.getByTestId('json-textarea') as HTMLTextAreaElement;
      setTextareaValue(textarea, '[{"bggId": -1, "name": "Test"}]');

      await user.click(screen.getByTestId('preview-button'));

      expect(screen.getByTestId('validation-errors')).toBeInTheDocument();
      expect(screen.getByText(/invalid entries/i)).toBeInTheDocument();
    });

    it('shows error for entries missing name', async () => {
      const user = userEvent.setup();
      render(<BulkImportJsonUploader />);

      const textarea = screen.getByTestId('json-textarea') as HTMLTextAreaElement;
      setTextareaValue(textarea, '[{"bggId": 123, "name": ""}]');

      await user.click(screen.getByTestId('preview-button'));

      expect(screen.getByTestId('validation-errors')).toBeInTheDocument();
      expect(screen.getByText(/invalid entries/i)).toBeInTheDocument();
    });

    it('keeps preview button disabled for whitespace-only content', () => {
      render(<BulkImportJsonUploader />);

      const textarea = screen.getByTestId('json-textarea') as HTMLTextAreaElement;
      setTextareaValue(textarea, '   ');

      const previewBtn = screen.getByTestId('preview-button');
      expect(previewBtn).toBeDisabled();
    });
  });

  describe('Preview Step', () => {
    it('shows preview after clicking Preview Import with valid JSON', async () => {
      const user = userEvent.setup();
      render(<BulkImportJsonUploader />);

      const textarea = screen.getByTestId('json-textarea') as HTMLTextAreaElement;
      setTextareaValue(textarea, '[{"bggId": 123, "name": "Test Game"}]');

      await user.click(screen.getByTestId('preview-button'));

      // Preview should be visible
      expect(screen.getByTestId('bulk-import-preview')).toBeInTheDocument();
      // Upload area should be hidden
      expect(screen.queryByTestId('json-textarea')).not.toBeInTheDocument();
    });

    it('returns to upload when Back to Edit is clicked in preview', async () => {
      const user = userEvent.setup();
      render(<BulkImportJsonUploader />);

      const textarea = screen.getByTestId('json-textarea') as HTMLTextAreaElement;
      setTextareaValue(textarea, '[{"bggId": 123, "name": "Test Game"}]');

      await user.click(screen.getByTestId('preview-button'));
      expect(screen.getByTestId('bulk-import-preview')).toBeInTheDocument();

      await user.click(screen.getByTestId('preview-back'));

      // Should be back to upload area
      expect(screen.queryByTestId('bulk-import-preview')).not.toBeInTheDocument();
      expect(screen.getByTestId('json-textarea')).toBeInTheDocument();
    });

    it('returns to upload when Cancel is clicked in preview', async () => {
      const user = userEvent.setup();
      render(<BulkImportJsonUploader />);

      const textarea = screen.getByTestId('json-textarea') as HTMLTextAreaElement;
      setTextareaValue(textarea, '[{"bggId": 123, "name": "Test Game"}]');

      await user.click(screen.getByTestId('preview-button'));

      await user.click(screen.getByTestId('preview-cancel'));

      expect(screen.queryByTestId('bulk-import-preview')).not.toBeInTheDocument();
      expect(screen.getByTestId('json-textarea')).toBeInTheDocument();
    });
  });

  describe('API Submission (via Preview Confirm)', () => {
    it('calls mutate with JSON content when confirm is clicked', async () => {
      const user = userEvent.setup();
      render(<BulkImportJsonUploader />);

      const textarea = screen.getByTestId('json-textarea') as HTMLTextAreaElement;
      const validJson = '[{"bggId": 123, "name": "Test Game"}]';
      setTextareaValue(textarea, validJson);

      // Go to preview
      await user.click(screen.getByTestId('preview-button'));
      // Confirm import
      await user.click(screen.getByTestId('confirm-import'));

      expect(mockMutate).toHaveBeenCalledWith(validJson);
    });

    it('displays API error when submission fails in preview', () => {
      mockMutationState.error = new Error('Rate limit exceeded');
      render(<BulkImportJsonUploader />);

      expect(screen.getByTestId('api-error')).toBeInTheDocument();
      expect(screen.getByText(/rate limit exceeded/i)).toBeInTheDocument();
    });
  });

  describe('Result Display', () => {
    it('shows import result after successful submission via preview', async () => {
      const user = userEvent.setup();
      render(<BulkImportJsonUploader />);

      const textarea = screen.getByTestId('json-textarea') as HTMLTextAreaElement;
      setTextareaValue(textarea, '[{"bggId": 123, "name": "Test"}]');

      await user.click(screen.getByTestId('preview-button'));
      await user.click(screen.getByTestId('confirm-import'));

      expect(screen.getByTestId('bulk-import-results')).toBeInTheDocument();
    });

    it('displays summary stats in result', async () => {
      const user = userEvent.setup();
      render(<BulkImportJsonUploader />);

      const textarea = screen.getByTestId('json-textarea') as HTMLTextAreaElement;
      setTextareaValue(textarea, '[{"bggId": 123, "name": "Test"}]');

      await user.click(screen.getByTestId('preview-button'));
      await user.click(screen.getByTestId('confirm-import'));

      expect(screen.getByText('Total')).toBeInTheDocument();
      expect(screen.getByText('Enqueued')).toBeInTheDocument();
      expect(screen.getByText('Skipped')).toBeInTheDocument();
      expect(screen.getByText('Failed')).toBeInTheDocument();
    });

    it('displays error details table when errors exist', async () => {
      const user = userEvent.setup();
      render(<BulkImportJsonUploader />);

      const textarea = screen.getByTestId('json-textarea') as HTMLTextAreaElement;
      setTextareaValue(textarea, '[{"bggId": 123, "name": "Test"}]');

      await user.click(screen.getByTestId('preview-button'));
      await user.click(screen.getByTestId('confirm-import'));

      expect(screen.getByText('Gloomhaven')).toBeInTheDocument();
      expect(screen.getByText('Already exists')).toBeInTheDocument();
    });

    it('shows import another button after result', async () => {
      const user = userEvent.setup();
      render(<BulkImportJsonUploader />);

      const textarea = screen.getByTestId('json-textarea') as HTMLTextAreaElement;
      setTextareaValue(textarea, '[{"bggId": 123, "name": "Test"}]');

      await user.click(screen.getByTestId('preview-button'));
      await user.click(screen.getByTestId('confirm-import'));

      expect(screen.getByTestId('new-import-button')).toBeInTheDocument();
    });

    it('resets to initial state when import another is clicked', async () => {
      const user = userEvent.setup();
      render(<BulkImportJsonUploader />);

      const textarea = screen.getByTestId('json-textarea') as HTMLTextAreaElement;
      setTextareaValue(textarea, '[{"bggId": 123, "name": "Test"}]');

      await user.click(screen.getByTestId('preview-button'));
      await user.click(screen.getByTestId('confirm-import'));

      expect(screen.getByTestId('bulk-import-results')).toBeInTheDocument();

      await user.click(screen.getByTestId('new-import-button'));

      expect(screen.queryByTestId('bulk-import-results')).not.toBeInTheDocument();
      expect(screen.getByTestId('json-textarea')).toBeInTheDocument();
      expect(screen.getByTestId('json-textarea')).toHaveValue('');
    });
  });

  describe('File Upload', () => {
    it('renders hidden file input', () => {
      render(<BulkImportJsonUploader />);

      const fileInput = screen.getByTestId('file-input');
      expect(fileInput).toBeInTheDocument();
      expect(fileInput).toHaveAttribute('accept', '.json,application/json');
    });

    it('accepts JSON file and populates textarea', async () => {
      const user = userEvent.setup();
      render(<BulkImportJsonUploader />);

      const jsonContent = '[{"bggId": 1, "name": "Game1"}]';
      const file = new File([jsonContent], 'games.json', { type: 'application/json' });

      const fileInput = screen.getByTestId('file-input');
      await user.upload(fileInput, file);

      await waitFor(() => {
        const textarea = screen.getByTestId('json-textarea');
        expect(textarea).toHaveValue(jsonContent);
      });
    });

    it('shows file name badge after file selection', async () => {
      const user = userEvent.setup();
      render(<BulkImportJsonUploader />);

      const jsonContent = '[{"bggId": 1, "name": "Game1"}]';
      const file = new File([jsonContent], 'my-games.json', { type: 'application/json' });

      const fileInput = screen.getByTestId('file-input');
      await user.upload(fileInput, file);

      await waitFor(() => {
        expect(screen.getByText('my-games.json')).toBeInTheDocument();
      });
    });

    it('rejects non-JSON files', async () => {
      render(<BulkImportJsonUploader />);

      const file = new File(['hello'], 'data.txt', { type: 'text/plain' });

      const fileInput = screen.getByTestId('file-input');
      // Use fireEvent to bypass browser accept filtering in jsdom
      fireEvent.change(fileInput, { target: { files: [file] } });

      await waitFor(() => {
        expect(screen.getByText(/please select a .json file/i)).toBeInTheDocument();
      });
    });
  });

  describe('Drag and Drop', () => {
    it('shows drag-over state on drag enter', () => {
      render(<BulkImportJsonUploader />);

      const dropZone = screen.getByTestId('drop-zone');

      fireEvent.dragOver(dropZone, { dataTransfer: { files: [] } });

      expect(screen.getByText(/drop json file here/i)).toBeInTheDocument();
    });

    it('reverts drag-over state on drag leave', () => {
      render(<BulkImportJsonUploader />);

      const dropZone = screen.getByTestId('drop-zone');

      fireEvent.dragOver(dropZone, { dataTransfer: { files: [] } });
      fireEvent.dragLeave(dropZone, { dataTransfer: { files: [] } });

      expect(screen.getByText(/drag & drop a .json file here/i)).toBeInTheDocument();
    });
  });

  describe('Accessibility', () => {
    it('has accessible drop zone', () => {
      render(<BulkImportJsonUploader />);

      const dropZone = screen.getByTestId('drop-zone');
      expect(dropZone).toHaveAttribute('role', 'button');
      expect(dropZone).toHaveAttribute('tabIndex', '0');
      expect(dropZone).toHaveAttribute('aria-label', 'Drop JSON file here or click to select');
    });

    it('has labeled textarea', () => {
      render(<BulkImportJsonUploader />);

      expect(screen.getByLabelText('JSON Content')).toBeInTheDocument();
    });

    it('has labeled file input', () => {
      render(<BulkImportJsonUploader />);

      expect(screen.getByLabelText('Select JSON file')).toBeInTheDocument();
    });
  });
});
