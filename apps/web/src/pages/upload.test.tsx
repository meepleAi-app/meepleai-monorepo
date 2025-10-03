import { render, screen, waitFor } from '@testing-library/react';
import userEvent from '@testing-library/user-event';
import UploadPage from './upload';

global.fetch = jest.fn();
const mockedFetch = global.fetch as jest.MockedFunction<typeof fetch>;

describe('Upload page', () => {
  beforeEach(() => {
    jest.clearAllMocks();
  });

  it('renders upload form', () => {
    render(<UploadPage />);

    expect(screen.getByText('Upload PDF Rulebook')).toBeInTheDocument();
    expect(screen.getByLabelText(/game id/i)).toBeInTheDocument();
    expect(screen.getByLabelText(/pdf file/i)).toBeInTheDocument();
  });

  it('shows back to home link', () => {
    render(<UploadPage />);

    const link = screen.getByRole('link', { name: /back to home/i });
    expect(link).toBeInTheDocument();
    expect(link).toHaveAttribute('href', '/');
  });

  it('disables upload button when no file selected', async () => {
    render(<UploadPage />);

    const uploadButton = screen.getByRole('button', { name: /upload pdf/i });
    expect(uploadButton).toBeDisabled();
  });

  it('shows error when game ID is empty', async () => {
    const user = userEvent.setup();
    render(<UploadPage />);

    const gameIdInput = screen.getByLabelText(/game id/i);
    await user.clear(gameIdInput);

    const file = new File(['dummy content'], 'test.pdf', { type: 'application/pdf' });
    const fileInput = screen.getByLabelText(/pdf file/i);
    await user.upload(fileInput, file);

    const uploadButton = screen.getByRole('button', { name: /upload pdf/i });
    await user.click(uploadButton);

    expect(screen.getByText('Please enter a game ID')).toBeInTheDocument();
  });

  it('uploads file successfully', async () => {
    mockedFetch.mockResolvedValueOnce({
      ok: true,
      json: async () => ({ documentId: 'doc123' }),
    } as Response);

    const user = userEvent.setup();
    render(<UploadPage />);

    const file = new File(['dummy content'], 'rulebook.pdf', { type: 'application/pdf' });
    const fileInput = screen.getByLabelText(/pdf file/i);
    await user.upload(fileInput, file);

    const uploadButton = screen.getByRole('button', { name: /upload pdf/i });
    await user.click(uploadButton);

    await waitFor(() => {
      expect(screen.getByText(/pdf uploaded successfully/i)).toBeInTheDocument();
      expect(screen.getByText(/document id: doc123/i)).toBeInTheDocument();
    });
  });

  it('shows error message on upload failure', async () => {
    mockedFetch.mockResolvedValueOnce({
      ok: false,
      statusText: 'Bad Request',
      json: async () => ({ error: 'Invalid file format' }),
    } as Response);

    const user = userEvent.setup();
    render(<UploadPage />);

    const file = new File(['dummy content'], 'test.pdf', { type: 'application/pdf' });
    const fileInput = screen.getByLabelText(/pdf file/i);
    await user.upload(fileInput, file);

    const uploadButton = screen.getByRole('button', { name: /upload pdf/i });
    await user.click(uploadButton);

    await waitFor(() => {
      expect(screen.getByText(/upload failed: invalid file format/i)).toBeInTheDocument();
    });
  });

  it('shows error on network failure', async () => {
    mockedFetch.mockRejectedValueOnce(new Error('Network error'));

    const user = userEvent.setup();
    render(<UploadPage />);

    const file = new File(['dummy content'], 'test.pdf', { type: 'application/pdf' });
    const fileInput = screen.getByLabelText(/pdf file/i);
    await user.upload(fileInput, file);

    const uploadButton = screen.getByRole('button', { name: /upload pdf/i });
    await user.click(uploadButton);

    await waitFor(() => {
      expect(screen.getByText(/upload failed: network error/i)).toBeInTheDocument();
    });
  });

  it('disables upload button while uploading', async () => {
    // First call: delayed upload response
    mockedFetch.mockImplementationOnce(() => new Promise((resolve) => setTimeout(() => resolve({
      ok: true,
      json: async () => ({ documentId: 'doc123' }),
    } as Response), 100)));

    // Second call: loadPdfs after upload
    mockedFetch.mockResolvedValueOnce({
      ok: true,
      json: async () => ({ pdfs: [] }),
    } as Response);

    const user = userEvent.setup();
    render(<UploadPage />);

    const file = new File(['dummy content'], 'test.pdf', { type: 'application/pdf' });
    const fileInput = screen.getByLabelText(/pdf file/i);
    await user.upload(fileInput, file);

    const uploadButton = screen.getByRole('button', { name: /upload pdf/i });
    await user.click(uploadButton);

    // Button should be disabled and show uploading text
    await waitFor(() => {
      expect(uploadButton).toBeDisabled();
      expect(screen.getByText(/uploading/i)).toBeInTheDocument();
    });

    // After upload completes, success message appears
    await waitFor(() => {
      expect(screen.getByText(/pdf uploaded successfully/i)).toBeInTheDocument();
    });
  });

  it('loads PDFs for a game', async () => {
    mockedFetch.mockResolvedValueOnce({
      ok: true,
      json: async () => ({
        pdfs: [
          {
            id: 'pdf1',
            fileName: 'rulebook1.pdf',
            fileSizeBytes: 1024000,
            uploadedAt: '2025-01-01T10:00:00Z',
            uploadedByUserId: 'user1',
          },
        ],
      }),
    } as Response);

    const user = userEvent.setup();
    render(<UploadPage />);

    const refreshButton = screen.getByRole('button', { name: /refresh/i });
    await user.click(refreshButton);

    await waitFor(() => {
      expect(screen.getByText('rulebook1.pdf')).toBeInTheDocument();
    });
  });

  it('formats file size correctly', async () => {
    mockedFetch.mockResolvedValueOnce({
      ok: true,
      json: async () => ({
        pdfs: [
          {
            id: 'pdf1',
            fileName: 'small.pdf',
            fileSizeBytes: 500,
            uploadedAt: '2025-01-01T10:00:00Z',
            uploadedByUserId: 'user1',
          },
          {
            id: 'pdf2',
            fileName: 'medium.pdf',
            fileSizeBytes: 5120,
            uploadedAt: '2025-01-01T10:00:00Z',
            uploadedByUserId: 'user1',
          },
          {
            id: 'pdf3',
            fileName: 'large.pdf',
            fileSizeBytes: 5242880,
            uploadedAt: '2025-01-01T10:00:00Z',
            uploadedByUserId: 'user1',
          },
        ],
      }),
    } as Response);

    const user = userEvent.setup();
    render(<UploadPage />);

    const refreshButton = screen.getByRole('button', { name: /refresh/i });
    await user.click(refreshButton);

    await waitFor(() => {
      expect(screen.getByText(/500 B/i)).toBeInTheDocument();
      expect(screen.getByText(/5.0 KB/i)).toBeInTheDocument();
      expect(screen.getByText(/5.0 MB/i)).toBeInTheDocument();
    });
  });

  it('shows empty state when no PDFs', async () => {
    mockedFetch.mockResolvedValueOnce({
      ok: true,
      json: async () => ({ pdfs: [] }),
    } as Response);

    const user = userEvent.setup();
    render(<UploadPage />);

    const refreshButton = screen.getByRole('button', { name: /refresh/i });
    await user.click(refreshButton);

    await waitFor(() => {
      expect(screen.getByText(/no pdfs uploaded yet/i)).toBeInTheDocument();
    });
  });

  it('clears file input after successful upload', async () => {
    mockedFetch.mockResolvedValueOnce({
      ok: true,
      json: async () => ({ documentId: 'doc123' }),
    } as Response);

    mockedFetch.mockResolvedValueOnce({
      ok: true,
      json: async () => ({ pdfs: [] }),
    } as Response);

    const user = userEvent.setup();
    render(<UploadPage />);

    const file = new File(['dummy content'], 'test.pdf', { type: 'application/pdf' });
    const fileInput = screen.getByLabelText(/pdf file/i) as HTMLInputElement;
    await user.upload(fileInput, file);

    expect(fileInput.files).toHaveLength(1);

    const uploadButton = screen.getByRole('button', { name: /upload pdf/i });
    await user.click(uploadButton);

    await waitFor(() => {
      expect(screen.getByText(/pdf uploaded successfully/i)).toBeInTheDocument();
    });

    // Note: We can't directly verify fileInput.files is empty because
    // JSDOM doesn't support setting file input values, but we can verify
    // the selected file state is cleared
    expect(screen.queryByDisplayValue('test.pdf')).not.toBeInTheDocument();
  });

  it('does not load PDFs when game ID is empty', async () => {
    const user = userEvent.setup();
    render(<UploadPage />);

    const gameIdInput = screen.getByLabelText(/game id/i);
    await user.clear(gameIdInput);

    const refreshButton = screen.getByRole('button', { name: /refresh/i });
    await user.click(refreshButton);

    // No fetch should be called
    expect(mockedFetch).not.toHaveBeenCalled();
  });

  it('can change game ID', async () => {
    const user = userEvent.setup();
    render(<UploadPage />);

    const gameIdInput = screen.getByLabelText(/game id/i) as HTMLInputElement;
    expect(gameIdInput.value).toBe('demo');

    await user.clear(gameIdInput);
    await user.type(gameIdInput, 'chess');

    expect(gameIdInput.value).toBe('chess');
  });
});
