/**
 * MultiDocumentCollectionUpload Tests - Issue #2764 Sprint 4
 *
 * Tests for MultiDocumentCollectionUpload component:
 * - Multi-file PDF upload workflow
 * - Collection name/description input
 * - Upload progress tracking
 * - Success/error states
 * - Form validation
 *
 * Pattern: Vitest + React Testing Library + MSW
 * Coverage target: 342 lines (85%+)
 */

import { render, screen, waitFor } from '@testing-library/react';
import userEvent from '@testing-library/user-event';
import { describe, it, expect, vi, beforeEach, afterEach } from 'vitest';
import React from 'react';

import { MultiDocumentCollectionUpload } from '../MultiDocumentCollectionUpload';

// Mock the api module to control collection creation
vi.mock('@/lib/api', () => ({
  api: {
    documents: {
      createCollection: vi.fn(),
    },
  },
  ApiError: class ApiError extends Error {
    constructor(message: string) {
      super(message);
      this.name = 'ApiError';
    }
  },
}));

// Import after mock
import { api } from '@/lib/api';

// Store original fetch to restore later
const originalFetch = global.fetch;

// Helper to create mock files
function createMockPdfFile(name: string, size: number = 1024): File {
  const content = '%PDF-' + new Array(size).fill('a').join('');
  return new File([content], name, { type: 'application/pdf' });
}

describe('MultiDocumentCollectionUpload - Issue #2764 Sprint 4', () => {
  beforeEach(() => {
    // Reset mocks
    vi.clearAllMocks();

    // Default successful mock for collection creation
    (api.documents.createCollection as ReturnType<typeof vi.fn>).mockResolvedValue({
      id: `coll-${Date.now()}`,
      name: 'Test Collection',
      gameId: 'game-123',
      createdAt: new Date().toISOString(),
    });

    // Mock fetch for PDF upload - MSW doesn't work reliably with jsdom's fetch
    // so we mock fetch directly for the PDF upload endpoint
    global.fetch = vi.fn().mockImplementation((url: string, options?: RequestInit) => {
      // Handle PDF upload endpoint
      if (typeof url === 'string' && url.includes('/api/v1/ingest/pdf')) {
        return Promise.resolve({
          ok: true,
          status: 201,
          json: () => Promise.resolve({
            documentId: `pdf-${Date.now()}`,
            fileName: 'test.pdf',
            status: 'pending',
            message: 'PDF uploaded successfully',
          }),
        } as Response);
      }
      // Fallback to original fetch for other requests
      return originalFetch(url, options);
    });
  });

  afterEach(() => {
    // Restore original fetch
    global.fetch = originalFetch;
  });

  // ============================================================================
  // TEST 1: Initial rendering
  // ============================================================================
  it('should render form with required fields', () => {
    // Arrange & Act
    render(
      <MultiDocumentCollectionUpload
        gameId="game-123"
        gameName="Test Game"
      />
    );

    // Assert
    expect(screen.getByText('Create Document Collection')).toBeInTheDocument();
    expect(screen.getByText(/upload multiple pdf documents/i)).toBeInTheDocument();
    expect(screen.getByLabelText(/collection name/i)).toBeInTheDocument();
    expect(screen.getByLabelText(/description/i)).toBeInTheDocument();
    expect(screen.getByText(/documents \*/i)).toBeInTheDocument();
  });

  // ============================================================================
  // TEST 2: Display game name
  // ============================================================================
  it('should display game name in description', () => {
    // Arrange & Act
    render(
      <MultiDocumentCollectionUpload
        gameId="game-123"
        gameName="Chess"
      />
    );

    // Assert
    expect(screen.getByText(/for chess/i)).toBeInTheDocument();
  });

  // ============================================================================
  // TEST 3: Collection name input
  // ============================================================================
  it('should update collection name on input', async () => {
    // Arrange
    const user = userEvent.setup();
    render(
      <MultiDocumentCollectionUpload
        gameId="game-123"
        gameName="Test Game"
      />
    );

    // Act
    const nameInput = screen.getByLabelText(/collection name/i);
    await user.type(nameInput, 'My Collection');

    // Assert
    expect(nameInput).toHaveValue('My Collection');
  });

  // ============================================================================
  // TEST 4: Collection description input
  // ============================================================================
  it('should update collection description on input', async () => {
    // Arrange
    const user = userEvent.setup();
    render(
      <MultiDocumentCollectionUpload
        gameId="game-123"
        gameName="Test Game"
      />
    );

    // Act
    const descInput = screen.getByLabelText(/description/i);
    await user.type(descInput, 'A detailed description');

    // Assert
    expect(descInput).toHaveValue('A detailed description');
  });

  // ============================================================================
  // TEST 5: Submit button disabled initially
  // ============================================================================
  it('should disable submit button when form is incomplete', () => {
    // Arrange & Act
    render(
      <MultiDocumentCollectionUpload
        gameId="game-123"
        gameName="Test Game"
      />
    );

    // Assert - Button should be disabled with no files and no name
    const submitButton = screen.getByRole('button', { name: /create collection/i });
    expect(submitButton).toBeDisabled();
  });

  // ============================================================================
  // TEST 6: Submit button enabled with valid form
  // ============================================================================
  it('should enable submit button when form is valid', async () => {
    // Arrange
    const user = userEvent.setup();
    render(
      <MultiDocumentCollectionUpload
        gameId="game-123"
        gameName="Test Game"
      />
    );

    // Act - Fill in collection name
    const nameInput = screen.getByLabelText(/collection name/i);
    await user.type(nameInput, 'My Collection');

    // Add a file
    const file = createMockPdfFile('rules.pdf');
    const fileInput = document.getElementById('file-upload') as HTMLInputElement;
    await userEvent.upload(fileInput, file);

    // Assert
    const submitButton = screen.getByRole('button', { name: /create collection/i });
    expect(submitButton).not.toBeDisabled();
  });

  // ============================================================================
  // TEST 7: Upload progress display
  // ============================================================================
  it('should show upload progress during submission', async () => {
    // Arrange
    const user = userEvent.setup();
    let resolveUpload: (value: Response) => void;

    // Use a deferred promise to control upload timing
    global.fetch = vi.fn().mockImplementation((url: string) => {
      if (typeof url === 'string' && url.includes('/api/v1/ingest/pdf')) {
        return new Promise<Response>(resolve => {
          resolveUpload = resolve;
        });
      }
      return originalFetch(url);
    });

    // Also delay collection creation to keep component in uploading state
    (api.documents.createCollection as ReturnType<typeof vi.fn>).mockImplementation(
      () => new Promise(() => {}) // Never resolves - we control timing
    );

    render(
      <MultiDocumentCollectionUpload
        gameId="game-123"
        gameName="Test Game"
      />
    );

    // Fill form
    const nameInput = screen.getByLabelText(/collection name/i);
    await user.type(nameInput, 'My Collection');

    const file = createMockPdfFile('rules.pdf');
    const fileInput = document.getElementById('file-upload') as HTMLInputElement;
    await userEvent.upload(fileInput, file);

    // Act - Submit
    const submitButton = screen.getByRole('button', { name: /create collection/i });
    await user.click(submitButton);

    // Assert - Upload progress button should appear during upload
    await waitFor(() => {
      // Check for the uploading button which appears during upload phase
      expect(screen.getByRole('button', { name: /uploading/i })).toBeInTheDocument();
    });

    // Also verify the progress Alert is shown
    expect(screen.getByText(/uploading pdfs/i)).toBeInTheDocument();

    // Clean up - resolve the promise so the test doesn't hang
    resolveUpload!({
      ok: true,
      status: 201,
      json: () => Promise.resolve({
        documentId: 'pdf-1',
        fileName: 'test.pdf',
        status: 'pending',
      }),
    } as Response);
  });

  // ============================================================================
  // TEST 8: Success state
  // ============================================================================
  it('should show success message after successful upload', async () => {
    // Arrange
    const user = userEvent.setup();
    const mockOnSuccess = vi.fn();

    render(
      <MultiDocumentCollectionUpload
        gameId="game-123"
        gameName="Test Game"
        onSuccess={mockOnSuccess}
      />
    );

    // Fill form
    const nameInput = screen.getByLabelText(/collection name/i);
    await user.type(nameInput, 'My Collection');

    const file = createMockPdfFile('rules.pdf');
    const fileInput = document.getElementById('file-upload') as HTMLInputElement;
    await userEvent.upload(fileInput, file);

    // Act - Submit
    const submitButton = screen.getByRole('button', { name: /create collection/i });
    await user.click(submitButton);

    // Assert
    await waitFor(() => {
      expect(screen.getByText(/collection created successfully/i)).toBeInTheDocument();
    });
    expect(mockOnSuccess).toHaveBeenCalled();
  });

  // ============================================================================
  // TEST 9: Error state on upload failure
  // ============================================================================
  it('should show error message on upload failure', async () => {
    // Arrange
    const user = userEvent.setup();

    // Mock fetch to return error
    global.fetch = vi.fn().mockImplementation((url: string) => {
      if (typeof url === 'string' && url.includes('/api/v1/ingest/pdf')) {
        return Promise.resolve({
          ok: false,
          status: 500,
          statusText: 'Internal Server Error',
          json: () => Promise.resolve({ error: 'Server error during upload' }),
        } as Response);
      }
      return originalFetch(url);
    });

    render(
      <MultiDocumentCollectionUpload
        gameId="game-123"
        gameName="Test Game"
      />
    );

    // Fill form
    const nameInput = screen.getByLabelText(/collection name/i);
    await user.type(nameInput, 'My Collection');

    const file = createMockPdfFile('rules.pdf');
    const fileInput = document.getElementById('file-upload') as HTMLInputElement;
    await userEvent.upload(fileInput, file);

    // Act
    const submitButton = screen.getByRole('button', { name: /create collection/i });
    await user.click(submitButton);

    // Assert
    await waitFor(() => {
      expect(screen.getByText(/upload failed/i)).toBeInTheDocument();
    });
  });

  // ============================================================================
  // TEST 10: Cancel button
  // ============================================================================
  it('should call onCancel when cancel button clicked', async () => {
    // Arrange
    const user = userEvent.setup();
    const mockOnCancel = vi.fn();

    render(
      <MultiDocumentCollectionUpload
        gameId="game-123"
        gameName="Test Game"
        onCancel={mockOnCancel}
      />
    );

    // Act
    const cancelButton = screen.getByRole('button', { name: /cancel/i });
    await user.click(cancelButton);

    // Assert
    expect(mockOnCancel).toHaveBeenCalled();
  });

  // ============================================================================
  // TEST 11: Reset form after success
  // ============================================================================
  it('should provide reset option after successful upload', async () => {
    // Arrange
    const user = userEvent.setup();

    render(
      <MultiDocumentCollectionUpload
        gameId="game-123"
        gameName="Test Game"
      />
    );

    // Fill and submit form
    const nameInput = screen.getByLabelText(/collection name/i);
    await user.type(nameInput, 'My Collection');

    const file = createMockPdfFile('rules.pdf');
    const fileInput = document.getElementById('file-upload') as HTMLInputElement;
    await userEvent.upload(fileInput, file);

    const submitButton = screen.getByRole('button', { name: /create collection/i });
    await user.click(submitButton);

    // Wait for success
    await waitFor(() => {
      expect(screen.getByText(/collection created successfully/i)).toBeInTheDocument();
    });

    // Act - Click reset
    const resetButton = screen.getByRole('button', { name: /create another collection/i });
    await user.click(resetButton);

    // Assert - Form should be reset
    const newNameInput = screen.getByLabelText(/collection name/i);
    expect(newNameInput).toHaveValue('');
  });

  // ============================================================================
  // TEST 12: Try again after error
  // ============================================================================
  it('should provide try again option after error', async () => {
    // Arrange
    const user = userEvent.setup();

    // Mock fetch to return error
    global.fetch = vi.fn().mockImplementation((url: string) => {
      if (typeof url === 'string' && url.includes('/api/v1/ingest/pdf')) {
        return Promise.resolve({
          ok: false,
          status: 500,
          statusText: 'Internal Server Error',
          json: () => Promise.resolve({ error: 'Failed' }),
        } as Response);
      }
      return originalFetch(url);
    });

    render(
      <MultiDocumentCollectionUpload
        gameId="game-123"
        gameName="Test Game"
      />
    );

    // Fill and submit
    const nameInput = screen.getByLabelText(/collection name/i);
    await user.type(nameInput, 'My Collection');

    const file = createMockPdfFile('rules.pdf');
    const fileInput = document.getElementById('file-upload') as HTMLInputElement;
    await userEvent.upload(fileInput, file);

    const submitButton = screen.getByRole('button', { name: /create collection/i });
    await user.click(submitButton);

    // Wait for error
    await waitFor(() => {
      expect(screen.getByText(/upload failed/i)).toBeInTheDocument();
    });

    // Assert - Try again button available
    expect(screen.getByRole('button', { name: /try again/i })).toBeInTheDocument();
  });

  // ============================================================================
  // TEST 13: Disabled inputs during upload
  // ============================================================================
  it('should disable inputs during upload', async () => {
    // Arrange
    const user = userEvent.setup();

    // Make upload slow
    global.fetch = vi.fn().mockImplementation((url: string) => {
      if (typeof url === 'string' && url.includes('/api/v1/ingest/pdf')) {
        return new Promise(resolve => {
          setTimeout(() => {
            resolve({
              ok: true,
              status: 201,
              json: () => Promise.resolve({
                documentId: 'pdf-1',
                status: 'pending',
              }),
            } as Response);
          }, 200);
        });
      }
      return originalFetch(url);
    });

    render(
      <MultiDocumentCollectionUpload
        gameId="game-123"
        gameName="Test Game"
      />
    );

    // Fill form
    const nameInput = screen.getByLabelText(/collection name/i);
    await user.type(nameInput, 'My Collection');

    const file = createMockPdfFile('rules.pdf');
    const fileInput = document.getElementById('file-upload') as HTMLInputElement;
    await userEvent.upload(fileInput, file);

    // Act - Submit
    const submitButton = screen.getByRole('button', { name: /create collection/i });
    await user.click(submitButton);

    // Assert - Inputs should be disabled during upload
    await waitFor(() => {
      expect(screen.getByLabelText(/collection name/i)).toBeDisabled();
    });
  });

  // ============================================================================
  // TEST 14: Multiple files upload
  // ============================================================================
  it('should handle multiple file uploads', async () => {
    // Arrange
    const user = userEvent.setup();
    const mockOnSuccess = vi.fn();

    render(
      <MultiDocumentCollectionUpload
        gameId="game-123"
        gameName="Test Game"
        onSuccess={mockOnSuccess}
      />
    );

    // Fill form with multiple files
    const nameInput = screen.getByLabelText(/collection name/i);
    await user.type(nameInput, 'Multi-file Collection');

    const files = [
      createMockPdfFile('rules.pdf'),
      createMockPdfFile('expansion.pdf'),
    ];
    const fileInput = document.getElementById('file-upload') as HTMLInputElement;
    await userEvent.upload(fileInput, files);

    // Act
    const submitButton = screen.getByRole('button', { name: /create collection/i });
    await user.click(submitButton);

    // Assert
    await waitFor(() => {
      expect(screen.getByText(/collection created successfully/i)).toBeInTheDocument();
    });
    expect(mockOnSuccess).toHaveBeenCalled();
  });

  // ============================================================================
  // TEST 15: Custom className support
  // ============================================================================
  it('should apply custom className', () => {
    // Arrange & Act
    const { container } = render(
      <MultiDocumentCollectionUpload
        gameId="game-123"
        gameName="Test Game"
        className="custom-upload-class"
      />
    );

    // Assert
    expect(container.firstChild).toHaveClass('custom-upload-class');
  });

  // ============================================================================
  // TEST 16: Creating collection progress
  // ============================================================================
  it('should show creating collection progress after uploads complete', async () => {
    // Arrange
    const user = userEvent.setup();
    let resolveCollection: (value: unknown) => void;

    // Slow collection creation - use a deferred promise we can control
    (api.documents.createCollection as ReturnType<typeof vi.fn>).mockImplementation(
      () => new Promise(resolve => {
        resolveCollection = resolve;
      })
    );

    render(
      <MultiDocumentCollectionUpload
        gameId="game-123"
        gameName="Test Game"
      />
    );

    // Fill form
    const nameInput = screen.getByLabelText(/collection name/i);
    await user.type(nameInput, 'My Collection');

    const file = createMockPdfFile('rules.pdf');
    const fileInput = document.getElementById('file-upload') as HTMLInputElement;
    await userEvent.upload(fileInput, file);

    // Act
    const submitButton = screen.getByRole('button', { name: /create collection/i });
    await user.click(submitButton);

    // Assert - Creating collection message should appear after PDF uploads complete
    // but before collection creation resolves
    // Use getAllByText since multiple elements contain "creating collection"
    await waitFor(() => {
      const elements = screen.getAllByText(/creating collection/i);
      expect(elements.length).toBeGreaterThan(0);
    });

    // Clean up - resolve the promise so the test doesn't hang
    resolveCollection!({ id: 'coll-1', name: 'Test Collection' });
  });

  // ============================================================================
  // TEST 17: Done button after success
  // ============================================================================
  it('should show Done button after success when onCancel provided', async () => {
    // Arrange
    const user = userEvent.setup();
    const mockOnCancel = vi.fn();

    render(
      <MultiDocumentCollectionUpload
        gameId="game-123"
        gameName="Test Game"
        onCancel={mockOnCancel}
      />
    );

    // Fill and submit
    const nameInput = screen.getByLabelText(/collection name/i);
    await user.type(nameInput, 'My Collection');

    const file = createMockPdfFile('rules.pdf');
    const fileInput = document.getElementById('file-upload') as HTMLInputElement;
    await userEvent.upload(fileInput, file);

    const submitButton = screen.getByRole('button', { name: /create collection/i });
    await user.click(submitButton);

    // Wait for success
    await waitFor(() => {
      expect(screen.getByText(/collection created successfully/i)).toBeInTheDocument();
    });

    // Act
    const doneButton = screen.getByRole('button', { name: /done/i });
    await user.click(doneButton);

    // Assert
    expect(mockOnCancel).toHaveBeenCalled();
  });

  // ============================================================================
  // TEST 18: Input max lengths
  // ============================================================================
  it('should enforce max length on inputs', () => {
    // Arrange & Act
    render(
      <MultiDocumentCollectionUpload
        gameId="game-123"
        gameName="Test Game"
      />
    );

    // Assert
    const nameInput = screen.getByLabelText(/collection name/i);
    expect(nameInput).toHaveAttribute('maxLength', '200');

    const descInput = screen.getByLabelText(/description/i);
    expect(descInput).toHaveAttribute('maxLength', '1000');
  });
});
