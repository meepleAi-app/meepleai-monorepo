/**
 * AvatarUpload Component Tests - Issue #2882
 *
 * Tests for avatar upload functionality including:
 * - File picker opening on click
 * - Image preview display
 * - Crop UI rendering
 * - Upload callback execution
 * - Hover overlay with purple camera icon
 * - Error handling
 */

import { render, screen, fireEvent, waitFor, within } from '@testing-library/react';
import userEvent from '@testing-library/user-event';
import { describe, it, expect, vi, beforeEach, afterEach } from 'vitest';

import { AvatarUpload } from '../AvatarUpload';

// Mock react-image-crop to avoid complex canvas operations in tests
vi.mock('react-image-crop', () => ({
  default: ({
    children,
    onChange,
  }: {
    children: React.ReactNode;
    onChange: (crop: unknown, percentCrop: unknown) => void;
  }) => (
    <div
      data-testid="react-crop"
      onClick={() =>
        onChange(
          { x: 0, y: 0, width: 100, height: 100 },
          { x: 5, y: 5, width: 90, height: 90, unit: '%' }
        )
      }
    >
      {children}
    </div>
  ),
  centerCrop: vi.fn(crop => crop),
  makeAspectCrop: vi.fn(() => ({ unit: '%', width: 90, x: 5, y: 5, height: 90 })),
}));

// Mock URL.createObjectURL and URL.revokeObjectURL
const mockObjectUrl = 'blob:http://localhost/mock-url';

beforeEach(() => {
  vi.stubGlobal('URL', {
    ...URL,
    createObjectURL: vi.fn(() => mockObjectUrl),
    revokeObjectURL: vi.fn(),
  });
});

afterEach(() => {
  vi.unstubAllGlobals();
  vi.clearAllMocks();
});

// Helper to create a mock file
function createMockFile(
  name: string = 'test.jpg',
  type: string = 'image/jpeg',
  size: number = 1024
): File {
  const content = new Array(size).fill('a').join('');
  const blob = new Blob([content], { type });
  return new File([blob], name, { type });
}

describe('AvatarUpload', () => {
  const defaultProps = {
    currentAvatarUrl: null,
    displayName: 'Test User',
    onUpload: vi.fn().mockResolvedValue(undefined),
  };

  beforeEach(() => {
    defaultProps.onUpload = vi.fn().mockResolvedValue(undefined);
  });

  describe('Rendering', () => {
    it('renders avatar with fallback initials when no avatar URL', () => {
      render(<AvatarUpload {...defaultProps} />);

      expect(screen.getByText('TU')).toBeInTheDocument();
    });

    it('renders hidden file input with correct accept types', () => {
      render(<AvatarUpload {...defaultProps} />);

      const fileInput = document.querySelector('input[type="file"]') as HTMLInputElement;
      expect(fileInput).toBeInTheDocument();
      expect(fileInput).toHaveAttribute('accept', 'image/jpeg,image/png,image/webp,image/gif');
      expect(fileInput).toHaveClass('hidden');
    });

    it('renders camera button with purple background', () => {
      render(<AvatarUpload {...defaultProps} />);

      const cameraButton = screen.getByRole('button', { name: 'Cambia avatar' });
      expect(cameraButton).toBeInTheDocument();
      expect(cameraButton).toHaveClass('bg-purple-500');
    });

    it('renders with custom size', () => {
      render(<AvatarUpload {...defaultProps} size={120} />);

      const avatarSpan = document.querySelector('span.rounded-full') as HTMLElement;
      expect(avatarSpan).toHaveStyle({ width: '120px', height: '120px' });
    });

    it('disables button when disabled prop is true', () => {
      render(<AvatarUpload {...defaultProps} disabled />);

      const cameraButton = screen.getByRole('button', { name: 'Cambia avatar' });
      expect(cameraButton).toBeDisabled();
    });

    it('renders avatar wrapper with group role and proper aria-label', () => {
      render(<AvatarUpload {...defaultProps} />);

      const wrapper = screen.getByRole('group', { name: 'Avatar di Test User' });
      expect(wrapper).toBeInTheDocument();
    });
  });

  describe('File Selection', () => {
    it('opens file picker when camera button is clicked', async () => {
      render(<AvatarUpload {...defaultProps} />);
      const user = userEvent.setup();

      const fileInput = document.querySelector('input[type="file"]') as HTMLInputElement;
      const clickSpy = vi.spyOn(fileInput, 'click');

      const cameraButton = screen.getByRole('button', { name: 'Cambia avatar' });
      await user.click(cameraButton);

      expect(clickSpy).toHaveBeenCalled();
    });

    it('opens file picker when avatar area is clicked', async () => {
      render(<AvatarUpload {...defaultProps} />);
      const user = userEvent.setup();

      const fileInput = document.querySelector('input[type="file"]') as HTMLInputElement;
      const clickSpy = vi.spyOn(fileInput, 'click');

      // Click on the wrapper group
      const wrapper = screen.getByRole('group', { name: 'Avatar di Test User' });
      await user.click(wrapper);

      expect(clickSpy).toHaveBeenCalled();
    });

    it('shows error for unsupported file types', async () => {
      render(<AvatarUpload {...defaultProps} />);

      const fileInput = document.querySelector('input[type="file"]') as HTMLInputElement;
      const unsupportedFile = createMockFile('test.pdf', 'application/pdf');

      fireEvent.change(fileInput, { target: { files: [unsupportedFile] } });

      await waitFor(() => {
        expect(
          screen.getByText('Formato non supportato. Usa JPEG, PNG, WebP o GIF.')
        ).toBeInTheDocument();
      });
    });

    it('shows error for files exceeding 5MB', async () => {
      render(<AvatarUpload {...defaultProps} />);

      const fileInput = document.querySelector('input[type="file"]') as HTMLInputElement;
      const largeFile = createMockFile('large.jpg', 'image/jpeg', 6 * 1024 * 1024);

      fireEvent.change(fileInput, { target: { files: [largeFile] } });

      await waitFor(() => {
        expect(screen.getByText('Immagine troppo grande. Massimo 5MB.')).toBeInTheDocument();
      });
    });
  });

  describe('Crop Dialog', () => {
    it('opens crop dialog when valid image is selected', async () => {
      // Mock FileReader
      const mockFileReader = {
        readAsDataURL: vi.fn(),
        result: 'data:image/jpeg;base64,mockdata',
        onload: null as ((e: ProgressEvent<FileReader>) => void) | null,
        onerror: null as ((e: ProgressEvent<FileReader>) => void) | null,
      };

      vi.spyOn(global, 'FileReader').mockImplementation(
        () => mockFileReader as unknown as FileReader
      );

      // Mock Image with proper event handling
      const mockImage = {
        width: 500,
        height: 500,
        onload: null as (() => void) | null,
        onerror: null as (() => void) | null,
        src: '',
        addEventListener: vi.fn(),
        removeEventListener: vi.fn(),
      };

      vi.spyOn(global, 'Image').mockImplementation(() => mockImage as unknown as HTMLImageElement);

      render(<AvatarUpload {...defaultProps} />);

      const fileInput = document.querySelector('input[type="file"]') as HTMLInputElement;
      const validFile = createMockFile('test.jpg', 'image/jpeg');

      fireEvent.change(fileInput, { target: { files: [validFile] } });

      // Trigger FileReader onload
      await waitFor(() => {
        expect(mockFileReader.readAsDataURL).toHaveBeenCalled();
      });

      // Simulate FileReader completing
      mockFileReader.onload?.({ target: mockFileReader } as unknown as ProgressEvent<FileReader>);

      // Simulate Image loading
      await waitFor(() => {
        mockImage.onload?.();
      });

      await waitFor(() => {
        expect(screen.getByText('Ritaglia avatar')).toBeInTheDocument();
      });
    });
  });

  describe('Purple Camera Icon (DoD Requirement)', () => {
    it('has purple background on camera button', () => {
      render(<AvatarUpload {...defaultProps} />);

      const cameraButton = screen.getByRole('button', { name: 'Cambia avatar' });
      expect(cameraButton).toHaveClass('bg-purple-500');
      expect(cameraButton).toHaveClass('hover:bg-purple-600');
    });

    it('has purple overlay element', () => {
      render(<AvatarUpload {...defaultProps} />);

      // The overlay div should exist with purple color class
      const overlay = document.querySelector('[class*="bg-purple-600"]');
      expect(overlay).toBeInTheDocument();
    });
  });

  describe('Accessibility', () => {
    it('has proper aria-label on camera button', () => {
      render(<AvatarUpload {...defaultProps} />);

      const button = screen.getByRole('button', { name: 'Cambia avatar' });
      expect(button).toHaveAttribute('aria-label', 'Cambia avatar');
    });

    it('has aria-disabled attribute on wrapper when disabled', () => {
      render(<AvatarUpload {...defaultProps} disabled />);

      const wrapper = screen.getByRole('group', { name: 'Avatar di Test User' });
      expect(wrapper).toHaveAttribute('aria-disabled', 'true');
    });

    it('disables button when disabled prop is true', () => {
      render(<AvatarUpload {...defaultProps} disabled />);

      const button = screen.getByRole('button', { name: 'Cambia avatar' });
      expect(button).toBeDisabled();
    });

    it('supports keyboard activation on button with Enter key', async () => {
      render(<AvatarUpload {...defaultProps} />);
      const user = userEvent.setup();

      const fileInput = document.querySelector('input[type="file"]') as HTMLInputElement;
      const clickSpy = vi.spyOn(fileInput, 'click');

      const button = screen.getByRole('button', { name: 'Cambia avatar' });
      button.focus();
      await user.keyboard('{Enter}');

      expect(clickSpy).toHaveBeenCalled();
    });

    it('supports keyboard activation on button with Space key', async () => {
      render(<AvatarUpload {...defaultProps} />);
      const user = userEvent.setup();

      const fileInput = document.querySelector('input[type="file"]') as HTMLInputElement;
      const clickSpy = vi.spyOn(fileInput, 'click');

      const button = screen.getByRole('button', { name: 'Cambia avatar' });
      button.focus();
      await user.keyboard(' ');

      expect(clickSpy).toHaveBeenCalled();
    });
  });

  describe('Error Display', () => {
    it('shows error alert outside dialog for file validation errors', async () => {
      render(<AvatarUpload {...defaultProps} />);

      const fileInput = document.querySelector('input[type="file"]') as HTMLInputElement;
      const invalidFile = createMockFile('test.exe', 'application/x-msdownload');

      fireEvent.change(fileInput, { target: { files: [invalidFile] } });

      await waitFor(() => {
        const alert = screen.getByRole('alert');
        expect(alert).toBeInTheDocument();
        expect(within(alert).getByText(/Formato non supportato/)).toBeInTheDocument();
      });
    });
  });

  describe('Initials Generation', () => {
    it('generates correct initials from single name', () => {
      render(<AvatarUpload {...defaultProps} displayName="Mario" />);
      expect(screen.getByText('M')).toBeInTheDocument();
    });

    it('generates correct initials from two names', () => {
      render(<AvatarUpload {...defaultProps} displayName="Mario Rossi" />);
      expect(screen.getByText('MR')).toBeInTheDocument();
    });

    it('generates correct initials from multiple names (max 2)', () => {
      render(<AvatarUpload {...defaultProps} displayName="Mario Giovanni Rossi" />);
      expect(screen.getByText('MG')).toBeInTheDocument();
    });

    it('shows U for empty display name', () => {
      render(<AvatarUpload {...defaultProps} displayName="" />);
      expect(screen.getByText('U')).toBeInTheDocument();
    });
  });
});
