import { act, renderHook } from '@testing-library/react';
import { beforeEach, describe, expect, it, vi } from 'vitest';
import { z } from 'zod';

import { useValidatedStorage } from '../useValidatedStorage';

// Mock localStorage
const localStorageMock = (() => {
  let store: Record<string, string> = {};
  return {
    getItem: vi.fn((key: string) => store[key] ?? null),
    setItem: vi.fn((key: string, value: string) => {
      store[key] = value;
    }),
    removeItem: vi.fn((key: string) => {
      delete store[key];
    }),
    clear: vi.fn(() => {
      store = {};
    }),
    get length() {
      return Object.keys(store).length;
    },
    key: vi.fn((i: number) => Object.keys(store)[i] ?? null),
  };
})();

Object.defineProperty(window, 'localStorage', { value: localStorageMock });
Object.defineProperty(window, 'sessionStorage', { value: localStorageMock });

beforeEach(() => {
  localStorageMock.clear();
  vi.clearAllMocks();
});

const ThemeSchema = z.enum(['light', 'dark', 'system']);
type Theme = z.infer<typeof ThemeSchema>;

describe('useValidatedStorage', () => {
  it('returns fallback when storage is empty', () => {
    const { result } = renderHook(() =>
      useValidatedStorage('theme', ThemeSchema, 'system' as Theme)
    );
    expect(result.current[0]).toBe('system');
  });

  it('returns validated value from storage', () => {
    localStorageMock.setItem('theme', JSON.stringify('dark'));
    const { result } = renderHook(() =>
      useValidatedStorage('theme', ThemeSchema, 'system' as Theme)
    );
    expect(result.current[0]).toBe('dark');
  });

  it('returns fallback when stored value is invalid', () => {
    localStorageMock.setItem('theme', JSON.stringify('invalid'));
    const { result } = renderHook(() =>
      useValidatedStorage('theme', ThemeSchema, 'system' as Theme)
    );
    expect(result.current[0]).toBe('system');
  });

  it('updates value and persists to storage', () => {
    const { result } = renderHook(() =>
      useValidatedStorage('theme', ThemeSchema, 'system' as Theme)
    );

    act(() => {
      result.current[1]('dark');
    });

    expect(result.current[0]).toBe('dark');
    expect(localStorageMock.setItem).toHaveBeenCalledWith('theme', JSON.stringify('dark'));
  });

  it('supports function updater pattern', () => {
    localStorageMock.setItem('theme', JSON.stringify('light'));
    const { result } = renderHook(() =>
      useValidatedStorage('theme', ThemeSchema, 'system' as Theme)
    );

    act(() => {
      result.current[1](prev => (prev === 'light' ? 'dark' : 'light'));
    });

    expect(result.current[0]).toBe('dark');
  });

  it('syncs across tabs via storage event', () => {
    const { result } = renderHook(() =>
      useValidatedStorage('theme', ThemeSchema, 'system' as Theme)
    );

    act(() => {
      window.dispatchEvent(
        new StorageEvent('storage', {
          key: 'theme',
          newValue: JSON.stringify('dark'),
        })
      );
    });

    expect(result.current[0]).toBe('dark');
  });

  it('ignores cross-tab events for other keys', () => {
    const { result } = renderHook(() =>
      useValidatedStorage('theme', ThemeSchema, 'system' as Theme)
    );

    act(() => {
      window.dispatchEvent(
        new StorageEvent('storage', {
          key: 'other-key',
          newValue: JSON.stringify('dark'),
        })
      );
    });

    expect(result.current[0]).toBe('system');
  });

  it('resets to fallback when key is deleted cross-tab', () => {
    localStorageMock.setItem('theme', JSON.stringify('dark'));
    const { result } = renderHook(() =>
      useValidatedStorage('theme', ThemeSchema, 'system' as Theme)
    );
    expect(result.current[0]).toBe('dark');

    act(() => {
      window.dispatchEvent(
        new StorageEvent('storage', {
          key: 'theme',
          newValue: null,
        })
      );
    });

    expect(result.current[0]).toBe('system');
  });

  it('rejects invalid cross-tab values', () => {
    const { result } = renderHook(() =>
      useValidatedStorage('theme', ThemeSchema, 'system' as Theme)
    );

    act(() => {
      window.dispatchEvent(
        new StorageEvent('storage', {
          key: 'theme',
          newValue: JSON.stringify('invalid-theme'),
        })
      );
    });

    // Should remain at fallback since 'invalid-theme' fails validation
    expect(result.current[0]).toBe('system');
  });

  it('works with complex object schemas', () => {
    const UserSchema = z.object({ name: z.string(), age: z.number() });
    const fallback = { name: '', age: 0 };

    localStorageMock.setItem('user', JSON.stringify({ name: 'Alice', age: 30 }));

    const { result } = renderHook(() => useValidatedStorage('user', UserSchema, fallback));

    expect(result.current[0]).toEqual({ name: 'Alice', age: 30 });
  });
});
