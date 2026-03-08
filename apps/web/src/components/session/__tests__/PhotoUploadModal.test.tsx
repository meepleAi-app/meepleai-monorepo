/**
 * Unit tests for PhotoUploadModal component (Issue #5368)
 * Epic #5358 - Session Photo Attachments (Phase 0)
 */

import { describe, it, expect, vi, beforeEach, afterEach } from 'vitest';
import { render, screen, cleanup, fireEvent, waitFor } from '@testing-library/react';
import userEvent from '@testing-library/user-event';

import {
  PhotoUploadModal,
  type PhotoUploadModalProps,
  type SessionAttachmentDto,
} from '../PhotoUploadModal';

// Track created object URLs for cleanup verification
const revokeObjectURLSpy = vi.fn();
const createObjectURLSpy = vi.fn(() => 'blob:http://localhost/fake-preview');
Object.defineProperty(URL, 'createObjectURL', { value: createObjectURLSpy, writable: true });
Object.defineProperty(URL, 'revokeObjectURL', { value: revokeObjectURLSpy, writable: true });

function createFile(name: string, size: number, type: string): File {
  const buffer = new ArrayBuffer(size);
  return new File([buffer], name, { type });
}

const mockAttachmentResponse: SessionAttachmentDto = {
  id: 'att-001',
  sessionId: 'session-123',
  playerId: 'player-456',
  attachmentType: 'BoardState',
  blobUrl: 'https://storage.example.com/photo.jpg',
  thumbnailUrl: 'https://storage.example.com/thumb.jpg',
  caption: null,
  contentType: 'image/jpeg',
  fileSizeBytes: 500000,
  snapshotIndex: null,
  createdAt: '2026-03-07T10:00:00Z',
};

describe('PhotoUploadModal', () => {
  const defaultProps: PhotoUploadModalProps = {
    sessionId: 'session-123',
    playerId: 'player-456',
    open: true,
    onOpenChange: vi.fn(),
    onUploadComplete: vi.fn(),
  };

  beforeEach(() => {
    vi.clearAllMocks();
    createObjectURLSpy.mockReturnValue('blob:http://localhost/fake-preview');
  });

  afterEach(() => {
    cleanup();
    vi.restoreAllMocks();
  });

  // ================================================================
  // Rendering
  // ================================================================

  it('renders dialog when open', () => {
    render(<PhotoUploadModal {...defaultProps} />);
    expect(screen.getByTestId('photo-upload-modal')).toBeInTheDocument();
    expect(screen.getByRole('heading', { name: /Upload Photo/ })).toBeInTheDocument();
  });

  it('does not render dialog when closed', () => {
    render(<PhotoUploadModal {...defaultProps} open={false} />);
    expect(screen.queryByTestId('photo-upload-modal')).not.toBeInTheDocument();
  });

  it('renders drop area with instructions', () => {
    render(<PhotoUploadModal {...defaultProps} />);
    expect(screen.getByTestId('photo-drop-area')).toBeInTheDocument();
    expect(screen.getByText(/Tap to take a photo/)).toBeInTheDocument();
    expect(screen.getByText(/JPEG or PNG, max 10 MB/)).toBeInTheDocument();
  });

  it('renders attachment type selector with all options', () => {
    render(<PhotoUploadModal {...defaultProps} />);
    const select = screen.getByTestId('attachment-type-select');
    expect(select).toBeInTheDocument();
    expect(screen.getByText('Player Area')).toBeInTheDocument();
    expect(screen.getByText('Board State')).toBeInTheDocument();
    expect(screen.getByText('Character Sheet')).toBeInTheDocument();
    expect(screen.getByText('Resource Inventory')).toBeInTheDocument();
    expect(screen.getByText('Custom')).toBeInTheDocument();
  });

  it('renders caption input', () => {
    render(<PhotoUploadModal {...defaultProps} />);
    expect(screen.getByTestId('photo-caption-input')).toBeInTheDocument();
    expect(screen.getByText('0/200')).toBeInTheDocument();
  });

  it('renders upload button disabled when no file selected', () => {
    render(<PhotoUploadModal {...defaultProps} />);
    const uploadBtn = screen.getByTestId('upload-button');
    expect(uploadBtn).toBeDisabled();
  });

  it('renders cancel button', () => {
    render(<PhotoUploadModal {...defaultProps} />);
    expect(screen.getByTestId('cancel-button')).toBeInTheDocument();
  });

  // ================================================================
  // File input attributes
  // ================================================================

  it('has correct accept and capture attributes on file input', () => {
    render(<PhotoUploadModal {...defaultProps} />);
    const input = screen.getByTestId('photo-file-input');
    expect(input).toHaveAttribute('accept', 'image/jpeg,image/png');
    expect(input).toHaveAttribute('capture', 'environment');
  });

  // ================================================================
  // File selection
  // ================================================================

  it('shows preview after selecting a valid file', async () => {
    render(<PhotoUploadModal {...defaultProps} />);
    const input = screen.getByTestId('photo-file-input');
    const file = createFile('photo.jpg', 500000, 'image/jpeg');

    fireEvent.change(input, { target: { files: [file] } });

    expect(await screen.findByTestId('photo-preview')).toBeInTheDocument();
    expect(screen.getByText('photo.jpg')).toBeInTheDocument();
    expect(screen.getByText('(0.5 MB)')).toBeInTheDocument();
  });

  it('enables upload button after file selection', () => {
    render(<PhotoUploadModal {...defaultProps} />);
    const input = screen.getByTestId('photo-file-input');
    const file = createFile('photo.png', 100000, 'image/png');

    fireEvent.change(input, { target: { files: [file] } });

    expect(screen.getByTestId('upload-button')).not.toBeDisabled();
  });

  // ================================================================
  // Validation
  // ================================================================

  it('shows error for invalid file type', () => {
    render(<PhotoUploadModal {...defaultProps} />);
    const input = screen.getByTestId('photo-file-input');
    const file = createFile('doc.pdf', 1000, 'application/pdf');

    fireEvent.change(input, { target: { files: [file] } });

    expect(screen.getByTestId('upload-error')).toBeInTheDocument();
    expect(screen.getByText(/Only JPEG and PNG images are accepted/)).toBeInTheDocument();
  });

  it('shows error for file exceeding 10MB', () => {
    render(<PhotoUploadModal {...defaultProps} />);
    const input = screen.getByTestId('photo-file-input');
    const file = createFile('huge.jpg', 11 * 1024 * 1024, 'image/jpeg');

    fireEvent.change(input, { target: { files: [file] } });

    expect(screen.getByTestId('upload-error')).toBeInTheDocument();
    expect(screen.getByText(/File must be 10 MB or smaller/)).toBeInTheDocument();
  });

  it('does not show preview for invalid file', () => {
    render(<PhotoUploadModal {...defaultProps} />);
    const input = screen.getByTestId('photo-file-input');
    const file = createFile('doc.pdf', 1000, 'application/pdf');

    fireEvent.change(input, { target: { files: [file] } });

    expect(screen.queryByTestId('photo-preview')).not.toBeInTheDocument();
  });

  // ================================================================
  // Caption
  // ================================================================

  it('updates caption character count', async () => {
    const user = userEvent.setup();
    render(<PhotoUploadModal {...defaultProps} />);
    const captionInput = screen.getByTestId('photo-caption-input');

    await user.type(captionInput, 'Hello World');

    expect(screen.getByText('11/200')).toBeInTheDocument();
  });

  // ================================================================
  // Type selector
  // ================================================================

  it('allows changing attachment type', async () => {
    const user = userEvent.setup();
    render(<PhotoUploadModal {...defaultProps} />);
    const select = screen.getByTestId('attachment-type-select');

    await user.selectOptions(select, 'CharacterSheet');

    expect((select as HTMLSelectElement).value).toBe('CharacterSheet');
  });

  // ================================================================
  // Clear file
  // ================================================================

  it('clears file when remove button is clicked', async () => {
    const user = userEvent.setup();
    render(<PhotoUploadModal {...defaultProps} />);

    // Select a file first
    const input = screen.getByTestId('photo-file-input');
    const file = createFile('photo.jpg', 500000, 'image/jpeg');
    fireEvent.change(input, { target: { files: [file] } });

    expect(screen.getByTestId('photo-preview')).toBeInTheDocument();

    // Click remove
    const removeBtn = screen.getByRole('button', { name: 'Remove file' });
    await user.click(removeBtn);

    expect(screen.queryByTestId('photo-preview')).not.toBeInTheDocument();
  });

  // ================================================================
  // Cancel
  // ================================================================

  it('calls onOpenChange(false) when cancel is clicked', async () => {
    const user = userEvent.setup();
    const onOpenChange = vi.fn();
    render(<PhotoUploadModal {...defaultProps} onOpenChange={onOpenChange} />);

    await user.click(screen.getByTestId('cancel-button'));

    expect(onOpenChange).toHaveBeenCalledWith(false);
  });

  // ================================================================
  // Drag and drop
  // ================================================================

  it('handles drag-and-drop file selection', () => {
    render(<PhotoUploadModal {...defaultProps} />);
    const dropArea = screen.getByTestId('photo-drop-area');
    const file = createFile('board.jpg', 300000, 'image/jpeg');

    fireEvent.dragOver(dropArea);
    fireEvent.drop(dropArea, { dataTransfer: { files: [file] } });

    expect(screen.getByTestId('photo-preview')).toBeInTheDocument();
  });

  // ================================================================
  // Upload via XHR
  // ================================================================

  it('uploads file via XHR and calls onUploadComplete', async () => {
    const onUploadComplete = vi.fn();
    const onOpenChange = vi.fn();

    // Mock XHR
    const xhrMock = {
      open: vi.fn(),
      send: vi.fn(),
      upload: { addEventListener: vi.fn() },
      addEventListener: vi.fn(),
      status: 201,
      responseText: JSON.stringify(mockAttachmentResponse),
    };

    vi.spyOn(window, 'XMLHttpRequest').mockImplementation(
      () => xhrMock as unknown as XMLHttpRequest
    );

    render(
      <PhotoUploadModal
        {...defaultProps}
        onUploadComplete={onUploadComplete}
        onOpenChange={onOpenChange}
      />
    );

    // Select file
    const input = screen.getByTestId('photo-file-input');
    const file = createFile('photo.jpg', 500000, 'image/jpeg');
    fireEvent.change(input, { target: { files: [file] } });

    // Click upload
    const uploadBtn = screen.getByTestId('upload-button');
    fireEvent.click(uploadBtn);

    // Verify XHR was opened with correct URL
    expect(xhrMock.open).toHaveBeenCalledWith(
      'POST',
      '/api/v1/live-sessions/session-123/attachments'
    );

    // Simulate XHR load event
    const loadHandler = xhrMock.addEventListener.mock.calls.find(
      (call: [string, () => void]) => call[0] === 'load'
    )?.[1] as (() => void) | undefined;
    if (loadHandler) loadHandler();

    await waitFor(() => {
      expect(onUploadComplete).toHaveBeenCalledWith(mockAttachmentResponse);
    });
  });

  it('shows error on XHR failure', async () => {
    const xhrMock = {
      open: vi.fn(),
      send: vi.fn(),
      upload: { addEventListener: vi.fn() },
      addEventListener: vi.fn(),
      status: 400,
      responseText: JSON.stringify({ error: 'File too large' }),
    };

    vi.spyOn(window, 'XMLHttpRequest').mockImplementation(
      () => xhrMock as unknown as XMLHttpRequest
    );

    render(<PhotoUploadModal {...defaultProps} />);

    const input = screen.getByTestId('photo-file-input');
    const file = createFile('photo.jpg', 500000, 'image/jpeg');
    fireEvent.change(input, { target: { files: [file] } });

    fireEvent.click(screen.getByTestId('upload-button'));

    // Simulate load with error status
    const loadHandler = xhrMock.addEventListener.mock.calls.find(
      (call: [string, () => void]) => call[0] === 'load'
    )?.[1] as (() => void) | undefined;
    if (loadHandler) loadHandler();

    await waitFor(() => {
      expect(screen.getByTestId('upload-error')).toBeInTheDocument();
      expect(screen.getByText('File too large')).toBeInTheDocument();
    });
  });

  it('shows error on XHR network error', async () => {
    const xhrMock = {
      open: vi.fn(),
      send: vi.fn(),
      upload: { addEventListener: vi.fn() },
      addEventListener: vi.fn(),
    };

    vi.spyOn(window, 'XMLHttpRequest').mockImplementation(
      () => xhrMock as unknown as XMLHttpRequest
    );

    render(<PhotoUploadModal {...defaultProps} />);

    const input = screen.getByTestId('photo-file-input');
    const file = createFile('photo.jpg', 500000, 'image/jpeg');
    fireEvent.change(input, { target: { files: [file] } });

    fireEvent.click(screen.getByTestId('upload-button'));

    // Simulate network error
    const errorHandler = xhrMock.addEventListener.mock.calls.find(
      (call: [string, () => void]) => call[0] === 'error'
    )?.[1] as (() => void) | undefined;
    if (errorHandler) errorHandler();

    await waitFor(() => {
      expect(screen.getByTestId('upload-error')).toBeInTheDocument();
      expect(screen.getByText('Network error during upload.')).toBeInTheDocument();
    });
  });

  // ================================================================
  // Snapshot index
  // ================================================================

  it('includes snapshotIndex in FormData when provided', async () => {
    const xhrMock = {
      open: vi.fn(),
      send: vi.fn(),
      upload: { addEventListener: vi.fn() },
      addEventListener: vi.fn(),
      status: 201,
      responseText: JSON.stringify(mockAttachmentResponse),
    };

    vi.spyOn(window, 'XMLHttpRequest').mockImplementation(
      () => xhrMock as unknown as XMLHttpRequest
    );

    render(<PhotoUploadModal {...defaultProps} snapshotIndex={3} />);

    const input = screen.getByTestId('photo-file-input');
    const file = createFile('photo.jpg', 500000, 'image/jpeg');
    fireEvent.change(input, { target: { files: [file] } });

    fireEvent.click(screen.getByTestId('upload-button'));

    // Verify FormData was sent
    expect(xhrMock.send).toHaveBeenCalledWith(expect.any(FormData));
    const sentFormData = xhrMock.send.mock.calls[0][0] as FormData;
    expect(sentFormData.get('snapshotIndex')).toBe('3');
    expect(sentFormData.get('playerId')).toBe('player-456');
  });

  // ================================================================
  // Reset state on reopen
  // ================================================================

  it('resets state when modal reopens', () => {
    const { rerender } = render(<PhotoUploadModal {...defaultProps} />);

    // Select a file
    const input = screen.getByTestId('photo-file-input');
    const file = createFile('photo.jpg', 500000, 'image/jpeg');
    fireEvent.change(input, { target: { files: [file] } });
    expect(screen.getByTestId('photo-preview')).toBeInTheDocument();

    // Close
    rerender(<PhotoUploadModal {...defaultProps} open={false} />);

    // Reopen
    rerender(<PhotoUploadModal {...defaultProps} open={true} />);

    // Preview should be gone
    expect(screen.queryByTestId('photo-preview')).not.toBeInTheDocument();
  });
});
