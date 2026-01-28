/**
 * PDF Upload Limits Configuration Component Tests
 * Issue #3078: Admin UI - PDF Limits Configuration
 */

import { render, screen, fireEvent, waitFor } from '@testing-library/react';
import userEvent from '@testing-library/user-event';
import { PdfLimitsConfig } from '../PdfLimitsConfig';
import { api } from '../../../lib/api';
import { toast } from '@/components/layout/Toast';
import { useAuthUser } from '@/components/auth/AuthProvider';

vi.mock('../../../lib/api');
vi.mock('@/components/layout/Toast');
vi.mock('@/components/auth/AuthProvider');

const mockApi = api as Mocked<typeof api>;
const mockToast = toast as Mocked<typeof toast>;
const mockUseAuthUser = useAuthUser as Mock;

describe('PdfLimitsConfig', () => {
  const mockLimits = {
    maxFileSizeBytes: 104857600, // 100 MB
    maxPagesPerDocument: 500,
    maxDocumentsPerGame: 10,
    allowedMimeTypes: ['application/pdf'],
    lastUpdatedAt: '2025-01-15T10:00:00Z',
    lastUpdatedByUserId: 'admin-user-id-12345678',
  };

  const mockUser = {
    id: 'test-user',
    email: 'admin@test.com',
    role: 'Admin',
    displayName: 'Test Admin',
  };

  beforeEach(() => {
    vi.clearAllMocks();
    mockUseAuthUser.mockReturnValue({ user: mockUser, loading: false });
    mockApi.config = {
      getPdfUploadLimits: vi.fn().mockResolvedValue(mockLimits),
      updatePdfUploadLimits: vi.fn().mockResolvedValue({
        ...mockLimits,
        lastUpdatedAt: new Date().toISOString(),
      }),
    } as any;
    mockToast.success = vi.fn();
    mockToast.error = vi.fn();
    mockToast.info = vi.fn();
  });

  it('renders loading state initially', () => {
    render(<PdfLimitsConfig />);

    expect(screen.getByText(/Loading PDF upload limits/)).toBeInTheDocument();
  });

  it('loads and displays current limits', async () => {
    render(<PdfLimitsConfig />);

    await waitFor(() => {
      expect(screen.getByText(/PDF Upload Limits/)).toBeInTheDocument();
    });

    // Check form fields have correct values
    expect(screen.getByLabelText(/Maximum File Size/i)).toHaveValue(100);
    expect(screen.getByLabelText(/Maximum Pages Per Document/i)).toHaveValue(500);
    expect(screen.getByLabelText(/Maximum Documents Per Game/i)).toHaveValue(10);

    // Check PDF MIME type is checked
    const pdfCheckbox = screen.getByLabelText(/PDF \(application\/pdf\)/);
    expect(pdfCheckbox).toBeChecked();
  });

  it('displays last updated information', async () => {
    render(<PdfLimitsConfig />);

    await waitFor(() => {
      expect(screen.getByText(/Last updated:/)).toBeInTheDocument();
    });

    expect(screen.getByText(/admin-us/)).toBeInTheDocument();
  });

  it('converts bytes to MB for display', async () => {
    mockApi.config.getPdfUploadLimits = vi.fn().mockResolvedValue({
      ...mockLimits,
      maxFileSizeBytes: 209715200, // 200 MB
    });

    render(<PdfLimitsConfig />);

    await waitFor(() => {
      expect(screen.getByLabelText(/Maximum File Size/i)).toHaveValue(200);
    });
  });

  it('converts bytes to GB for large values', async () => {
    mockApi.config.getPdfUploadLimits = vi.fn().mockResolvedValue({
      ...mockLimits,
      maxFileSizeBytes: 2147483648, // 2 GB
    });

    render(<PdfLimitsConfig />);

    await waitFor(() => {
      expect(screen.getByLabelText(/Maximum File Size/i)).toHaveValue(2);
    });

    // Check that GB unit is selected
    expect(screen.getByRole('combobox')).toHaveTextContent('GB');
  });

  it('disables save button when form is pristine', async () => {
    render(<PdfLimitsConfig />);

    await waitFor(() => {
      expect(screen.getByText(/PDF Upload Limits/)).toBeInTheDocument();
    });

    const saveButton = screen.getByRole('button', { name: /Save Changes/i });
    expect(saveButton).toBeDisabled();
  });

  it('enables save button when form is dirty', async () => {
    const user = userEvent.setup();
    render(<PdfLimitsConfig />);

    await waitFor(() => {
      expect(screen.getByText(/PDF Upload Limits/)).toBeInTheDocument();
    });

    const pagesInput = screen.getByLabelText(/Maximum Pages Per Document/i);
    await user.clear(pagesInput);
    await user.type(pagesInput, '600');

    const saveButton = screen.getByRole('button', { name: /Save Changes/i });
    expect(saveButton).toBeEnabled();
  });

  it('submits form with correct data', async () => {
    const user = userEvent.setup();
    render(<PdfLimitsConfig />);

    await waitFor(() => {
      expect(screen.getByText(/PDF Upload Limits/)).toBeInTheDocument();
    });

    // Change pages per document
    const pagesInput = screen.getByLabelText(/Maximum Pages Per Document/i);
    await user.clear(pagesInput);
    await user.type(pagesInput, '600');

    // Submit form
    const saveButton = screen.getByRole('button', { name: /Save Changes/i });
    await user.click(saveButton);

    await waitFor(() => {
      expect(mockApi.config.updatePdfUploadLimits).toHaveBeenCalledWith({
        maxFileSizeBytes: 104857600, // 100 MB
        maxPagesPerDocument: 600,
        maxDocumentsPerGame: 10,
        allowedMimeTypes: ['application/pdf'],
      });
      expect(mockToast.success).toHaveBeenCalledWith(
        'PDF upload limits updated successfully'
      );
    });
  });

  it('handles MIME type toggle', async () => {
    const user = userEvent.setup();
    render(<PdfLimitsConfig />);

    await waitFor(() => {
      expect(screen.getByText(/PDF Upload Limits/)).toBeInTheDocument();
    });

    // Toggle additional MIME type
    const legacyPdfCheckbox = screen.getByLabelText(/PDF Legacy/);
    await user.click(legacyPdfCheckbox);

    expect(legacyPdfCheckbox).toBeChecked();

    // Save should be enabled now
    const saveButton = screen.getByRole('button', { name: /Save Changes/i });
    expect(saveButton).toBeEnabled();
  });

  it('shows validation error for empty MIME types', async () => {
    const user = userEvent.setup();
    render(<PdfLimitsConfig />);

    await waitFor(() => {
      expect(screen.getByText(/PDF Upload Limits/)).toBeInTheDocument();
    });

    // Uncheck the only MIME type
    const pdfCheckbox = screen.getByLabelText(/PDF \(application\/pdf\)/);
    await user.click(pdfCheckbox);

    // Try to submit
    const saveButton = screen.getByRole('button', { name: /Save Changes/i });
    await user.click(saveButton);

    await waitFor(() => {
      expect(screen.getByText(/Select at least one MIME type/)).toBeInTheDocument();
    });
  });

  it('resets form to current values', async () => {
    const user = userEvent.setup();
    render(<PdfLimitsConfig />);

    await waitFor(() => {
      expect(screen.getByText(/PDF Upload Limits/)).toBeInTheDocument();
    });

    // Change a value
    const pagesInput = screen.getByLabelText(/Maximum Pages Per Document/i);
    await user.clear(pagesInput);
    await user.type(pagesInput, '999');

    // Reset
    const resetButton = screen.getByRole('button', { name: /Reset/i });
    await user.click(resetButton);

    await waitFor(() => {
      expect(pagesInput).toHaveValue(500);
      expect(mockToast.info).toHaveBeenCalledWith('Form reset to current values');
    });
  });

  it('handles API error gracefully', async () => {
    mockApi.config.getPdfUploadLimits = vi
      .fn()
      .mockRejectedValue(new Error('Network error'));

    render(<PdfLimitsConfig />);

    await waitFor(() => {
      expect(mockToast.error).toHaveBeenCalled();
    });
  });

  it('shows loading state during submission', async () => {
    const user = userEvent.setup();

    // Make API call slow
    mockApi.config.updatePdfUploadLimits = vi.fn().mockImplementation(
      () => new Promise((resolve) => setTimeout(() => resolve(mockLimits), 1000))
    );

    render(<PdfLimitsConfig />);

    await waitFor(() => {
      expect(screen.getByText(/PDF Upload Limits/)).toBeInTheDocument();
    });

    // Change a value
    const pagesInput = screen.getByLabelText(/Maximum Pages Per Document/i);
    await user.clear(pagesInput);
    await user.type(pagesInput, '600');

    // Submit
    const saveButton = screen.getByRole('button', { name: /Save Changes/i });
    await user.click(saveButton);

    // Should show loading state
    expect(screen.getByText(/Saving.../)).toBeInTheDocument();
  });

  it('renders info card with explanations', async () => {
    render(<PdfLimitsConfig />);

    await waitFor(() => {
      expect(screen.getByText(/About PDF Upload Limits/)).toBeInTheDocument();
    });

    expect(screen.getByText(/Maximum File Size:/)).toBeInTheDocument();
    expect(screen.getByText(/Maximum Pages:/)).toBeInTheDocument();
    expect(screen.getByText(/Documents Per Game:/)).toBeInTheDocument();
    expect(screen.getByText(/MIME Types:/)).toBeInTheDocument();
  });

  it('returns null when user is not authenticated', () => {
    mockUseAuthUser.mockReturnValue({ user: null, loading: false });

    const { container } = render(<PdfLimitsConfig />);

    expect(container.firstChild).toBeNull();
  });

  it('converts display value to bytes correctly when saving', async () => {
    const user = userEvent.setup();
    render(<PdfLimitsConfig />);

    await waitFor(() => {
      expect(screen.getByText(/PDF Upload Limits/)).toBeInTheDocument();
    });

    // Change file size to 150 MB
    const fileSizeInput = screen.getByLabelText(/Maximum File Size/i);
    await user.clear(fileSizeInput);
    await user.type(fileSizeInput, '150');

    // Submit
    const saveButton = screen.getByRole('button', { name: /Save Changes/i });
    await user.click(saveButton);

    await waitFor(() => {
      expect(mockApi.config.updatePdfUploadLimits).toHaveBeenCalledWith(
        expect.objectContaining({
          maxFileSizeBytes: 157286400, // 150 * 1024 * 1024
        })
      );
    });
  });
});
