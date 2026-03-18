/**
 * Tests for useTestResults hooks
 * Issue #3379: Test Results History & Persistence
 */

import { renderHook, waitFor } from '@testing-library/react';
import { QueryClient, QueryClientProvider } from '@tanstack/react-query';
import { describe, it, expect, vi, beforeEach, type Mock } from 'vitest';
import * as React from 'react';

import { api } from '@/lib/api';
import {
  useTestResults,
  useTestResult,
  useSaveTestResult,
  useDeleteTestResult,
  TEST_RESULTS_QUERY_KEYS,
} from '@/lib/domain-hooks/useTestResults';
import type { TestResult, TestResultList } from '@/lib/api/schemas/test-results.schemas';

// Mock the API module
vi.mock('@/lib/api', () => ({
  api: {
    testResults: {
      getAll: vi.fn(),
      getById: vi.fn(),
      save: vi.fn(),
      delete: vi.fn(),
    },
  },
}));

// Mock result data
const mockTestResult: TestResult = {
  id: '123e4567-e89b-12d3-a456-426614174000',
  typologyId: '123e4567-e89b-12d3-a456-426614174001',
  strategyOverride: null,
  modelUsed: 'gpt-4',
  query: 'Test query',
  response: 'Test response',
  confidenceScore: 0.95,
  tokensUsed: 150,
  costEstimate: 0.003,
  latencyMs: 1200,
  citationsJson: null,
  executedAt: '2025-01-15T10:30:00Z',
  executedBy: '123e4567-e89b-12d3-a456-426614174002',
  notes: null,
  isSaved: true,
};

const mockTestResultList: TestResultList = {
  items: [mockTestResult],
  totalCount: 1,
  skip: 0,
  take: 20,
};

// Create wrapper with QueryClientProvider
function createWrapper() {
  const queryClient = new QueryClient({
    defaultOptions: {
      queries: {
        retry: false,
      },
    },
  });
  return function Wrapper({ children }: { children: React.ReactNode }) {
    return React.createElement(QueryClientProvider, { client: queryClient }, children);
  };
}

describe('useTestResults hooks', () => {
  beforeEach(() => {
    vi.clearAllMocks();
  });

  describe('TEST_RESULTS_QUERY_KEYS', () => {
    it('generates correct query keys', () => {
      expect(TEST_RESULTS_QUERY_KEYS.all).toEqual(['testResults']);
      expect(TEST_RESULTS_QUERY_KEYS.list()).toEqual(['testResults', 'list', undefined]);
      expect(TEST_RESULTS_QUERY_KEYS.list({ skip: 0, take: 10 })).toEqual([
        'testResults',
        'list',
        { skip: 0, take: 10 },
      ]);
      expect(TEST_RESULTS_QUERY_KEYS.detail('123')).toEqual(['testResults', 'detail', '123']);
    });
  });

  describe('useTestResults', () => {
    it('fetches test results successfully', async () => {
      (api.testResults.getAll as Mock).mockResolvedValue(mockTestResultList);

      const { result } = renderHook(() => useTestResults(), {
        wrapper: createWrapper(),
      });

      expect(result.current.isLoading).toBe(true);

      await waitFor(() => {
        expect(result.current.isLoading).toBe(false);
      });

      expect(result.current.data).toEqual(mockTestResultList);
      expect(api.testResults.getAll).toHaveBeenCalledWith(undefined);
    });

    it('passes query parameters correctly', async () => {
      (api.testResults.getAll as Mock).mockResolvedValue(mockTestResultList);

      const query = { typologyId: 'type-123', savedOnly: true, skip: 10, take: 20 };

      const { result } = renderHook(() => useTestResults(query), {
        wrapper: createWrapper(),
      });

      await waitFor(() => {
        expect(result.current.isLoading).toBe(false);
      });

      expect(api.testResults.getAll).toHaveBeenCalledWith(query);
    });

    it('handles errors correctly', async () => {
      const error = new Error('Failed to fetch');
      (api.testResults.getAll as Mock).mockRejectedValue(error);

      const { result } = renderHook(() => useTestResults(), {
        wrapper: createWrapper(),
      });

      await waitFor(() => {
        expect(result.current.isLoading).toBe(false);
      });

      expect(result.current.error).toBeTruthy();
    });
  });

  describe('useTestResult', () => {
    it('fetches a single test result by ID', async () => {
      (api.testResults.getById as Mock).mockResolvedValue(mockTestResult);

      const { result } = renderHook(() => useTestResult('123'), {
        wrapper: createWrapper(),
      });

      await waitFor(() => {
        expect(result.current.isLoading).toBe(false);
      });

      expect(result.current.data).toEqual(mockTestResult);
      expect(api.testResults.getById).toHaveBeenCalledWith('123');
    });

    it('does not fetch when ID is empty', async () => {
      const { result } = renderHook(() => useTestResult(''), {
        wrapper: createWrapper(),
      });

      // Should not be loading because query is disabled
      expect(result.current.isLoading).toBe(false);
      expect(api.testResults.getById).not.toHaveBeenCalled();
    });

    it('handles null response (not found)', async () => {
      (api.testResults.getById as Mock).mockResolvedValue(null);

      const { result } = renderHook(() => useTestResult('nonexistent'), {
        wrapper: createWrapper(),
      });

      await waitFor(() => {
        expect(result.current.isLoading).toBe(false);
      });

      expect(result.current.data).toBeNull();
    });
  });

  describe('useSaveTestResult', () => {
    it('saves a test result successfully', async () => {
      (api.testResults.save as Mock).mockResolvedValue({ id: 'new-123' });

      const { result } = renderHook(() => useSaveTestResult(), {
        wrapper: createWrapper(),
      });

      const request = {
        typologyId: 'type-123',
        query: 'Test query',
        response: 'Test response',
        modelUsed: 'gpt-4',
        confidenceScore: 0.9,
        tokensUsed: 100,
        costEstimate: 0.002,
        latencyMs: 1000,
      };

      result.current.mutate(request);

      await waitFor(() => {
        expect(result.current.isSuccess).toBe(true);
      });

      expect(api.testResults.save).toHaveBeenCalledWith(request);
      expect(result.current.data).toEqual({ id: 'new-123' });
    });

    it('handles save errors', async () => {
      const error = new Error('Save failed');
      (api.testResults.save as Mock).mockRejectedValue(error);

      const { result } = renderHook(() => useSaveTestResult(), {
        wrapper: createWrapper(),
      });

      result.current.mutate({
        typologyId: 'type-123',
        query: 'Test',
        response: 'Response',
        modelUsed: 'gpt-4',
        confidenceScore: 0.9,
        tokensUsed: 100,
        costEstimate: 0.002,
        latencyMs: 1000,
      });

      await waitFor(() => {
        expect(result.current.isError).toBe(true);
      });

      expect(result.current.error?.message).toBe('Save failed');
    });
  });

  describe('useDeleteTestResult', () => {
    it('deletes a test result successfully', async () => {
      (api.testResults.delete as Mock).mockResolvedValue(undefined);

      const { result } = renderHook(() => useDeleteTestResult(), {
        wrapper: createWrapper(),
      });

      result.current.mutate('123');

      await waitFor(() => {
        expect(result.current.isSuccess).toBe(true);
      });

      expect(api.testResults.delete).toHaveBeenCalledWith('123');
    });

    it('handles delete errors', async () => {
      const error = new Error('Delete failed');
      (api.testResults.delete as Mock).mockRejectedValue(error);

      const { result } = renderHook(() => useDeleteTestResult(), {
        wrapper: createWrapper(),
      });

      result.current.mutate('123');

      await waitFor(() => {
        expect(result.current.isError).toBe(true);
      });

      expect(result.current.error?.message).toBe('Delete failed');
    });
  });
});
