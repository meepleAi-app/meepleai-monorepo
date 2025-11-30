/**
 * MultiFileUpload - Upload Interactions Tests
 * Tests for file selection, drag-drop, keyboard navigation, and callbacks
 *
 * Test Coverage:
 * - File selection via input
 * - Multiple file selection
 * - Browse button functionality
 * - Drag and drop events
 * - Drag zone visual states
 * - Drop zone click handling
 * - Keyboard navigation
 * - Manual upload trigger
 * - Callback props
 */

import { act, fireEvent, render, screen, waitFor } from '@testing-library/react';
import { MultiFileUpload } from '../../components/MultiFileUpload';
import {
  mockAddFiles,
  mockStartUpload,
  mockGetStats,
  createMockFile,
  mockFileReader,
  defaultProps,
  setupBeforeEach,
  setupAfterEach,
  waitForFileValidation,
  createStats,
  getLastUploadQueueOptions
} from './MultiFileUpload.test-helpers';

// Import mock creation function
import { createMockUseUploadQueue } from './MultiFileUpload.test-helpers';
createMockUseUploadQueue();

describe('MultiFileUpload - Upload Interactions', () => {
  beforeEach(setupBeforeEach);
  afterEach(setupAfterEach);

  describe('File Selection via Input', () => {
    it('handles file selection via input', async () => {
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
            expect.objectContaining({ name: 'test.pdf', size: 1000, type: 'application/pdf' })
          ]),
          'game-123',
          'en'
        );
      }, { timeout: 2000 });
    });

    it('handles multiple file selection', async () => {
      mockFileReader('%PDF-');
      render(<MultiFileUpload {...defaultProps} />);

      const files = [
        createMockFile('test1.pdf', 1000, 'application/pdf'),
        createMockFile('test2.pdf', 2000, 'application/pdf'),
        createMockFile('test3.pdf', 3000, 'application/pdf')
      ];
      const input = screen.getByLabelText(/File input for PDF upload/i) as HTMLInputElement;

      await act(async () => {
        fireEvent.change(input, { target: { files } });
        await waitForFileValidation(150);
      });

      await waitFor(() => {
        expect(mockAddFiles).toHaveBeenCalledWith(
          expect.arrayContaining([
            expect.objectContaining({ name: 'test1.pdf', type: 'application/pdf' }),
            expect.objectContaining({ name: 'test2.pdf', type: 'application/pdf' }),
            expect.objectContaining({ name: 'test3.pdf', type: 'application/pdf' })
          ]),
          'game-123',
          'en'
        );
      }, { timeout: 2000 });
    });

    it('resets input value after selection', async () => {
      mockFileReader('%PDF-');
      render(<MultiFileUpload {...defaultProps} />);

      const file = createMockFile('test.pdf', 1000, 'application/pdf');
      const input = screen.getByLabelText(/File input for PDF upload/i) as HTMLInputElement;

      await act(async () => {
        fireEvent.change(input, { target: { files: [file] } });
        await waitForFileValidation(10);
      });

      expect(input.value).toBe('');
    });

    it('handles browse button click', () => {
      render(<MultiFileUpload {...defaultProps} />);

      const browseButton = screen.getByRole('button', { name: /Select Files/i });
      const input = screen.getByLabelText(/File input for PDF upload/i) as HTMLInputElement;

      const clickSpy = vi.spyOn(input, 'click');
      fireEvent.click(browseButton);

      expect(clickSpy).toHaveBeenCalled();
    });
  });

  describe('Drag and Drop Events', () => {
    it('handles drag enter event', () => {
      render(<MultiFileUpload {...defaultProps} />);

      const dropZone = screen.getByRole('button', { name: /Click to browse files or drag and drop PDFs here/i });

      fireEvent.dragEnter(dropZone);

      expect(screen.getByText(/Drop files here/i)).toBeInTheDocument();
    });

    it('handles drag leave event', () => {
      render(<MultiFileUpload {...defaultProps} />);

      const dropZone = screen.getByRole('button', { name: /Click to browse files or drag and drop PDFs here/i });

      fireEvent.dragEnter(dropZone);
      expect(screen.getByText(/Drop files here/i)).toBeInTheDocument();

      fireEvent.dragLeave(dropZone, { currentTarget: dropZone, target: dropZone });

      expect(screen.getByText(/Drag and drop PDF files here/i)).toBeInTheDocument();
    });

    it('handles drag over event', () => {
      render(<MultiFileUpload {...defaultProps} />);

      const dropZone = screen.getByRole('button', { name: /Click to browse files or drag and drop PDFs here/i });
      const event = new Event('dragover', { bubbles: true, cancelable: true });

      fireEvent(dropZone, event);

      expect(event.defaultPrevented).toBe(true);
    });

    it('handles file drop', async () => {
      mockFileReader('%PDF-');
      render(<MultiFileUpload {...defaultProps} />);

      const dropZone = screen.getByRole('button', { name: /Click to browse files or drag and drop PDFs here/i });
      const file = createMockFile('dropped.pdf', 1000, 'application/pdf');

      await act(async () => {
        fireEvent.drop(dropZone, {
          dataTransfer: { files: [file] }
        });
        await waitForFileValidation(100);
      });

      await waitFor(() => {
        expect(mockAddFiles).toHaveBeenCalledWith(
          expect.arrayContaining([
            expect.objectContaining({ name: 'dropped.pdf', size: 1000, type: 'application/pdf' })
          ]),
          'game-123',
          'en'
        );
      }, { timeout: 2000 });
    });

    it('resets dragging state after drop', async () => {
      mockFileReader('%PDF-');
      render(<MultiFileUpload {...defaultProps} />);

      const dropZone = screen.getByRole('button', { name: /Click to browse files or drag and drop PDFs here/i });

      fireEvent.dragEnter(dropZone);
      expect(screen.getByText(/Drop files here/i)).toBeInTheDocument();

      const file = createMockFile('test.pdf', 1000, 'application/pdf');

      await act(async () => {
        fireEvent.drop(dropZone, {
          dataTransfer: { files: [file] }
        });
        await waitForFileValidation(10);
      });

      expect(screen.getByText(/Drag and drop PDF files here/i)).toBeInTheDocument();
    });

    it('handles null files in drop event', async () => {
      render(<MultiFileUpload {...defaultProps} />);

      const dropZone = screen.getByRole('button', { name: /Click to browse files or drag and drop PDFs here/i });

      await act(async () => {
        fireEvent.drop(dropZone, {
          dataTransfer: { files: null }
        });
        await waitForFileValidation(10);
      });

      expect(mockAddFiles).not.toHaveBeenCalled();
    });
  });

  describe('Drag Zone Visual States', () => {
    it('changes drag zone appearance when dragging', () => {
      render(<MultiFileUpload {...defaultProps} />);
      const dropZone = screen.getByRole('button', { name: /Click to browse files or drag and drop PDFs here/i });

      expect(dropZone).toBeInTheDocument();

      fireEvent.dragEnter(dropZone);
      expect(screen.getByText('Drop files here')).toBeInTheDocument();

      fireEvent.dragLeave(dropZone, { currentTarget: dropZone, target: dropZone });
      expect(screen.getByText('Drag and drop PDF files here')).toBeInTheDocument();
    });

    it('shows different text when dragging', () => {
      render(<MultiFileUpload {...defaultProps} />);
      const dropZone = screen.getByRole('button', { name: /Click to browse files or drag and drop PDFs here/i });

      expect(screen.getByText('Drag and drop PDF files here')).toBeInTheDocument();
      expect(screen.queryByText('Drop files here')).not.toBeInTheDocument();

      fireEvent.dragEnter(dropZone);

      expect(screen.getByText('Drop files here')).toBeInTheDocument();
      expect(screen.queryByText('Drag and drop PDF files here')).not.toBeInTheDocument();
    });
  });

  describe('Drop Zone Click Handling', () => {
    it('triggers file input on drop zone click', () => {
      render(<MultiFileUpload {...defaultProps} />);
      const dropZone = screen.getByRole('button', { name: /Click to browse files or drag and drop PDFs here/i });
      const input = screen.getByLabelText(/File input for PDF upload/i) as HTMLInputElement;
      const clickSpy = vi.spyOn(input, 'click');

      fireEvent.click(dropZone);
      expect(clickSpy).toHaveBeenCalled();
    });

    it('prevents event propagation when Select Files button clicked', () => {
      render(<MultiFileUpload {...defaultProps} />);
      const selectButton = screen.getByRole('button', { name: /Select Files/i });
      const input = screen.getByLabelText(/File input for PDF upload/i) as HTMLInputElement;
      const clickSpy = vi.spyOn(input, 'click');

      const event = new MouseEvent('click', { bubbles: true, cancelable: true });
      const stopPropSpy = vi.spyOn(event, 'stopPropagation');

      fireEvent(selectButton, event);

      expect(stopPropSpy).toHaveBeenCalled();
      expect(clickSpy).toHaveBeenCalled();
    });
  });

  describe('Manual Upload Trigger', () => {
    it('handles manual upload trigger', () => {
      mockGetStats.mockReturnValue(createStats({ total: 1, pending: 1 }));

      render(<MultiFileUpload {...defaultProps} autoUpload={false} />);

      const startButton = screen.getByTestId('start-upload-button');
      fireEvent.click(startButton);

      expect(mockStartUpload).toHaveBeenCalled();
    });
  });

  describe('Callback Props', () => {
    it('calls onUploadComplete when provided', () => {
      const onUploadComplete = vi.fn();
      render(<MultiFileUpload {...defaultProps} onUploadComplete={onUploadComplete} />);

      const options = getLastUploadQueueOptions();

      act(() => {
        options.onUploadComplete?.({ id: '1', status: 'success' });
      });

      expect(onUploadComplete).toHaveBeenCalled();
    });

    it('passes observability hooks to useUploadQueue', () => {
      const hooks = {
        onUploadStart: vi.fn(),
        onUploadSuccess: vi.fn(),
        onUploadError: vi.fn(),
        onQueueAdd: vi.fn(),
        onRetry: vi.fn()
      };

      render(<MultiFileUpload {...defaultProps} {...hooks} />);

      const options = getLastUploadQueueOptions();

      expect(options.onUploadStart).toBe(hooks.onUploadStart);
      expect(options.onUploadSuccess).toBe(hooks.onUploadSuccess);
      expect(options.onUploadError).toBe(hooks.onUploadError);
      expect(options.onQueueAdd).toBe(hooks.onQueueAdd);
      expect(options.onRetry).toBe(hooks.onRetry);
    });
  });

  describe('Keyboard Navigation', () => {
    it('supports keyboard navigation for drop zone', () => {
      render(<MultiFileUpload {...defaultProps} />);

      const dropZone = screen.getByRole('button', { name: /Click to browse files or drag and drop PDFs here/i });
      const input = screen.getByLabelText(/File input for PDF upload/i) as HTMLInputElement;
      const clickSpy = vi.spyOn(input, 'click');

      fireEvent.keyDown(dropZone, { key: 'Enter' });
      expect(clickSpy).toHaveBeenCalled();

      clickSpy.mockClear();
      fireEvent.keyDown(dropZone, { key: ' ' });
      expect(clickSpy).toHaveBeenCalled();
    });

    it('prevents default on Enter key in drop zone', () => {
      render(<MultiFileUpload {...defaultProps} />);
      const dropZone = screen.getByRole('button', { name: /Click to browse files or drag and drop PDFs here/i });
      const input = screen.getByLabelText(/File input for PDF upload/i) as HTMLInputElement;
      const clickSpy = vi.spyOn(input, 'click');

      const event = new KeyboardEvent('keydown', { key: 'Enter', bubbles: true, cancelable: true });
      const preventDefaultSpy = vi.spyOn(event, 'preventDefault');

      fireEvent(dropZone, event);

      expect(preventDefaultSpy).toHaveBeenCalled();
      expect(clickSpy).toHaveBeenCalled();
    });

    it('prevents default on Space key in drop zone', () => {
      render(<MultiFileUpload {...defaultProps} />);
      const dropZone = screen.getByRole('button', { name: /Click to browse files or drag and drop PDFs here/i });
      const input = screen.getByLabelText(/File input for PDF upload/i) as HTMLInputElement;
      const clickSpy = vi.spyOn(input, 'click');

      const event = new KeyboardEvent('keydown', { key: ' ', bubbles: true, cancelable: true });
      const preventDefaultSpy = vi.spyOn(event, 'preventDefault');

      fireEvent(dropZone, event);

      expect(preventDefaultSpy).toHaveBeenCalled();
      expect(clickSpy).toHaveBeenCalled();
    });

    it('ignores other keys in drop zone', () => {
      render(<MultiFileUpload {...defaultProps} />);
      const dropZone = screen.getByRole('button', { name: /Click to browse files or drag and drop PDFs here/i });
      const input = screen.getByLabelText(/File input for PDF upload/i) as HTMLInputElement;
      const clickSpy = vi.spyOn(input, 'click');

      fireEvent.keyDown(dropZone, { key: 'a' });
      fireEvent.keyDown(dropZone, { key: 'Escape' });
      fireEvent.keyDown(dropZone, { key: 'Tab' });

      expect(clickSpy).not.toHaveBeenCalled();
    });
  });

  describe('Button Hover States', () => {
    it('handles Select Files button hover states', () => {
      render(<MultiFileUpload {...defaultProps} />);
      const button = screen.getByRole('button', { name: /Select Files/i });

      expect(button).toBeInTheDocument();
      expect(button).toHaveAttribute('type', 'button');

      fireEvent.mouseEnter(button);
      fireEvent.mouseLeave(button);
      expect(button).toBeInTheDocument();
    });

    it('handles Start Upload button hover states in manual mode', () => {
      mockGetStats.mockReturnValue(createStats({ total: 1, pending: 1 }));

      render(<MultiFileUpload {...defaultProps} autoUpload={false} />);
      const button = screen.getByTestId('start-upload-button');

      expect(button).toBeInTheDocument();
      expect(button).not.toBeDisabled();

      fireEvent.mouseEnter(button);
      fireEvent.mouseLeave(button);
      expect(button).toBeInTheDocument();
    });
  });
});
