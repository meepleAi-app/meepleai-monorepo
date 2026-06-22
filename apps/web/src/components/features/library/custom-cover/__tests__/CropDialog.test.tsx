import { render, screen, fireEvent, waitFor } from '@testing-library/react';
import { describe, it, expect, vi, beforeAll, beforeEach } from 'vitest';

import { CropDialog } from '../CropDialog';

// Stub URL.createObjectURL / revokeObjectURL unconditionally so they are
// always vi.fn() spies. vitest v4 exposes the native Node.js implementation
// which (a) strictly requires a real Blob and (b) is not a spy, causing
// "is not a spy or a call to a spy!" errors on expect(URL.revokeObjectURL).
// @ts-expect-error — override for vitest v4 / jsdom compat
URL.createObjectURL = vi.fn(() => 'blob:mock');
// @ts-expect-error — override for vitest v4 / jsdom compat
URL.revokeObjectURL = vi.fn();

// Reset mocks before each test.
beforeEach(() => {
  vi.clearAllMocks();
});

// Mock react-easy-crop: it needs real browser canvas / ResizeObserver.
vi.mock('react-easy-crop', () => ({
  default: ({ onCropComplete }: { onCropComplete?: (area: unknown, pixels: unknown) => void }) => {
    return (
      <div data-testid="mock-cropper">
        <button
          type="button"
          onClick={() => onCropComplete?.({}, { x: 0, y: 0, width: 200, height: 300 })}
        >
          simulate-crop
        </button>
      </div>
    );
  },
}));

// jsdom canvas stub: toBlob returns a small webp-like blob so the
// quality dial-down loop terminates successfully on first iteration.
beforeAll(() => {
  // HTMLImageElement.onload never fires in jsdom. Stub loadImage path
  // by faking the Image constructor so it resolves immediately.
  Object.defineProperty(global, 'Image', {
    writable: true,
    value: class MockImage {
      onload: (() => void) | null = null;
      onerror: (() => void) | null = null;
      src = '';
      constructor() {
        // When src is set trigger onload on next microtask.
        Object.defineProperty(this, 'src', {
          set: (val: string) => {
            // store internally
            Object.defineProperty(this, '_src', { value: val, writable: true });
            queueMicrotask(() => this.onload?.());
          },
          get: () => (this as unknown as { _src: string })._src,
        });
      }
    },
  });

  // Stub HTMLCanvasElement.getContext so ctx.drawImage is a no-op.
  HTMLCanvasElement.prototype.getContext = vi.fn().mockReturnValue({
    drawImage: vi.fn(),
  }) as unknown as typeof HTMLCanvasElement.prototype.getContext;

  // Stub toBlob to return a 100-byte blob (fits ≤200KB immediately).
  HTMLCanvasElement.prototype.toBlob = vi.fn(
    (cb: BlobCallback, _type?: string, _quality?: number) => {
      cb(new Blob([new Uint8Array(100)], { type: 'image/webp' }));
    }
  );
});

describe('CropDialog', () => {
  const mockImage = new File(
    [new Blob([new ArrayBuffer(100)], { type: 'image/jpeg' })],
    'test.jpg',
    { type: 'image/jpeg' }
  );

  it('renders cropper, confirm + cancel buttons', () => {
    render(
      <CropDialog open={true} imageFile={mockImage} onConfirm={() => {}} onCancel={() => {}} />
    );
    expect(screen.getByTestId('mock-cropper')).toBeInTheDocument();
    expect(screen.getByRole('button', { name: /conferma/i })).toBeInTheDocument();
    expect(screen.getByRole('button', { name: /annulla/i })).toBeInTheDocument();
  });

  it('revokes blob URL on unmount to prevent memory leak', () => {
    const { unmount } = render(
      <CropDialog open={true} imageFile={mockImage} onConfirm={() => {}} onCancel={() => {}} />
    );
    expect(URL.revokeObjectURL).not.toHaveBeenCalled();
    unmount();
    expect(URL.revokeObjectURL).toHaveBeenCalledOnce();
    // Verify the URL passed to revokeObjectURL is a string (was created by createObjectURL).
    const [revokedUrl] = (URL.revokeObjectURL as any).mock.calls[0];
    expect(typeof revokedUrl).toBe('string');
  });

  it('calls onCancel when cancel button is clicked', () => {
    const onCancel = vi.fn();
    render(
      <CropDialog open={true} imageFile={mockImage} onConfirm={() => {}} onCancel={onCancel} />
    );
    fireEvent.click(screen.getByRole('button', { name: /annulla/i }));
    expect(onCancel).toHaveBeenCalled();
  });

  it('calls onConfirm with cropped blob when confirm clicked after crop', async () => {
    const onConfirm = vi.fn();
    render(
      <CropDialog open={true} imageFile={mockImage} onConfirm={onConfirm} onCancel={() => {}} />
    );
    // Trigger onCropComplete via mock cropper button.
    fireEvent.click(screen.getByText('simulate-crop'));
    // Confirm button enabled now; click it.
    fireEvent.click(screen.getByRole('button', { name: /conferma/i }));
    await waitFor(() => expect(onConfirm).toHaveBeenCalledOnce());
    const [blob] = onConfirm.mock.calls[0] as [Blob];
    expect(blob).toBeInstanceOf(Blob);
  });
});
