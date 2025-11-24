/**
 * MultiFileUpload - Validation Types Tests
 * Tests for file type, size, magic bytes, and batch size validation
 *
 * Test Coverage:
 * - File type validation (PDF only)
 * - File size limits (100MB max, no empty files)
 * - PDF magic bytes validation (%PDF-)
 * - Batch size limits (20 files max)
 * - Valid file acceptance
 */

import { act, fireEvent, render, screen, waitFor } from '@testing-library/react';
import { MultiFileUpload } from '../../components/MultiFileUpload';
import {
  mockAddFiles,
  createMockFile,
  mockFileReader,
  defaultProps,
  setupBeforeEach,
  setupAfterEach,
  waitForFileValidation
} from './MultiFileUpload.test-helpers';

// Import mock creation function
import { createMockUseUploadQueue } from './MultiFileUpload.test-helpers';
createMockUseUploadQueue();

describe('MultiFileUpload - Validation Types', () => {
  beforeEach(setupBeforeEach);
  afterEach(setupAfterEach);

  describe('File Type Validation', () => {
    it('rejects non-PDF files', async () => {
      mockFileReader('not-a-pdf');
      render(<MultiFileUpload {...defaultProps} />);

      const file = createMockFile('test.txt', 1000, 'text/plain');
      const input = screen.getByLabelText(/File input for PDF upload/i) as HTMLInputElement;

      await act(async () => {
        fireEvent.change(input, { target: { files: [file] } });
        await waitForFileValidation(10);
      });

      await waitFor(() => {
        expect(screen.getByRole('alert')).toHaveTextContent(/Invalid file type/i);
        expect(mockAddFiles).not.toHaveBeenCalled();
      });
    });

    it('accepts valid PDF files with correct MIME type', async () => {
      mockFileReader('%PDF-');
      render(<MultiFileUpload {...defaultProps} />);

      const file = createMockFile('test.pdf', 1000, 'application/pdf');
      const input = screen.getByLabelText(/File input for PDF upload/i) as HTMLInputElement;

      await act(async () => {
        fireEvent.change(input, { target: { files: [file] } });
        await waitForFileValidation(100);
      });

      await waitFor(() => {
        expect(mockAddFiles).toHaveBeenCalledWith(
          expect.arrayContaining([
            expect.objectContaining({ name: 'test.pdf', type: 'application/pdf' })
          ]),
          'game-123',
          'en'
        );
      }, { timeout: 2000 });
    });
  });

  describe('File Size Validation', () => {
    it('rejects files exceeding 100MB limit', async () => {
      mockFileReader('%PDF-');
      render(<MultiFileUpload {...defaultProps} />);

      const file = createMockFile('huge.pdf', 105 * 1024 * 1024, 'application/pdf'); // 105 MB
      const input = screen.getByLabelText(/File input for PDF upload/i) as HTMLInputElement;

      await act(async () => {
        fireEvent.change(input, { target: { files: [file] } });
        await waitForFileValidation(10);
      });

      await waitFor(() => {
        expect(screen.getByRole('alert')).toHaveTextContent(/File too large/i);
        expect(screen.getByRole('alert')).toHaveTextContent(/100 MB limit/i);
        expect(mockAddFiles).not.toHaveBeenCalled();
      });
    });

    it('rejects empty files', async () => {
      mockFileReader('');
      render(<MultiFileUpload {...defaultProps} />);

      const file = createMockFile('empty.pdf', 0, 'application/pdf');
      const input = screen.getByLabelText(/File input for PDF upload/i) as HTMLInputElement;

      await act(async () => {
        fireEvent.change(input, { target: { files: [file] } });
        await waitForFileValidation(10);
      });

      await waitFor(() => {
        expect(screen.getByRole('alert')).toHaveTextContent(/Empty file/i);
        expect(mockAddFiles).not.toHaveBeenCalled();
      });
    });

    it('accepts files under 100MB limit', async () => {
      mockFileReader('%PDF-');
      render(<MultiFileUpload {...defaultProps} />);

      const file = createMockFile('valid.pdf', 50 * 1024 * 1024, 'application/pdf'); // 50 MB
      const input = screen.getByLabelText(/File input for PDF upload/i) as HTMLInputElement;

      await act(async () => {
        fireEvent.change(input, { target: { files: [file] } });
        await waitForFileValidation(100);
      });

      await waitFor(() => {
        expect(mockAddFiles).toHaveBeenCalledWith(
          expect.arrayContaining([
            expect.objectContaining({ name: 'valid.pdf', type: 'application/pdf' })
          ]),
          'game-123',
          'en'
        );
      }, { timeout: 2000 });
    });
  });

  describe('PDF Magic Bytes Validation', () => {
    it('rejects files with invalid PDF magic bytes', async () => {
      mockFileReader('not-pdf-header');
      render(<MultiFileUpload {...defaultProps} />);

      const file = createMockFile('invalid.pdf', 1000, 'application/pdf', 'not-pdf-header');
      const input = screen.getByLabelText(/File input for PDF upload/i) as HTMLInputElement;

      await act(async () => {
        fireEvent.change(input, { target: { files: [file] } });
        await waitForFileValidation(10);
      });

      await waitFor(() => {
        expect(screen.getByRole('alert')).toHaveTextContent(/Invalid PDF format/i);
        expect(mockAddFiles).not.toHaveBeenCalled();
      });
    });

    it('accepts files with valid PDF magic bytes (%PDF-)', async () => {
      mockFileReader('%PDF-1.4');
      render(<MultiFileUpload {...defaultProps} />);

      const file = createMockFile('valid.pdf', 1000, 'application/pdf', '%PDF-1.4');
      const input = screen.getByLabelText(/File input for PDF upload/i) as HTMLInputElement;

      await act(async () => {
        fireEvent.change(input, { target: { files: [file] } });
        await waitForFileValidation(100);
      });

      await waitFor(() => {
        expect(mockAddFiles).toHaveBeenCalledWith(
          expect.arrayContaining([
            expect.objectContaining({ name: 'valid.pdf', type: 'application/pdf' })
          ]),
          'game-123',
          'en'
        );
      }, { timeout: 2000 });
    });

    it('accepts files with PDF magic bytes variations', async () => {
      mockFileReader('%PDF-2.0');
      render(<MultiFileUpload {...defaultProps} />);

      const file = createMockFile('modern.pdf', 1000, 'application/pdf', '%PDF-2.0');
      const input = screen.getByLabelText(/File input for PDF upload/i) as HTMLInputElement;

      await act(async () => {
        fireEvent.change(input, { target: { files: [file] } });
        await waitForFileValidation(100);
      });

      await waitFor(() => {
        expect(mockAddFiles).toHaveBeenCalledWith(
          expect.arrayContaining([
            expect.objectContaining({ name: 'modern.pdf', type: 'application/pdf' })
          ]),
          'game-123',
          'en'
        );
      }, { timeout: 2000 });
    });
  });

  describe('Batch Size Validation', () => {
    it('rejects batches exceeding 20 files', async () => {
      mockFileReader('%PDF-');
      render(<MultiFileUpload {...defaultProps} />);

      const files = Array.from({ length: 21 }, (_, i) =>
        createMockFile(`test${i}.pdf`, 1000, 'application/pdf')
      );
      const input = screen.getByLabelText(/File input for PDF upload/i) as HTMLInputElement;

      await act(async () => {
        fireEvent.change(input, { target: { files } });
        await waitForFileValidation(10);
      });

      await waitFor(() => {
        expect(screen.getByRole('alert')).toHaveTextContent(/Too many files selected/i);
        expect(screen.getByRole('alert')).toHaveTextContent(/Maximum 20 files/i);
        expect(mockAddFiles).not.toHaveBeenCalled();
      });
    });

    it('accepts batch of exactly 20 files', async () => {
      mockFileReader('%PDF-');
      render(<MultiFileUpload {...defaultProps} />);

      const files = Array.from({ length: 20 }, (_, i) =>
        createMockFile(`test${i}.pdf`, 1000, 'application/pdf')
      );
      const input = screen.getByLabelText(/File input for PDF upload/i) as HTMLInputElement;

      await act(async () => {
        fireEvent.change(input, { target: { files } });
        await waitForFileValidation(150);
      });

      await waitFor(() => {
        expect(mockAddFiles).toHaveBeenCalledWith(
          expect.arrayContaining(
            Array.from({ length: 20 }, (_, i) =>
              expect.objectContaining({ name: `test${i}.pdf`, type: 'application/pdf' })
            )
          ),
          'game-123',
          'en'
        );
      }, { timeout: 2000 });
    });

    it('accepts batch with fewer than 20 files', async () => {
      mockFileReader('%PDF-');
      render(<MultiFileUpload {...defaultProps} />);

      const files = Array.from({ length: 5 }, (_, i) =>
        createMockFile(`test${i}.pdf`, 1000, 'application/pdf')
      );
      const input = screen.getByLabelText(/File input for PDF upload/i) as HTMLInputElement;

      await act(async () => {
        fireEvent.change(input, { target: { files } });
        await waitForFileValidation(100);
      });

      await waitFor(() => {
        expect(mockAddFiles).toHaveBeenCalledWith(
          expect.arrayContaining(
            Array.from({ length: 5 }, (_, i) =>
              expect.objectContaining({ name: `test${i}.pdf`, type: 'application/pdf' })
            )
          ),
          'game-123',
          'en'
        );
      }, { timeout: 2000 });
    });
  });
});
