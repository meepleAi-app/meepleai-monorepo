import { beforeEach, describe, expect, it, vi } from 'vitest';
import { z } from 'zod';

import { safeStorage } from '../safeStorage';

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

describe('safeStorage', () => {
  describe('get', () => {
    it('returns fallback when key does not exist', () => {
      const schema = z.string();
      expect(safeStorage.get('missing', schema, 'default')).toBe('default');
    });

    it('returns validated value when key exists and is valid', () => {
      localStorageMock.setItem('theme', JSON.stringify('dark'));
      const schema = z.enum(['light', 'dark', 'system']);
      expect(safeStorage.get('theme', schema, 'system')).toBe('dark');
    });

    it('returns fallback when stored value fails validation', () => {
      localStorageMock.setItem('theme', JSON.stringify('invalid-theme'));
      const schema = z.enum(['light', 'dark', 'system']);
      expect(safeStorage.get('theme', schema, 'system')).toBe('system');
    });

    it('returns fallback when stored value is not valid JSON', () => {
      localStorageMock.setItem('broken', 'not-json{{{');
      const schema = z.string();
      expect(safeStorage.get('broken', schema, 'fallback')).toBe('fallback');
    });

    it('validates complex object schemas', () => {
      const UserSchema = z.object({
        name: z.string(),
        age: z.number().min(0),
      });
      const validUser = { name: 'Alice', age: 30 };
      localStorageMock.setItem('user', JSON.stringify(validUser));
      expect(safeStorage.get('user', UserSchema, { name: '', age: 0 })).toEqual(validUser);
    });

    it('rejects invalid complex objects and returns fallback', () => {
      const UserSchema = z.object({
        name: z.string(),
        age: z.number().min(0),
      });
      const invalidUser = { name: 123, age: -5 };
      localStorageMock.setItem('user', JSON.stringify(invalidUser));
      const fallback = { name: '', age: 0 };
      expect(safeStorage.get('user', UserSchema, fallback)).toEqual(fallback);
    });

    it('reads from sessionStorage when type is session', () => {
      localStorageMock.setItem('key', JSON.stringify('value'));
      const schema = z.string();
      expect(safeStorage.get('key', schema, 'default', 'session')).toBe('value');
    });

    it('validates arrays', () => {
      const schema = z.array(z.string());
      localStorageMock.setItem('tags', JSON.stringify(['a', 'b', 'c']));
      expect(safeStorage.get('tags', schema, [])).toEqual(['a', 'b', 'c']);
    });

    it('rejects arrays with wrong element types', () => {
      const schema = z.array(z.string());
      localStorageMock.setItem('tags', JSON.stringify([1, 2, 3]));
      expect(safeStorage.get('tags', schema, [])).toEqual([]);
    });
  });

  describe('getRaw', () => {
    it('returns raw string without JSON parsing', () => {
      localStorageMock.setItem('token', 'abc123');
      expect(safeStorage.getRaw('token')).toBe('abc123');
    });

    it('returns fallback when key is missing', () => {
      expect(safeStorage.getRaw('missing', 'default')).toBe('default');
    });
  });

  describe('set', () => {
    it('writes JSON-serialized value', () => {
      safeStorage.set('key', { foo: 'bar' });
      expect(localStorageMock.setItem).toHaveBeenCalledWith('key', '{"foo":"bar"}');
    });

    it('returns true on success', () => {
      expect(safeStorage.set('key', 'value')).toBe(true);
    });

    it('returns false when storage throws', () => {
      localStorageMock.setItem.mockImplementationOnce(() => {
        throw new Error('QuotaExceeded');
      });
      expect(safeStorage.set('key', 'value')).toBe(false);
    });
  });

  describe('setRaw', () => {
    it('writes raw string without JSON serialization', () => {
      safeStorage.setRaw('token', 'abc123');
      expect(localStorageMock.setItem).toHaveBeenCalledWith('token', 'abc123');
    });
  });

  describe('remove', () => {
    it('removes key from storage', () => {
      localStorageMock.setItem('key', 'value');
      safeStorage.remove('key');
      expect(localStorageMock.removeItem).toHaveBeenCalledWith('key');
    });
  });

  describe('has', () => {
    it('returns true when key exists', () => {
      localStorageMock.setItem('key', 'value');
      expect(safeStorage.has('key')).toBe(true);
    });

    it('returns false when key does not exist', () => {
      expect(safeStorage.has('missing')).toBe(false);
    });
  });
});
