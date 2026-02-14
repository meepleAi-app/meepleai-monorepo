/**
 * Mock for swr (not installed as dependency)
 * Used by Step3BggMatch component
 */

import { vi } from 'vitest';

const useSWR = vi.fn((_key: unknown, _fetcher?: unknown, _config?: unknown) => ({
  data: undefined,
  error: undefined,
  isLoading: false,
  isValidating: false,
  mutate: vi.fn(),
}));

export default useSWR;
