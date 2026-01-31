/**
 * FileUploadList Tests - Issue #2764 Sprint 4
 *
 * Tests for FileUploadList component:
 * - File selection and validation
 * - Document type selection per file
 * - Drag and drop reordering
 * - File removal
 * - Max files limit
 * - File size validation
 *
 * Pattern: Vitest + React Testing Library
 * Coverage target: 268 lines (85%+)
 */

import { render, screen, within, fireEvent } from '@testing-library/react';
import userEvent from '@testing-library/user-event';
import { describe, it, expect, vi, beforeEach } from 'vitest';
import React from 'react';

import { FileUploadList, type FileUploadItem } from '../FileUploadList';
import type { DocumentType } from '../DocumentTypeSelector';

// Helper to create mock files
function createMockFile(name: string, size: number, type = 'application/pdf'): File {
  const content = new Array(size).fill('a').join('');
  return new File([content], name, { type });
}

// Helper to create mock file items
function createMockFileItem(
  id: string,
  fileName: string,
  size: number,
  options?: Partial<FileUploadItem>
): FileUploadItem {
  return {
    id,
    file: createMockFile(fileName, size),
    documentType: 'base' as DocumentType,
    sortOrder: 0,
    ...options,
  };
}

describe('FileUploadList - Issue #2764 Sprint 4', () => {
  // ============================================================================
  // TEST 1: Empty state rendering
  // ============================================================================
  it('should render empty state when no files', () => {
    // Arrange & Act
    render(<FileUploadList files={[]} onChange={vi.fn()} />);

    // Assert
    expect(screen.getByText(/no files added yet/i)).toBeInTheDocument();
    expect(screen.getByText(/click "add pdf files" to begin/i)).toBeInTheDocument();
  });

  // ============================================================================
  // TEST 2: Display file count status
  // ============================================================================
  it('should display file count and limits', () => {
    // Arrange
    const files = [createMockFileItem('1', 'test.pdf', 1024)];

    // Act
    render(<FileUploadList files={files} onChange={vi.fn()} maxFiles={5} maxSizeMB={50} />);

    // Assert
    expect(screen.getByText('1/5 files (max 50MB each)')).toBeInTheDocument();
  });

  // ============================================================================
  // TEST 3: Render file list
  // ============================================================================
  it('should render list of files', () => {
    // Arrange
    const files = [
      createMockFileItem('1', 'rules.pdf', 1024 * 100, { sortOrder: 0 }),
      createMockFileItem('2', 'expansion.pdf', 1024 * 200, { sortOrder: 1 }),
    ];

    // Act
    render(<FileUploadList files={files} onChange={vi.fn()} />);

    // Assert
    expect(screen.getByText('rules.pdf')).toBeInTheDocument();
    expect(screen.getByText('expansion.pdf')).toBeInTheDocument();
    expect(screen.getByRole('list', { name: /uploaded files/i })).toBeInTheDocument();
  });

  // ============================================================================
  // TEST 4: Display file sizes
  // ============================================================================
  it('should display formatted file sizes', () => {
    // Arrange
    const files = [
      createMockFileItem('1', 'small.pdf', 500), // 500 B
      createMockFileItem('2', 'medium.pdf', 1024 * 100), // 100 KB
      createMockFileItem('3', 'large.pdf', 1024 * 1024 * 5), // 5 MB
    ];

    // Act
    render(<FileUploadList files={files} onChange={vi.fn()} />);

    // Assert
    expect(screen.getByText('500 B')).toBeInTheDocument();
    expect(screen.getByText('100.0 KB')).toBeInTheDocument();
    expect(screen.getByText('5.0 MB')).toBeInTheDocument();
  });

  // ============================================================================
  // TEST 5: File input is hidden
  // ============================================================================
  it('should have hidden file input', () => {
    // Arrange & Act
    render(<FileUploadList files={[]} onChange={vi.fn()} />);

    // Assert
    const fileInput = document.getElementById('file-upload');
    expect(fileInput).toHaveClass('hidden');
    expect(fileInput).toHaveAttribute('type', 'file');
    expect(fileInput).toHaveAttribute('accept', 'application/pdf');
  });

  // ============================================================================
  // TEST 6: Add files via file input
  // ============================================================================
  it('should call onChange when files are added via input', async () => {
    // Arrange
    const mockOnChange = vi.fn();
    render(<FileUploadList files={[]} onChange={mockOnChange} />);

    const file = createMockFile('test.pdf', 1024);
    const fileInput = document.getElementById('file-upload') as HTMLInputElement;

    // Act
    await userEvent.upload(fileInput, file);

    // Assert
    expect(mockOnChange).toHaveBeenCalled();
    const newFiles = mockOnChange.mock.calls[0][0];
    expect(newFiles).toHaveLength(1);
    expect(newFiles[0].file.name).toBe('test.pdf');
    expect(newFiles[0].documentType).toBe('base');
  });

  // ============================================================================
  // TEST 7: Validate non-PDF files
  // ============================================================================
  it('should add error for non-PDF files', async () => {
    // Arrange
    const mockOnChange = vi.fn();
    render(<FileUploadList files={[]} onChange={mockOnChange} />);

    // Create a file with PDF extension but wrong MIME type to simulate invalid PDF
    // Note: The browser/JSDOM respects accept="application/pdf" so we need to fake it
    const file = new File(['not a real pdf'], 'test.pdf', { type: 'text/plain' });
    const fileInput = document.getElementById('file-upload') as HTMLInputElement;

    // Act - Use fireEvent to bypass accept attribute filtering
    Object.defineProperty(fileInput, 'files', {
      value: [file],
      writable: false,
    });
    fireEvent.change(fileInput);

    // Assert
    expect(mockOnChange).toHaveBeenCalled();
    const newFiles = mockOnChange.mock.calls[0][0];
    expect(newFiles[0].error).toBe('File must be a PDF');
  });

  // ============================================================================
  // TEST 8: Validate file size
  // ============================================================================
  it('should add error for oversized files', async () => {
    // Arrange
    const mockOnChange = vi.fn();
    render(<FileUploadList files={[]} onChange={mockOnChange} maxSizeMB={1} />);

    // Create a file larger than 1MB
    const file = createMockFile('large.pdf', 1024 * 1024 * 2); // 2MB
    const fileInput = document.getElementById('file-upload') as HTMLInputElement;

    // Act
    await userEvent.upload(fileInput, file);

    // Assert
    const newFiles = mockOnChange.mock.calls[0][0];
    expect(newFiles[0].error).toBe('File size exceeds 1MB limit');
  });

  // ============================================================================
  // TEST 9: Display error message for invalid files
  // ============================================================================
  it('should display error message for files with errors', () => {
    // Arrange
    const files = [
      createMockFileItem('1', 'invalid.txt', 1024, { error: 'File must be a PDF' }),
    ];

    // Act
    render(<FileUploadList files={files} onChange={vi.fn()} />);

    // Assert
    expect(screen.getByText('File must be a PDF')).toBeInTheDocument();
  });

  // ============================================================================
  // TEST 10: Remove file from list
  // ============================================================================
  it('should call onChange with file removed when remove button clicked', async () => {
    // Arrange
    const user = userEvent.setup();
    const mockOnChange = vi.fn();
    const files = [
      createMockFileItem('1', 'rules.pdf', 1024, { sortOrder: 0 }),
      createMockFileItem('2', 'expansion.pdf', 1024, { sortOrder: 1 }),
    ];

    render(<FileUploadList files={files} onChange={mockOnChange} />);

    // Act
    const removeButton = screen.getByRole('button', { name: /remove rules\.pdf/i });
    await user.click(removeButton);

    // Assert
    expect(mockOnChange).toHaveBeenCalled();
    const updatedFiles = mockOnChange.mock.calls[0][0];
    expect(updatedFiles).toHaveLength(1);
    expect(updatedFiles[0].file.name).toBe('expansion.pdf');
    expect(updatedFiles[0].sortOrder).toBe(0); // Reordered
  });

  // ============================================================================
  // TEST 11: Max files alert
  // ============================================================================
  it('should show alert when max files exceeded', async () => {
    // Arrange
    const mockOnChange = vi.fn();
    const alertSpy = vi.spyOn(window, 'alert').mockImplementation(() => {});
    const existingFiles = [
      createMockFileItem('1', 'file1.pdf', 1024, { sortOrder: 0 }),
      createMockFileItem('2', 'file2.pdf', 1024, { sortOrder: 1 }),
    ];

    render(<FileUploadList files={existingFiles} onChange={mockOnChange} maxFiles={3} />);

    // Try to add 2 more files (would exceed max of 3)
    const fileInput = document.getElementById('file-upload') as HTMLInputElement;
    const newFiles = [
      createMockFile('file3.pdf', 1024),
      createMockFile('file4.pdf', 1024),
    ];

    // Act
    await userEvent.upload(fileInput, newFiles);

    // Assert
    expect(alertSpy).toHaveBeenCalledWith('Maximum 3 files allowed');
    expect(mockOnChange).not.toHaveBeenCalled();

    alertSpy.mockRestore();
  });

  // ============================================================================
  // TEST 12: Disable add button when max reached
  // ============================================================================
  it('should disable add button when max files reached', () => {
    // Arrange
    const files = [
      createMockFileItem('1', 'file1.pdf', 1024, { sortOrder: 0 }),
      createMockFileItem('2', 'file2.pdf', 1024, { sortOrder: 1 }),
    ];

    // Act
    render(<FileUploadList files={files} onChange={vi.fn()} maxFiles={2} />);

    // Assert
    const addButton = screen.getByRole('button', { name: /add pdf files/i });
    expect(addButton).toBeDisabled();
  });

  // ============================================================================
  // TEST 13: Document type selector per file
  // ============================================================================
  it('should display document type selector for each file', () => {
    // Arrange
    const files = [createMockFileItem('1', 'rules.pdf', 1024)];

    // Act
    render(<FileUploadList files={files} onChange={vi.fn()} />);

    // Assert
    expect(screen.getByText('Type:')).toBeInTheDocument();
    expect(screen.getByRole('combobox', { name: /select document type/i })).toBeInTheDocument();
  });

  // ============================================================================
  // TEST 14: Change document type
  // ============================================================================
  it('should call onChange when document type is changed', async () => {
    // Arrange
    const user = userEvent.setup();
    const mockOnChange = vi.fn();
    const files = [createMockFileItem('1', 'rules.pdf', 1024)];

    render(<FileUploadList files={files} onChange={mockOnChange} />);

    // Act - Open dropdown and select expansion
    const selector = screen.getByRole('combobox', { name: /select document type/i });
    await user.click(selector);

    const expansionOption = screen.getByText('Additional rules for expansions');
    await user.click(expansionOption);

    // Assert
    expect(mockOnChange).toHaveBeenCalled();
    const updatedFiles = mockOnChange.mock.calls[0][0];
    expect(updatedFiles[0].documentType).toBe('expansion');
  });

  // ============================================================================
  // TEST 15: Disable type selector for files with errors
  // ============================================================================
  it('should disable document type selector for files with errors', () => {
    // Arrange
    const files = [
      createMockFileItem('1', 'invalid.pdf', 1024, { error: 'File must be a PDF' }),
    ];

    // Act
    render(<FileUploadList files={files} onChange={vi.fn()} />);

    // Assert
    const selector = screen.getByRole('combobox', { name: /select document type/i });
    expect(selector).toBeDisabled();
  });

  // ============================================================================
  // TEST 16: Drag and drop reordering
  // ============================================================================
  it('should reorder files on drag and drop', () => {
    // Arrange
    const mockOnChange = vi.fn();
    const files = [
      createMockFileItem('1', 'first.pdf', 1024, { sortOrder: 0 }),
      createMockFileItem('2', 'second.pdf', 1024, { sortOrder: 1 }),
      createMockFileItem('3', 'third.pdf', 1024, { sortOrder: 2 }),
    ];

    render(<FileUploadList files={files} onChange={mockOnChange} />);

    // Get list items
    const listItems = screen.getAllByRole('listitem');
    const firstItem = listItems[0];
    const thirdItem = listItems[2];

    // Act - Simulate drag from first to third position
    fireEvent.dragStart(firstItem);
    fireEvent.dragOver(thirdItem, { preventDefault: () => {} });

    // Assert - onChange should be called with reordered files
    expect(mockOnChange).toHaveBeenCalled();
  });

  // ============================================================================
  // TEST 17: Custom className support
  // ============================================================================
  it('should apply custom className', () => {
    // Arrange & Act
    const { container } = render(
      <FileUploadList files={[]} onChange={vi.fn()} className="custom-class" />
    );

    // Assert
    expect(container.firstChild).toHaveClass('custom-class');
  });

  // ============================================================================
  // TEST 18: File title attribute for truncation
  // ============================================================================
  it('should have title attribute for long filenames', () => {
    // Arrange
    const longFileName = 'very-long-filename-that-would-be-truncated-in-ui.pdf';
    const files = [createMockFileItem('1', longFileName, 1024)];

    // Act
    render(<FileUploadList files={files} onChange={vi.fn()} />);

    // Assert
    const fileNameElement = screen.getByText(longFileName);
    expect(fileNameElement).toHaveAttribute('title', longFileName);
  });

  // ============================================================================
  // TEST 19: Multiple files upload
  // ============================================================================
  it('should handle multiple files at once', async () => {
    // Arrange
    const mockOnChange = vi.fn();
    render(<FileUploadList files={[]} onChange={mockOnChange} />);

    const files = [
      createMockFile('file1.pdf', 1024),
      createMockFile('file2.pdf', 1024),
      createMockFile('file3.pdf', 1024),
    ];
    const fileInput = document.getElementById('file-upload') as HTMLInputElement;

    // Act
    await userEvent.upload(fileInput, files);

    // Assert
    const newFiles = mockOnChange.mock.calls[0][0];
    expect(newFiles).toHaveLength(3);
  });

  // ============================================================================
  // TEST 20: Drag end resets state
  // ============================================================================
  it('should reset drag state on drag end', () => {
    // Arrange
    const files = [
      createMockFileItem('1', 'first.pdf', 1024, { sortOrder: 0 }),
      createMockFileItem('2', 'second.pdf', 1024, { sortOrder: 1 }),
    ];

    render(<FileUploadList files={files} onChange={vi.fn()} />);

    const listItems = screen.getAllByRole('listitem');
    const firstItem = listItems[0];

    // Act
    fireEvent.dragStart(firstItem);
    fireEvent.dragEnd(firstItem);

    // Assert - Should not have drag styling (opacity-50)
    expect(firstItem).not.toHaveClass('opacity-50');
  });
});
