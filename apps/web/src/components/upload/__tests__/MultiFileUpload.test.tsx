/**
 * MultiFileUpload Tests - Issue #2764 Sprint 4
 *
 * Tests for MultiFileUpload component:
 * - File selection and validation
 * - Drag and drop support
 * - Upload queue integration
 * - Auto/manual upload modes
 * - Validation errors display
 * - Upload summary display
 *
 * Pattern: Vitest + React Testing Library + MSW
 * Coverage target: 370 lines (85%+)
 */

import { render, screen, fireEvent, waitFor } from '@testing-library/react';
import userEvent from '@testing-library/user-event';
import { describe, it, expect, vi, beforeEach, afterEach } from 'vitest';
import React from 'react';
import { http, HttpResponse, delay } from 'msw';
import { server } from '@/__tests__/mocks/server';

import { MultiFileUpload } from '../MultiFileUpload';

// API base for MSW handlers
const API_BASE = process.env.NEXT_PUBLIC_API_BASE || 'http://localhost:8080';

// Helper to create mock PDF files
function createMockPdfFile(name: string, sizeInKB: number = 100): File {
  // PDF header
  const pdfHeader = '%PDF-';
  const contentSize = Math.max(0, sizeInKB * 1024 - pdfHeader.length);
  const content = pdfHeader + new Array(contentSize).fill('a').join('');
  return new File([content], name, { type: 'application/pdf' });
}

// Helper to create non-PDF files
function createNonPdfFile(name: string): File {
  return new File(['content'], name, { type: 'text/plain' });
}

// Helper to create oversized file
function createOversizedFile(name: string): File {
  // Create a file larger than 100 MB
  const sizeInBytes = 105 * 1024 * 1024;
  const content = '%PDF-' + new Array(sizeInBytes).fill('a').join('');
  return new File([content], name, { type: 'application/pdf' });
}

describe('MultiFileUpload - Issue #2764 Sprint 4', () => {
  beforeEach(() => {
    // Default successful upload handler
    server.use(
      http.post(`${API_BASE}/api/v1/ingest/pdf`, async () => {
        return HttpResponse.json({
          documentId: `doc-${Date.now()}`,
          fileName: 'test.pdf',
          status: 'pending',
          message: 'PDF uploaded successfully',
        }, { status: 201 });
      }),
      // Progress endpoint - immediately return completed status
      http.get(`${API_BASE}/api/v1/pdfs/:documentId/progress`, () => {
        return HttpResponse.json({
          currentStep: 'Completed',
          percentComplete: 100,
          elapsedTime: 'PT1S',
          pagesProcessed: 1,
          totalPages: 1,
          startedAt: new Date().toISOString(),
          completedAt: new Date().toISOString(),
        });
      })
    );
  });

  afterEach(() => {
    server.resetHandlers();
  });

  // ============================================================================
  // TEST 1: Initial rendering
  // ============================================================================
  it('should render multi-file upload component', () => {
    // Arrange & Act
    render(
      <MultiFileUpload
        gameId="game-123"
        gameName="Test Game"
        language="en"
      />
    );

    // Assert
    expect(screen.getByTestId('multi-file-upload')).toBeInTheDocument();
    expect(screen.getByText('Multi-File Upload')).toBeInTheDocument();
  });

  // ============================================================================
  // TEST 2: Display game info badge
  // ============================================================================
  it('should display game info badge', () => {
    // Arrange & Act
    render(
      <MultiFileUpload
        gameId="game-123"
        gameName="Chess"
        language="en"
      />
    );

    // Assert
    expect(screen.getByTestId('game-info-badge')).toBeInTheDocument();
    expect(screen.getByText(/target game: chess \(game-123\)/i)).toBeInTheDocument();
  });

  // ============================================================================
  // TEST 3: Drag and drop zone
  // ============================================================================
  it('should render drag and drop zone', () => {
    // Arrange & Act
    render(
      <MultiFileUpload
        gameId="game-123"
        gameName="Test Game"
        language="en"
      />
    );

    // Assert
    expect(screen.getByRole('button', { name: /click to browse files or drag and drop/i })).toBeInTheDocument();
    expect(screen.getByText(/drag and drop pdf files here/i)).toBeInTheDocument();
  });

  // ============================================================================
  // TEST 4: Select Files button
  // ============================================================================
  it('should have Select Files button', () => {
    // Arrange & Act
    render(
      <MultiFileUpload
        gameId="game-123"
        gameName="Test Game"
        language="en"
      />
    );

    // Assert
    expect(screen.getByRole('button', { name: /select files/i })).toBeInTheDocument();
  });

  // ============================================================================
  // TEST 5: File input is hidden
  // ============================================================================
  it('should have hidden file input with correct attributes', () => {
    // Arrange & Act
    render(
      <MultiFileUpload
        gameId="game-123"
        gameName="Test Game"
        language="en"
      />
    );

    // Assert
    const fileInput = screen.getByLabelText(/file input for pdf upload/i);
    expect(fileInput).toHaveClass('hidden');
    expect(fileInput).toHaveAttribute('type', 'file');
    expect(fileInput).toHaveAttribute('accept', 'application/pdf');
    expect(fileInput).toHaveAttribute('multiple');
  });

  // ============================================================================
  // TEST 6: Display max file info
  // ============================================================================
  it('should display max file count and size info', () => {
    // Arrange & Act
    render(
      <MultiFileUpload
        gameId="game-123"
        gameName="Test Game"
        language="en"
      />
    );

    // Assert
    expect(screen.getByText(/up to 20 files, max 100 MB each/i)).toBeInTheDocument();
  });

  // ============================================================================
  // TEST 7: Handle valid file selection
  // ============================================================================
  it('should accept valid PDF files', async () => {
    // Arrange
    const mockOnQueueAdd = vi.fn();
    render(
      <MultiFileUpload
        gameId="game-123"
        gameName="Test Game"
        language="en"
        onQueueAdd={mockOnQueueAdd}
      />
    );

    const file = createMockPdfFile('test.pdf');
    const fileInput = screen.getByLabelText(/file input for pdf upload/i);

    // Act
    await userEvent.upload(fileInput, file);

    // Assert - File should be added to queue
    await waitFor(() => {
      expect(mockOnQueueAdd).toHaveBeenCalled();
    });
  });

  // ============================================================================
  // TEST 8: Validate non-PDF files
  // ============================================================================
  it('should show error for non-PDF files', async () => {
    // Arrange
    render(
      <MultiFileUpload
        gameId="game-123"
        gameName="Test Game"
        language="en"
      />
    );

    const file = createNonPdfFile('test.txt');
    const fileInput = screen.getByLabelText(/file input for pdf upload/i) as HTMLInputElement;

    // Act - Use fireEvent.change to bypass accept attribute filtering
    Object.defineProperty(fileInput, 'files', {
      value: [file],
      writable: false,
      configurable: true,
    });
    fireEvent.change(fileInput);

    // Assert - Validation error should appear
    await waitFor(() => {
      expect(screen.getByRole('alert')).toHaveTextContent(/invalid file type/i);
    });
  });

  // ============================================================================
  // TEST 9: Too many files error
  // ============================================================================
  it('should show error when too many files selected', async () => {
    // Arrange
    render(
      <MultiFileUpload
        gameId="game-123"
        gameName="Test Game"
        language="en"
      />
    );

    // Create 21 files (more than max 20) - use small files to avoid memory issues
    const files = Array.from({ length: 21 }, (_, i) => createMockPdfFile(`file${i}.pdf`, 1));
    const fileInput = screen.getByLabelText(/file input for pdf upload/i) as HTMLInputElement;

    // Act - Use fireEvent.change with Object.defineProperty
    Object.defineProperty(fileInput, 'files', {
      value: files,
      writable: false,
      configurable: true,
    });
    fireEvent.change(fileInput);

    // Assert
    await waitFor(() => {
      expect(screen.getByRole('alert')).toHaveTextContent(/too many files/i);
    });
  });

  // ============================================================================
  // TEST 10: Drag enter styling
  // ============================================================================
  it('should change styling on drag enter', () => {
    // Arrange
    render(
      <MultiFileUpload
        gameId="game-123"
        gameName="Test Game"
        language="en"
      />
    );

    const dropZone = screen.getByRole('button', { name: /click to browse files/i });

    // Act
    fireEvent.dragEnter(dropZone);

    // Assert - Text should change
    expect(screen.getByText('Drop files here')).toBeInTheDocument();
  });

  // ============================================================================
  // TEST 11: Drag leave resets styling
  // ============================================================================
  it('should reset styling on drag leave', () => {
    // Arrange
    render(
      <MultiFileUpload
        gameId="game-123"
        gameName="Test Game"
        language="en"
      />
    );

    const dropZone = screen.getByRole('button', { name: /click to browse files/i });

    // Act
    fireEvent.dragEnter(dropZone);
    fireEvent.dragLeave(dropZone);

    // Assert
    expect(screen.getByText(/drag and drop pdf files here/i)).toBeInTheDocument();
  });

  // ============================================================================
  // TEST 12: Drop files
  // ============================================================================
  it('should handle file drop', async () => {
    // Arrange
    const mockOnQueueAdd = vi.fn();
    render(
      <MultiFileUpload
        gameId="game-123"
        gameName="Test Game"
        language="en"
        onQueueAdd={mockOnQueueAdd}
      />
    );

    const file = createMockPdfFile('dropped.pdf');
    const dropZone = screen.getByRole('button', { name: /click to browse files/i });

    // Create DataTransfer mock
    const dataTransfer = {
      files: [file],
      types: ['Files'],
      items: [{ kind: 'file', type: 'application/pdf', getAsFile: () => file }],
    };

    // Act
    fireEvent.drop(dropZone, { dataTransfer });

    // Assert
    await waitFor(() => {
      expect(mockOnQueueAdd).toHaveBeenCalled();
    });
  });

  // ============================================================================
  // TEST 13: Keyboard accessibility
  // ============================================================================
  it('should trigger file input on Enter key', () => {
    // Arrange
    render(
      <MultiFileUpload
        gameId="game-123"
        gameName="Test Game"
        language="en"
      />
    );

    const dropZone = screen.getByRole('button', { name: /click to browse files/i });
    const fileInput = screen.getByLabelText(/file input for pdf upload/i);
    const clickSpy = vi.spyOn(fileInput, 'click');

    // Act
    fireEvent.keyDown(dropZone, { key: 'Enter' });

    // Assert
    expect(clickSpy).toHaveBeenCalled();
  });

  // ============================================================================
  // TEST 14: Keyboard accessibility - Space
  // ============================================================================
  it('should trigger file input on Space key', () => {
    // Arrange
    render(
      <MultiFileUpload
        gameId="game-123"
        gameName="Test Game"
        language="en"
      />
    );

    const dropZone = screen.getByRole('button', { name: /click to browse files/i });
    const fileInput = screen.getByLabelText(/file input for pdf upload/i);
    const clickSpy = vi.spyOn(fileInput, 'click');

    // Act
    fireEvent.keyDown(dropZone, { key: ' ' });

    // Assert
    expect(clickSpy).toHaveBeenCalled();
  });

  // ============================================================================
  // TEST 15: Manual upload mode - start button
  // ============================================================================
  it('should show start upload button in manual mode with pending files', async () => {
    // Arrange
    render(
      <MultiFileUpload
        gameId="game-123"
        gameName="Test Game"
        language="en"
        autoUpload={false}
      />
    );

    const file = createMockPdfFile('test.pdf');
    const fileInput = screen.getByLabelText(/file input for pdf upload/i);

    // Act - Add file
    await userEvent.upload(fileInput, file);

    // Assert - Start button should appear
    await waitFor(() => {
      expect(screen.getByTestId('start-upload-button')).toBeInTheDocument();
    });
  });

  // ============================================================================
  // TEST 16: onUploadComplete callback
  // ============================================================================
  it('should call onUploadComplete when uploads finish', async () => {
    // Arrange
    const mockOnUploadComplete = vi.fn();
    render(
      <MultiFileUpload
        gameId="game-123"
        gameName="Test Game"
        language="en"
        onUploadComplete={mockOnUploadComplete}
      />
    );

    const file = createMockPdfFile('test.pdf');
    const fileInput = screen.getByLabelText(/file input for pdf upload/i);

    // Act
    await userEvent.upload(fileInput, file);

    // Assert - After upload completes
    await waitFor(() => {
      expect(mockOnUploadComplete).toHaveBeenCalled();
    }, { timeout: 5000 });
  });

  // ============================================================================
  // TEST 17: Data attributes
  // ============================================================================
  it('should have correct data attributes', () => {
    // Arrange & Act
    render(
      <MultiFileUpload
        gameId="game-123"
        gameName="Test Game"
        language="en"
      />
    );

    // Assert
    const container = screen.getByTestId('multi-file-upload');
    expect(container).toHaveAttribute('data-game-id', 'game-123');
    expect(container).toHaveAttribute('data-game-name', 'Test Game');
  });

  // ============================================================================
  // TEST 18: Empty file validation
  // ============================================================================
  it('should reject empty files', async () => {
    // Arrange
    render(
      <MultiFileUpload
        gameId="game-123"
        gameName="Test Game"
        language="en"
      />
    );

    // Create empty file
    const emptyFile = new File([], 'empty.pdf', { type: 'application/pdf' });
    const fileInput = screen.getByLabelText(/file input for pdf upload/i) as HTMLInputElement;

    // Act - Use fireEvent.change to bypass any filtering
    Object.defineProperty(fileInput, 'files', {
      value: [emptyFile],
      writable: false,
      configurable: true,
    });
    fireEvent.change(fileInput);

    // Assert
    await waitFor(() => {
      expect(screen.getByRole('alert')).toHaveTextContent(/empty file/i);
    });
  });

  // ============================================================================
  // TEST 19: Invalid PDF magic bytes
  // ============================================================================
  it('should reject files without PDF magic bytes', async () => {
    // Arrange
    render(
      <MultiFileUpload
        gameId="game-123"
        gameName="Test Game"
        language="en"
      />
    );

    // Create file with wrong magic bytes but PDF type
    const fakeFile = new File(['NOTAPDF'], 'fake.pdf', { type: 'application/pdf' });
    const fileInput = screen.getByLabelText(/file input for pdf upload/i) as HTMLInputElement;

    // Act - Use fireEvent.change to bypass any filtering
    Object.defineProperty(fileInput, 'files', {
      value: [fakeFile],
      writable: false,
      configurable: true,
    });
    fireEvent.change(fileInput);

    // Assert
    await waitFor(() => {
      expect(screen.getByRole('alert')).toHaveTextContent(/invalid pdf format/i);
    });
  });

  // ============================================================================
  // TEST 20: Multiple valid files
  // ============================================================================
  it('should handle multiple valid files', async () => {
    // Arrange
    const mockOnQueueAdd = vi.fn();
    render(
      <MultiFileUpload
        gameId="game-123"
        gameName="Test Game"
        language="en"
        onQueueAdd={mockOnQueueAdd}
      />
    );

    const files = [
      createMockPdfFile('file1.pdf'),
      createMockPdfFile('file2.pdf'),
      createMockPdfFile('file3.pdf'),
    ];
    const fileInput = screen.getByLabelText(/file input for pdf upload/i);

    // Act
    await userEvent.upload(fileInput, files);

    // Assert
    await waitFor(() => {
      expect(mockOnQueueAdd).toHaveBeenCalled();
      const addedItems = mockOnQueueAdd.mock.calls[0][0];
      expect(addedItems).toHaveLength(3);
    });
  });

  // ============================================================================
  // TEST 21: Mixed valid and invalid files
  // ============================================================================
  it('should accept valid files while showing errors for invalid ones', async () => {
    // Arrange
    const mockOnQueueAdd = vi.fn();
    render(
      <MultiFileUpload
        gameId="game-123"
        gameName="Test Game"
        language="en"
        onQueueAdd={mockOnQueueAdd}
      />
    );

    const files = [
      createMockPdfFile('valid.pdf'),
      createNonPdfFile('invalid.txt'),
    ];
    const fileInput = screen.getByLabelText(/file input for pdf upload/i) as HTMLInputElement;

    // Act - Use fireEvent.change to bypass accept attribute filtering
    Object.defineProperty(fileInput, 'files', {
      value: files,
      writable: false,
      configurable: true,
    });
    fireEvent.change(fileInput);

    // Assert - Should show error and add valid file
    await waitFor(() => {
      expect(screen.getByRole('alert')).toHaveTextContent(/invalid file type/i);
      expect(mockOnQueueAdd).toHaveBeenCalled();
    });
  });

  // ============================================================================
  // TEST 22: Clear validation errors on new selection
  // ============================================================================
  it('should clear validation errors on new file selection', async () => {
    // Arrange
    render(
      <MultiFileUpload
        gameId="game-123"
        gameName="Test Game"
        language="en"
      />
    );

    const fileInput = screen.getByLabelText(/file input for pdf upload/i) as HTMLInputElement;

    // Add invalid file first - Use fireEvent.change to bypass accept attribute
    Object.defineProperty(fileInput, 'files', {
      value: [createNonPdfFile('invalid.txt')],
      writable: false,
      configurable: true,
    });
    fireEvent.change(fileInput);

    // Verify error exists
    await waitFor(() => {
      expect(screen.getByRole('alert')).toBeInTheDocument();
    });

    // Act - Add valid file
    Object.defineProperty(fileInput, 'files', {
      value: [createMockPdfFile('valid.pdf')],
      writable: false,
      configurable: true,
    });
    fireEvent.change(fileInput);

    // Assert - Previous errors should be cleared
    await waitFor(() => {
      // Either no alert or only the new error (if any)
      const alerts = screen.queryAllByRole('alert');
      expect(alerts.every(a => !a.textContent?.includes('invalid.txt'))).toBe(true);
    });
  });

  // ============================================================================
  // TEST 23: Test observability hooks
  // ============================================================================
  it('should call test observability hooks', async () => {
    // Arrange
    const mockOnUploadStart = vi.fn();
    const mockOnUploadSuccess = vi.fn();

    render(
      <MultiFileUpload
        gameId="game-123"
        gameName="Test Game"
        language="en"
        onUploadStart={mockOnUploadStart}
        onUploadSuccess={mockOnUploadSuccess}
      />
    );

    const file = createMockPdfFile('test.pdf');
    const fileInput = screen.getByLabelText(/file input for pdf upload/i);

    // Act
    await userEvent.upload(fileInput, file);

    // Assert
    await waitFor(() => {
      expect(mockOnUploadStart).toHaveBeenCalled();
    }, { timeout: 5000 });

    await waitFor(() => {
      expect(mockOnUploadSuccess).toHaveBeenCalled();
    }, { timeout: 5000 });
  });
});
