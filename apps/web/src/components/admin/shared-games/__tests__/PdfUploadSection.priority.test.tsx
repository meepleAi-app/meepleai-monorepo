/**
 * PdfUploadSection Priority Selector Tests
 *
 * Tests for the priority selector feature that allows admins
 * to mark PDF uploads as urgent (processed first in the queue).
 */

import { describe, it, expect, vi, beforeEach, afterEach } from 'vitest';
import { render, screen, waitFor } from '@testing-library/react';
import userEvent from '@testing-library/user-event';
import { QueryClient, QueryClientProvider } from '@tanstack/react-query';

import { PdfUploadSection } from '../PdfUploadSection';

// ============================================================================
// Test Utilities
// ============================================================================

function createTestQueryClient() {
  return new QueryClient({
    defaultOptions: {
      queries: {
        retry: false,
        gcTime: 0,
      },
    },
  });
}

function renderWithProviders(component: React.ReactElement) {
  const queryClient = createTestQueryClient();
  return render(<QueryClientProvider client={queryClient}>{component}</QueryClientProvider>);
}

function createMockPdf(): File {
  return new File(['pdf-content'], 'rules.pdf', { type: 'application/pdf' });
}

// ============================================================================
// Mock XHR
// ============================================================================

let mockXHR: {
  open: ReturnType<typeof vi.fn>;
  send: ReturnType<typeof vi.fn>;
  upload: { addEventListener: ReturnType<typeof vi.fn> };
  addEventListener: ReturnType<typeof vi.fn>;
  withCredentials: boolean;
  status: number;
  responseText: string;
};

beforeEach(() => {
  mockXHR = {
    open: vi.fn(),
    send: vi.fn(),
    upload: { addEventListener: vi.fn() },
    addEventListener: vi.fn(),
    withCredentials: false,
    status: 200,
    responseText: JSON.stringify({ documentId: 'doc-123', fileName: 'rules.pdf' }),
  };
  vi.clearAllMocks();
  global.XMLHttpRequest = vi.fn(() => mockXHR) as unknown as typeof XMLHttpRequest;
});

afterEach(() => {
  vi.restoreAllMocks();
});

// ============================================================================
// Helper: select file via the hidden input
// ============================================================================

async function selectFile(user: ReturnType<typeof userEvent.setup>) {
  const file = createMockPdf();
  const dropZone = screen.getByText(/Trascina un PDF/i);
  // Click the drop zone to trigger file input
  await user.click(dropZone);
  // Find the hidden file input and upload the file
  const input = document.getElementById('admin-pdf-input') as HTMLInputElement;
  await user.upload(input, file);
  return file;
}

// ============================================================================
// Tests
// ============================================================================

describe('PdfUploadSection Priority Selector', () => {
  const defaultProps = {
    gameId: 'game-123',
    onPdfUploaded: vi.fn(),
    onPdfRemoved: vi.fn(),
  };

  it('shows priority selector after file selection when showPrioritySelector is not false', async () => {
    const user = userEvent.setup();
    renderWithProviders(<PdfUploadSection {...defaultProps} />);

    // Priority selector should not be visible before file selection
    expect(screen.queryByTestId('priority-selector')).not.toBeInTheDocument();

    // Select a file
    await selectFile(user);

    // Priority selector should now be visible
    await waitFor(() => {
      expect(screen.getByTestId('priority-selector')).toBeInTheDocument();
    });
    expect(screen.getByText('Priorita:')).toBeInTheDocument();
  });

  it('shows priority selector when showPrioritySelector is explicitly true', async () => {
    const user = userEvent.setup();
    renderWithProviders(<PdfUploadSection {...defaultProps} showPrioritySelector={true} />);

    await selectFile(user);

    await waitFor(() => {
      expect(screen.getByTestId('priority-selector')).toBeInTheDocument();
    });
  });

  it('does not show selector when showPrioritySelector is false', async () => {
    const user = userEvent.setup();
    renderWithProviders(<PdfUploadSection {...defaultProps} showPrioritySelector={false} />);

    await selectFile(user);

    // File should be selected (upload button visible) but no priority selector
    await waitFor(() => {
      expect(screen.getByText('Carica PDF')).toBeInTheDocument();
    });
    expect(screen.queryByTestId('priority-selector')).not.toBeInTheDocument();
  });

  it('defaults to normal priority (no query param in URL)', async () => {
    const user = userEvent.setup();
    renderWithProviders(<PdfUploadSection {...defaultProps} />);

    await selectFile(user);

    // Click upload button
    const uploadBtn = await screen.findByText('Carica PDF');
    await user.click(uploadBtn);

    // XHR should be opened with the base URL (no priority query param)
    expect(mockXHR.open).toHaveBeenCalledWith('POST', '/api/v1/ingest/pdf');
  });

  it('includes ?priority=urgent in XHR URL when Urgente is selected', async () => {
    const user = userEvent.setup();
    renderWithProviders(<PdfUploadSection {...defaultProps} />);

    await selectFile(user);

    // Open the select and pick Urgente
    const trigger = screen.getByRole('combobox');
    await user.click(trigger);

    const urgentOption = await screen.findByText(/Urgente/i);
    await user.click(urgentOption);

    // Click upload button
    const uploadBtn = await screen.findByText('Carica PDF');
    await user.click(uploadBtn);

    // XHR should be opened with priority=urgent query param
    expect(mockXHR.open).toHaveBeenCalledWith('POST', '/api/v1/ingest/pdf?priority=urgent');
  });
});
