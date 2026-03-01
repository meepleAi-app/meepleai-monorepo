/**
 * Tests for PdfUploadZone
 * Issue #4820: Step 2 Knowledge Base & PDF Management
 */

import { fireEvent, render, screen, waitFor } from '@testing-library/react';
import { describe, expect, it, vi } from 'vitest';

import { PdfUploadZone } from '../PdfUploadZone';

const mockOnUpload = vi.fn();
const mockOnUploadComplete = vi.fn();

function renderZone(props = {}) {
  return render(
    <PdfUploadZone
      onUpload={mockOnUpload}
      onUploadComplete={mockOnUploadComplete}
      {...props}
    />,
  );
}

function createPdfFile(name = 'test.pdf', size = 1024) {
  return new File(['x'.repeat(size)], name, { type: 'application/pdf' });
}

describe('PdfUploadZone', () => {
  beforeEach(() => {
    vi.clearAllMocks();
  });

  // --- Rendering ---

  it('renders drop area', () => {
    renderZone();
    expect(screen.getByTestId('pdf-drop-area')).toBeInTheDocument();
    expect(screen.getByText('Trascina un PDF o clicca per selezionare')).toBeInTheDocument();
  });

  it('renders file size limit info', () => {
    renderZone();
    expect(screen.getByText('PDF fino a 50 MB')).toBeInTheDocument();
  });

  // --- File selection ---

  it('shows selected file details', () => {
    renderZone();

    const input = screen.getByTestId('pdf-file-input');
    const file = createPdfFile('Regolamento.pdf', 2_000_000);
    fireEvent.change(input, { target: { files: [file] } });

    expect(screen.getByText('Regolamento.pdf')).toBeInTheDocument();
    expect(screen.getByTestId('pdf-upload-button')).toBeInTheDocument();
  });

  it('rejects non-PDF files', () => {
    renderZone();

    const input = screen.getByTestId('pdf-file-input');
    const file = new File(['data'], 'image.png', { type: 'image/png' });
    fireEvent.change(input, { target: { files: [file] } });

    expect(screen.getByTestId('pdf-upload-error')).toBeInTheDocument();
    expect(screen.getByText('Seleziona un file PDF valido.')).toBeInTheDocument();
  });

  it('rejects files over 50MB', () => {
    renderZone();

    const input = screen.getByTestId('pdf-file-input');
    // Create a file that reports > 50MB
    const bigFile = new File(['x'], 'big.pdf', { type: 'application/pdf' });
    Object.defineProperty(bigFile, 'size', { value: 60 * 1024 * 1024 });
    fireEvent.change(input, { target: { files: [bigFile] } });

    expect(screen.getByTestId('pdf-upload-error')).toBeInTheDocument();
    expect(screen.getByText('Il file non deve superare i 50 MB.')).toBeInTheDocument();
  });

  it('clears file when remove button clicked', () => {
    renderZone();

    const input = screen.getByTestId('pdf-file-input');
    fireEvent.change(input, { target: { files: [createPdfFile()] } });
    expect(screen.getByText('test.pdf')).toBeInTheDocument();

    fireEvent.click(screen.getByLabelText('Rimuovi file'));
    expect(screen.queryByText('test.pdf')).not.toBeInTheDocument();
  });

  // --- Upload ---

  it('calls onUpload and onUploadComplete on success', async () => {
    mockOnUpload.mockResolvedValueOnce({
      documentId: 'doc-123',
      fileName: 'test.pdf',
    });

    renderZone();

    const input = screen.getByTestId('pdf-file-input');
    fireEvent.change(input, { target: { files: [createPdfFile()] } });
    fireEvent.click(screen.getByTestId('pdf-upload-button'));

    await waitFor(() => {
      expect(mockOnUpload).toHaveBeenCalledTimes(1);
      expect(mockOnUploadComplete).toHaveBeenCalledWith('doc-123', 'test.pdf', 1024);
    });
  });

  it('shows error when upload fails', async () => {
    mockOnUpload.mockRejectedValueOnce(new Error('Errore di rete'));

    renderZone();

    const input = screen.getByTestId('pdf-file-input');
    fireEvent.change(input, { target: { files: [createPdfFile()] } });
    fireEvent.click(screen.getByTestId('pdf-upload-button'));

    await waitFor(() => {
      expect(screen.getByTestId('pdf-upload-error')).toBeInTheDocument();
      expect(screen.getByText('Errore di rete')).toBeInTheDocument();
    });
  });

  // --- Drag and drop ---

  it('highlights on drag over', () => {
    renderZone();

    const dropArea = screen.getByTestId('pdf-drop-area');
    fireEvent.dragOver(dropArea);

    expect(dropArea).toHaveClass('border-teal-400');
  });

  it('removes highlight on drag leave', () => {
    renderZone();

    const dropArea = screen.getByTestId('pdf-drop-area');
    fireEvent.dragOver(dropArea);
    fireEvent.dragLeave(dropArea);

    expect(dropArea).not.toHaveClass('border-teal-400');
  });

  // --- Accessibility ---

  it('has accessible drop area', () => {
    renderZone();

    const dropArea = screen.getByTestId('pdf-drop-area');
    expect(dropArea).toHaveAttribute('role', 'button');
    expect(dropArea).toHaveAttribute('aria-label', 'Carica un file PDF');
  });

  it('is disabled when disabled prop is true', () => {
    renderZone({ disabled: true });

    const dropArea = screen.getByTestId('pdf-drop-area');
    expect(dropArea).toHaveClass('pointer-events-none');
  });
});
