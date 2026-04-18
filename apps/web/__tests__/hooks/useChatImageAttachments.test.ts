/**
 * Tests for useChatImageAttachments hook.
 * Session Vision AI feature.
 */

import { describe, it, expect, vi, beforeEach } from 'vitest';
import { renderHook, act } from '@testing-library/react';

import { useChatImageAttachments } from '@/hooks/useChatImageAttachments';

// Mock URL.createObjectURL / revokeObjectURL
const mockCreateObjectURL = vi.fn((file: Blob) => `blob:mock-${Math.random()}`);
const mockRevokeObjectURL = vi.fn();

beforeEach(() => {
  vi.clearAllMocks();
  globalThis.URL.createObjectURL = mockCreateObjectURL;
  globalThis.URL.revokeObjectURL = mockRevokeObjectURL;
});

function createMockFile(name: string, size: number, type: string): File {
  const buffer = new ArrayBuffer(size);
  return new File([buffer], name, { type });
}

describe('useChatImageAttachments', () => {
  it('starts with empty images', () => {
    const { result } = renderHook(() => useChatImageAttachments());

    expect(result.current.images).toEqual([]);
    expect(result.current.hasImages).toBe(false);
    expect(result.current.canAddMore).toBe(true);
  });

  it('adds valid image and returns null error', () => {
    const { result } = renderHook(() => useChatImageAttachments());
    const file = createMockFile('photo.jpg', 1024, 'image/jpeg');

    let error: string | null = null;
    act(() => {
      error = result.current.addImage(file);
    });

    expect(error).toBeNull();
    expect(result.current.images).toHaveLength(1);
    expect(result.current.images[0].file).toBe(file);
    expect(result.current.images[0].mediaType).toBe('image/jpeg');
    expect(result.current.hasImages).toBe(true);
    expect(mockCreateObjectURL).toHaveBeenCalledOnce();
  });

  it('rejects unsupported file types with error string', () => {
    const { result } = renderHook(() => useChatImageAttachments());
    const file = createMockFile('doc.pdf', 1024, 'application/pdf');

    let error: string | null = null;
    act(() => {
      error = result.current.addImage(file);
    });

    expect(error).toBeTypeOf('string');
    expect(error).toBeTruthy();
    expect(result.current.images).toHaveLength(0);
  });

  it('rejects oversized files', () => {
    const { result } = renderHook(() => useChatImageAttachments());
    // 11MB exceeds the 10MB limit
    const file = createMockFile('huge.jpg', 11 * 1024 * 1024, 'image/jpeg');

    let error: string | null = null;
    act(() => {
      error = result.current.addImage(file);
    });

    expect(error).toBeTypeOf('string');
    expect(error).toBeTruthy();
    expect(result.current.images).toHaveLength(0);
  });

  it('respects max limit and canAddMore becomes false', () => {
    const { result } = renderHook(() => useChatImageAttachments(2));

    act(() => {
      result.current.addImage(createMockFile('a.jpg', 100, 'image/jpeg'));
    });
    act(() => {
      result.current.addImage(createMockFile('b.jpg', 100, 'image/jpeg'));
    });

    expect(result.current.images).toHaveLength(2);
    expect(result.current.canAddMore).toBe(false);

    // Adding a third should not increase the count (state update capped in the hook)
    act(() => {
      result.current.addImage(createMockFile('c.jpg', 100, 'image/jpeg'));
    });
    expect(result.current.images).toHaveLength(2);
  });

  it('removeImage revokes URL', () => {
    const { result } = renderHook(() => useChatImageAttachments());

    act(() => {
      result.current.addImage(createMockFile('a.jpg', 100, 'image/jpeg'));
      result.current.addImage(createMockFile('b.jpg', 100, 'image/png'));
    });

    const previewUrl = result.current.images[0].previewUrl;

    act(() => {
      result.current.removeImage(0);
    });

    expect(mockRevokeObjectURL).toHaveBeenCalledWith(previewUrl);
    expect(result.current.images).toHaveLength(1);
  });

  it('clearImages revokes all URLs', () => {
    const { result } = renderHook(() => useChatImageAttachments());

    act(() => {
      result.current.addImage(createMockFile('a.jpg', 100, 'image/jpeg'));
      result.current.addImage(createMockFile('b.jpg', 100, 'image/png'));
    });

    expect(result.current.images).toHaveLength(2);

    act(() => {
      result.current.clearImages();
    });

    expect(mockRevokeObjectURL).toHaveBeenCalledTimes(2);
    expect(result.current.images).toHaveLength(0);
    expect(result.current.hasImages).toBe(false);
  });
});
