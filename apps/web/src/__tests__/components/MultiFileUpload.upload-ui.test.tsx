/**
 * MultiFileUpload - Upload UI Tests
 * Tests for component rendering, styling, and accessibility
 *
 * Test Coverage:
 * - Component structure and rendering
 * - Game info badge display
 * - Drag zone rendering and instructions
 * - File input configuration
 * - Component styling and layout
 * - Accessibility features
 * - ARIA attributes and labels
 */

import { render, screen } from '@testing-library/react';
import { MultiFileUpload } from '../../components/upload/MultiFileUpload';
import {
  mockGetStats,
  defaultProps,
  setupBeforeEach,
  setupAfterEach,
  createStats,
} from './MultiFileUpload.test-helpers';

// Import mock creation function
import { createMockUseUploadQueue } from './MultiFileUpload.test-helpers';
createMockUseUploadQueue();

describe('MultiFileUpload - Upload UI', () => {
  beforeEach(setupBeforeEach);
  afterEach(setupAfterEach);

  describe('Component Rendering', () => {
    it('renders with game info badge', () => {
      render(<MultiFileUpload {...defaultProps} />);

      expect(screen.getByTestId('multi-file-upload')).toBeInTheDocument();
      expect(screen.getByTestId('game-info-badge')).toHaveTextContent(
        'Target Game: Test Game (game-123)'
      );
    });

    it('renders drag and drop zone with instructions', () => {
      render(<MultiFileUpload {...defaultProps} />);

      expect(screen.getByText(/Drag and drop PDF files here/i)).toBeInTheDocument();
      expect(screen.getByText(/or click to browse/i)).toBeInTheDocument();
      expect(screen.getByRole('button', { name: /Select Files/i })).toBeInTheDocument();
    });

    it('applies data attributes correctly', () => {
      render(<MultiFileUpload {...defaultProps} />);

      const container = screen.getByTestId('multi-file-upload');
      expect(container).toHaveAttribute('data-game-id', 'game-123');
      expect(container).toHaveAttribute('data-game-name', 'Test Game');
    });

    it('renders heading with correct text', () => {
      render(<MultiFileUpload {...defaultProps} />);
      expect(screen.getByRole('heading', { level: 3 })).toHaveTextContent('Multi-File Upload');
    });

    it('renders game info badge with all details', () => {
      render(<MultiFileUpload {...defaultProps} />);
      const badge = screen.getByTestId('game-info-badge');
      expect(badge).toHaveTextContent('Target Game:');
      expect(badge).toHaveTextContent('Test Game');
      expect(badge).toHaveTextContent('(game-123)');
    });

    it('renders drag zone with folder emoji', () => {
      render(<MultiFileUpload {...defaultProps} />);
      const dropZone = screen.getByRole('button', {
        name: /Click to browse files or drag and drop PDFs here/i,
      });
      expect(dropZone).toBeInTheDocument();
      expect(dropZone.textContent).toContain('📁');
    });

    it('renders drag zone instructions', () => {
      render(<MultiFileUpload {...defaultProps} />);
      expect(screen.getByText('Drag and drop PDF files here')).toBeInTheDocument();
      expect(screen.getByText(/or click to browse/i)).toBeInTheDocument();
      expect(screen.getByText(/up to 20 files, max 100 MB each/i)).toBeInTheDocument();
    });

    it('renders Select Files button', () => {
      render(<MultiFileUpload {...defaultProps} />);
      const selectButton = screen.getByRole('button', { name: /Select Files/i });
      expect(selectButton).toBeInTheDocument();
      expect(selectButton).toHaveAttribute('type', 'button');
    });

    it('renders hidden file input', () => {
      render(<MultiFileUpload {...defaultProps} />);
      const input = screen.getByLabelText(/File input for PDF upload/i);
      expect(input).toHaveClass('hidden');
      expect(input).toHaveAttribute('accept', 'application/pdf');
      expect(input).toHaveAttribute('multiple');
    });
  });

  describe('Component Styling', () => {
    it('applies correct container styles', () => {
      render(<MultiFileUpload {...defaultProps} />);
      const container = screen.getByTestId('multi-file-upload');
      expect(container).toBeInTheDocument();
      expect(container).toHaveClass('mt-6');
    });

    it('applies correct heading styles', () => {
      render(<MultiFileUpload {...defaultProps} />);
      const heading = screen.getByRole('heading', { level: 3 });
      expect(heading).toBeInTheDocument();
      expect(heading).toHaveTextContent('Multi-File Upload');
    });

    it('applies correct game badge styles', () => {
      render(<MultiFileUpload {...defaultProps} />);
      const badge = screen.getByTestId('game-info-badge');
      expect(badge).toBeInTheDocument();
      expect(screen.getByText(/Target Game: Test Game/)).toBeInTheDocument();
    });
  });

  describe('Accessibility Features', () => {
    it('has accessible drag and drop zone', () => {
      render(<MultiFileUpload {...defaultProps} />);

      const dropZone = screen.getByRole('button', {
        name: /Click to browse files or drag and drop PDFs here/i,
      });
      expect(dropZone).toHaveAttribute('tabIndex', '0');
      expect(dropZone).toHaveAttribute('aria-label');
    });

    it('has accessible file input', () => {
      render(<MultiFileUpload {...defaultProps} />);

      const input = screen.getByLabelText(/File input for PDF upload/i);
      expect(input).toHaveAttribute('type', 'file');
      expect(input).toHaveAttribute('accept', 'application/pdf');
      expect(input).toHaveAttribute('multiple');
    });
  });

  describe('Manual vs Auto Upload Modes', () => {
    it('hides start upload button when autoUpload is true', () => {
      mockGetStats.mockReturnValue(createStats({ total: 2, pending: 2 }));

      render(<MultiFileUpload {...defaultProps} autoUpload={true} />);

      expect(screen.queryByTestId('start-upload-button')).not.toBeInTheDocument();
    });

    it('shows start upload button when autoUpload is false and files are pending', () => {
      mockGetStats.mockReturnValue(createStats({ total: 2, pending: 2 }));

      render(<MultiFileUpload {...defaultProps} autoUpload={false} />);

      expect(screen.getByTestId('start-upload-button')).toBeInTheDocument();
      expect(screen.getByTestId('start-upload-button')).toHaveTextContent(
        /Start Upload \(2 files\)/i
      );
    });

    it('shows correct button text with singular file', () => {
      mockGetStats.mockReturnValue(createStats({ total: 1, pending: 1 }));

      render(<MultiFileUpload {...defaultProps} autoUpload={false} />);
      expect(screen.getByTestId('start-upload-button')).toHaveTextContent('Start Upload (1 file)');
    });

    it('shows correct button text with multiple files', () => {
      mockGetStats.mockReturnValue(createStats({ total: 5, pending: 5 }));

      render(<MultiFileUpload {...defaultProps} autoUpload={false} />);
      expect(screen.getByTestId('start-upload-button')).toHaveTextContent('Start Upload (5 files)');
    });

    it('does not show button when no pending files', () => {
      mockGetStats.mockReturnValue(createStats({ total: 2, uploading: 1, processing: 1 }));

      render(<MultiFileUpload {...defaultProps} autoUpload={false} />);
      expect(screen.queryByTestId('start-upload-button')).not.toBeInTheDocument();
    });
  });
});
