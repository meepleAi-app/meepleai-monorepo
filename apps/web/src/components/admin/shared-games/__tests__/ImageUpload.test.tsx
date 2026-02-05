import { render, screen, fireEvent, waitFor } from '@testing-library/react';
import { describe, it, expect, vi, beforeEach } from 'vitest';

import { ImageUpload } from '../ImageUpload';

// Mock toast
vi.mock('@/components/layout/Toast', () => ({
  toast: {
    error: vi.fn(),
    success: vi.fn(),
  },
}));

// Mock next/image
vi.mock('next/image', () => ({
  default: ({ src, alt, ...props }: { src: string; alt: string }) => (
    // eslint-disable-next-line @next/next/no-img-element
    <img src={src} alt={alt} {...props} />
  ),
}));

describe('ImageUpload', () => {
  const mockOnFileChange = vi.fn();
  const mockOnUrlChange = vi.fn();

  beforeEach(() => {
    vi.clearAllMocks();
    // Mock URL.createObjectURL
    URL.createObjectURL = vi.fn(() => 'blob:test-url');
  });

  it('renders with default label', () => {
    render(<ImageUpload onFileChange={mockOnFileChange} />);

    expect(screen.getByText('Cover Image')).toBeInTheDocument();
    expect(screen.getByText(/Drop image or click to upload/)).toBeInTheDocument();
  });

  it('renders with custom label', () => {
    render(
      <ImageUpload
        onFileChange={mockOnFileChange}
        label="Game Icon"
      />
    );

    expect(screen.getByText('Game Icon')).toBeInTheDocument();
  });

  it('shows file and URL mode buttons', () => {
    render(<ImageUpload onFileChange={mockOnFileChange} />);

    expect(screen.getByRole('button', { name: /file/i })).toBeInTheDocument();
    expect(screen.getByRole('button', { name: /url/i })).toBeInTheDocument();
  });

  it('switches between file and URL modes', async () => {
    render(
      <ImageUpload
        onFileChange={mockOnFileChange}
        onUrlChange={mockOnUrlChange}
      />
    );

    // Initially in file mode
    expect(screen.getByText(/Drop image or click to upload/)).toBeInTheDocument();

    // Switch to URL mode
    fireEvent.click(screen.getByRole('button', { name: /url/i }));

    await waitFor(() => {
      expect(screen.getByPlaceholderText(/https:\/\/example.com/)).toBeInTheDocument();
    });
  });

  it('validates file type', async () => {
    render(<ImageUpload onFileChange={mockOnFileChange} />);

    const file = new File(['test'], 'test.txt', { type: 'text/plain' });
    const input = document.querySelector('input[type="file"]') as HTMLInputElement;

    fireEvent.change(input, { target: { files: [file] } });

    await waitFor(() => {
      expect(mockOnFileChange).toHaveBeenCalledWith(null);
    });
  });

  it('validates file size', async () => {
    render(<ImageUpload onFileChange={mockOnFileChange} />);

    // Create a file that appears larger than 5MB
    const largeContent = new Array(6 * 1024 * 1024).fill('a').join('');
    const file = new File([largeContent], 'large.jpg', { type: 'image/jpeg' });

    const input = document.querySelector('input[type="file"]') as HTMLInputElement;
    fireEvent.change(input, { target: { files: [file] } });

    await waitFor(() => {
      expect(mockOnFileChange).toHaveBeenCalledWith(null);
    });
  });

  it('accepts valid image file', async () => {
    render(<ImageUpload onFileChange={mockOnFileChange} />);

    const file = new File(['test'], 'test.jpg', { type: 'image/jpeg' });
    const input = document.querySelector('input[type="file"]') as HTMLInputElement;

    fireEvent.change(input, { target: { files: [file] } });

    await waitFor(() => {
      expect(mockOnFileChange).toHaveBeenCalledWith(file);
    });
  });

  it('shows file info after selection', async () => {
    render(<ImageUpload onFileChange={mockOnFileChange} />);

    const file = new File(['test'], 'my-image.jpg', { type: 'image/jpeg' });
    const input = document.querySelector('input[type="file"]') as HTMLInputElement;

    fireEvent.change(input, { target: { files: [file] } });

    await waitFor(() => {
      expect(screen.getByText('my-image.jpg')).toBeInTheDocument();
    });
  });

  it('clears file when clear button clicked', async () => {
    render(<ImageUpload onFileChange={mockOnFileChange} />);

    const file = new File(['test'], 'test.jpg', { type: 'image/jpeg' });
    const input = document.querySelector('input[type="file"]') as HTMLInputElement;

    fireEvent.change(input, { target: { files: [file] } });

    await waitFor(() => {
      expect(screen.getByText('test.jpg')).toBeInTheDocument();
    });

    // Click clear button
    const clearButton = screen.getByRole('button', { name: /clear file/i });
    fireEvent.click(clearButton);

    await waitFor(() => {
      expect(mockOnFileChange).toHaveBeenLastCalledWith(null);
    });
  });

  it('handles URL input change', async () => {
    render(
      <ImageUpload
        onFileChange={mockOnFileChange}
        onUrlChange={mockOnUrlChange}
      />
    );

    // Switch to URL mode
    fireEvent.click(screen.getByRole('button', { name: /url/i }));

    const urlInput = await screen.findByPlaceholderText(/https:\/\/example.com/);
    fireEvent.change(urlInput, { target: { value: 'https://test.com/image.jpg' } });

    expect(mockOnUrlChange).toHaveBeenCalledWith('https://test.com/image.jpg');
  });

  it('shows upload progress when isUploading', () => {
    render(
      <ImageUpload
        onFileChange={mockOnFileChange}
        isUploading={true}
        uploadProgress={50}
      />
    );

    expect(screen.getByText(/Uploading... 50%/)).toBeInTheDocument();
  });

  it('shows current image preview when currentImageUrl provided', () => {
    render(
      <ImageUpload
        onFileChange={mockOnFileChange}
        currentImageUrl="https://example.com/existing.jpg"
      />
    );

    const img = screen.getByAltText('Preview');
    expect(img).toHaveAttribute('src', 'https://example.com/existing.jpg');
  });

  it('disables input when disabled prop is true', () => {
    render(
      <ImageUpload
        onFileChange={mockOnFileChange}
        disabled={true}
      />
    );

    const input = document.querySelector('input[type="file"]') as HTMLInputElement;
    expect(input).toBeDisabled();
  });

  it('handles drag over state', async () => {
    render(<ImageUpload onFileChange={mockOnFileChange} />);

    const dropZone = screen.getByRole('button', { name: /upload image/i });

    fireEvent.dragOver(dropZone, {
      dataTransfer: { files: [] },
    });

    await waitFor(() => {
      expect(screen.getByText('Drop image here')).toBeInTheDocument();
    });

    fireEvent.dragLeave(dropZone, {
      dataTransfer: { files: [] },
    });

    await waitFor(() => {
      expect(screen.getByText(/Drop image or click to upload/)).toBeInTheDocument();
    });
  });

  it('shows recommended size for icon type', () => {
    render(
      <ImageUpload
        onFileChange={mockOnFileChange}
        imageTypeHint="icon"
      />
    );

    expect(screen.getByText(/128×128 or 256×256/)).toBeInTheDocument();
  });

  it('shows recommended size for cover type', () => {
    render(
      <ImageUpload
        onFileChange={mockOnFileChange}
        imageTypeHint="cover"
      />
    );

    expect(screen.getByText(/800×600 or larger/)).toBeInTheDocument();
  });

  it('handles keyboard navigation', async () => {
    render(<ImageUpload onFileChange={mockOnFileChange} />);

    const dropZone = screen.getByRole('button', { name: /upload image/i });

    // Should be focusable
    dropZone.focus();
    expect(document.activeElement).toBe(dropZone);

    // Enter key should trigger click (file picker would open)
    fireEvent.keyDown(dropZone, { key: 'Enter' });
    // Space key should trigger click
    fireEvent.keyDown(dropZone, { key: ' ' });
  });
});
