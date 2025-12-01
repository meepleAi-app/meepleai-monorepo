/**
 * MultiFileUpload - Validation Errors Tests
 * Tests for error handling, display, and edge cases
 *
 * Test Coverage:
 * - Mixed valid/invalid files
 * - Multiple validation errors display
 * - Error clearing on success
 * - FileReader error handling
 * - Edge cases (empty/null files)
 */

import { act, fireEvent, render, screen, waitFor } from '@testing-library/react';
import { MultiFileUpload } from '../../components/upload/MultiFileUpload';
import {
  mockAddFiles,
  createMockFile,
  mockFileReader,
  defaultProps,
  setupBeforeEach,
  setupAfterEach,
  waitForFileValidation,
} from './MultiFileUpload.test-helpers';

// Import mock creation function
import { createMockUseUploadQueue } from './MultiFileUpload.test-helpers';
createMockUseUploadQueue();

describe('MultiFileUpload - Validation Errors', () => {
  beforeEach(setupBeforeEach);
  afterEach(setupAfterEach);

  describe('Mixed Valid/Invalid Files', () => {
    it('shows multiple validation errors for mixed invalid files', async () => {
      mockFileReader('%PDF-');
      render(<MultiFileUpload {...defaultProps} />);

      const files = [
        createMockFile('valid.pdf', 1000, 'application/pdf'),
        createMockFile('too-big.pdf', 105 * 1024 * 1024, 'application/pdf'),
        createMockFile('wrong-type.txt', 1000, 'text/plain'),
      ];
      const input = screen.getByLabelText(/File input for PDF upload/i) as HTMLInputElement;

      await act(async () => {
        fireEvent.change(input, { target: { files } });
        await waitForFileValidation(10);
      });

      await waitFor(() => {
        const alert = screen.getByRole('alert');
        expect(alert).toHaveTextContent(/Validation Errors/i);
        expect(alert.textContent).toContain('too-big.pdf');
        expect(alert.textContent).toContain('wrong-type.txt');
      });
    });

    it('adds only valid files when some files fail validation', async () => {
      mockFileReader('%PDF-');
      render(<MultiFileUpload {...defaultProps} />);

      const files = [
        createMockFile('valid1.pdf', 1000, 'application/pdf'),
        createMockFile('invalid.txt', 1000, 'text/plain'),
        createMockFile('valid2.pdf', 2000, 'application/pdf'),
      ];
      const input = screen.getByLabelText(/File input for PDF upload/i) as HTMLInputElement;

      await act(async () => {
        fireEvent.change(input, { target: { files } });
        await waitForFileValidation(150);
      });

      await waitFor(
        () => {
          expect(mockAddFiles).toHaveBeenCalledTimes(1);
          const callArgs = mockAddFiles.mock.calls[0];
          expect(callArgs[0]).toHaveLength(2);
          expect(callArgs[0]).toEqual(
            expect.arrayContaining([
              expect.objectContaining({ name: 'valid1.pdf', type: 'application/pdf' }),
              expect.objectContaining({ name: 'valid2.pdf', type: 'application/pdf' }),
            ])
          );
          expect(callArgs[1]).toBe('game-123');
          expect(callArgs[2]).toBe('en');
          expect(callArgs[0].every((f: File) => f.name !== 'invalid.txt')).toBe(true);
        },
        { timeout: 2000 }
      );
    });

    it('shows validation errors even when some files are valid', async () => {
      mockFileReader('%PDF-');
      render(<MultiFileUpload {...defaultProps} />);

      const files = [
        createMockFile('valid.pdf', 1000, 'application/pdf'),
        createMockFile('invalid.txt', 1000, 'text/plain'),
        createMockFile('empty.pdf', 0, 'application/pdf'),
      ];
      const input = screen.getByLabelText(/File input for PDF upload/i) as HTMLInputElement;

      await act(async () => {
        fireEvent.change(input, { target: { files } });
        await waitForFileValidation(50);
      });

      await waitFor(
        () => {
          const alert = screen.getByRole('alert');
          expect(alert).toHaveTextContent(/Validation Errors/i);
          expect(alert.textContent).toContain('invalid.txt');
          expect(alert.textContent).toContain('empty.pdf');
          // Valid file should still be added
          expect(mockAddFiles).toHaveBeenCalledWith(
            expect.arrayContaining([expect.objectContaining({ name: 'valid.pdf' })]),
            'game-123',
            'en'
          );
        },
        { timeout: 2000 }
      );
    });
  });

  describe('Validation Error Display', () => {
    it('renders validation errors with list when multiple errors present', async () => {
      mockFileReader('not-pdf');
      render(<MultiFileUpload {...defaultProps} />);

      const files = [
        createMockFile('invalid1.txt', 1000, 'text/plain'),
        createMockFile('invalid2.doc', 2000, 'application/msword'),
      ];
      const input = screen.getByLabelText(/File input for PDF upload/i) as HTMLInputElement;

      await act(async () => {
        fireEvent.change(input, { target: { files } });
        await waitForFileValidation(10);
      });

      await waitFor(() => {
        const alert = screen.getByRole('alert');
        expect(alert).toHaveTextContent('Validation Errors:');
        expect(alert.querySelector('ul')).toBeInTheDocument();
        expect(alert.querySelectorAll('li')).toHaveLength(2);
      });
    });

    it('clears validation errors on successful file addition', async () => {
      render(<MultiFileUpload {...defaultProps} />);

      // First, trigger validation error with mock returning non-PDF
      mockFileReader('not-pdf');
      const invalidFile = createMockFile('test.txt', 1000, 'text/plain');
      const input = screen.getByLabelText(/File input for PDF upload/i) as HTMLInputElement;

      await act(async () => {
        fireEvent.change(input, { target: { files: [invalidFile] } });
        await waitForFileValidation(10);
      });

      await waitFor(
        () => {
          expect(screen.getByRole('alert')).toBeInTheDocument();
        },
        { timeout: 1000 }
      );

      // Then, add valid file with proper PDF header
      mockFileReader('%PDF-');
      const validFile = createMockFile('valid.pdf', 1000, 'application/pdf');

      await act(async () => {
        fireEvent.change(input, { target: { files: [validFile] } });
        await waitForFileValidation(50);
      });

      await waitFor(
        () => {
          expect(mockAddFiles).toHaveBeenCalled();
        },
        { timeout: 1000 }
      );

      await waitFor(
        () => {
          const alert = screen.queryByRole('alert');
          if (alert) {
            expect(alert.textContent).not.toContain('Invalid PDF format: valid.pdf');
          }
        },
        { timeout: 1000 }
      );
    });

    it('does not render validation errors initially', () => {
      render(<MultiFileUpload {...defaultProps} />);
      expect(screen.queryByRole('alert')).not.toBeInTheDocument();
    });
  });

  describe('FileReader Error Handling', () => {
    it('handles FileReader errors gracefully', async () => {
      const mockReader = {
        readAsArrayBuffer: vi.fn(function (this: any) {
          setTimeout(() => this.onerror?.(new Error('Read failed')), 0);
        }),
        onload: null as any,
        onerror: null as any,
      };
      (global as any).FileReader = vi.fn(() => mockReader);

      render(<MultiFileUpload {...defaultProps} />);

      const file = createMockFile('test.pdf', 1000, 'application/pdf');
      const input = screen.getByLabelText(/File input for PDF upload/i) as HTMLInputElement;

      await act(async () => {
        fireEvent.change(input, { target: { files: [file] } });
        await waitForFileValidation(10);
      });

      await waitFor(() => {
        expect(screen.getByRole('alert')).toHaveTextContent(/Unable to read file/i);
      });
    });

    it('continues processing other files after FileReader error', async () => {
      let callCount = 0;
      (global as any).FileReader = vi.fn(function (this: any) {
        callCount++;
        const shouldFail = callCount === 1; // First file fails

        this.readAsArrayBuffer = vi.fn(function (this: any) {
          if (shouldFail) {
            setTimeout(() => this.onerror?.(new Error('Read failed')), 0);
          } else {
            const result = '%PDF-';
            const buffer = new ArrayBuffer(result.length);
            const view = new Uint8Array(buffer);
            for (let i = 0; i < result.length; i++) {
              view[i] = result.charCodeAt(i);
            }
            this.result = buffer;
            setTimeout(() => {
              if (this.onload) {
                this.onload({ target: { result: buffer } });
              }
            }, 0);
          }
        });
        this.onload = null;
        this.onerror = null;
        this.result = null;
      });

      render(<MultiFileUpload {...defaultProps} />);

      const files = [
        createMockFile('fail.pdf', 1000, 'application/pdf'),
        createMockFile('success.pdf', 1000, 'application/pdf'),
      ];
      const input = screen.getByLabelText(/File input for PDF upload/i) as HTMLInputElement;

      await act(async () => {
        fireEvent.change(input, { target: { files } });
        await waitForFileValidation(150);
      });

      await waitFor(
        () => {
          // Should have error for failed file
          expect(screen.getByRole('alert')).toBeInTheDocument();
          // Should still add the successful file
          expect(mockAddFiles).toHaveBeenCalledWith(
            expect.arrayContaining([expect.objectContaining({ name: 'success.pdf' })]),
            'game-123',
            'en'
          );
        },
        { timeout: 2000 }
      );
    });
  });

  describe('Edge Cases - Validation', () => {
    it('handles empty file list selection', async () => {
      render(<MultiFileUpload {...defaultProps} />);

      const input = screen.getByLabelText(/File input for PDF upload/i) as HTMLInputElement;

      await act(async () => {
        fireEvent.change(input, { target: { files: [] } });
        await waitForFileValidation(10);
      });

      expect(mockAddFiles).not.toHaveBeenCalled();
    });

    it('handles null files gracefully', async () => {
      render(<MultiFileUpload {...defaultProps} />);

      const input = screen.getByLabelText(/File input for PDF upload/i) as HTMLInputElement;

      await act(async () => {
        fireEvent.change(input, { target: { files: null as any } });
        await waitForFileValidation(10);
      });

      expect(mockAddFiles).not.toHaveBeenCalled();
      expect(screen.queryByRole('alert')).not.toBeInTheDocument();
    });
  });
});
