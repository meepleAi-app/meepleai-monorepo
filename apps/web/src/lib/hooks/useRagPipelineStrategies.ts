'use client';

/**
 * RAG Pipeline Strategies Hook
 *
 * TanStack Query hooks for CRUD operations on RAG pipeline strategies.
 * Issue #3464: Save/load/export for custom strategies.
 */

import { useQuery, useMutation, useQueryClient } from '@tanstack/react-query';
import { useCallback } from 'react';

// =============================================================================
// Types
// =============================================================================

export interface RagPipelineStrategyListItem {
  id: string;
  name: string;
  description: string;
  version: string;
  isTemplate: boolean;
  templateCategory: string | null;
  tags: string[];
  isActive: boolean;
  createdAt: string;
  updatedAt: string;
}

export interface RagPipelineStrategyDetail extends RagPipelineStrategyListItem {
  nodesJson: string;
  edgesJson: string;
  createdByUserId: string;
}

export interface SaveStrategyRequest {
  name: string;
  description: string;
  nodesJson: string;
  edgesJson: string;
  tags?: string[];
}

export interface SaveStrategyResult {
  id: string;
  name: string;
  version: string;
  updatedAt: string;
}

export interface ImportStrategyRequest {
  jsonContent: string;
  newName?: string;
}

export interface ExportedStrategy {
  name: string;
  description: string;
  version: string;
  nodesJson: string;
  edgesJson: string;
  tags: string[];
  exportedAt: string;
}

// =============================================================================
// API Functions
// =============================================================================

const API_BASE = '/api/v1/admin/rag-pipeline/strategies';

async function fetchStrategies(
  search?: string,
  includeTemplates = true
): Promise<RagPipelineStrategyListItem[]> {
  const params = new URLSearchParams();
  if (search) params.set('search', search);
  params.set('includeTemplates', String(includeTemplates));

  const response = await fetch(`${API_BASE}?${params.toString()}`, {
    credentials: 'include',
  });

  if (!response.ok) {
    throw new Error(`Failed to fetch strategies: ${response.statusText}`);
  }

  return response.json();
}

async function fetchStrategyById(id: string): Promise<RagPipelineStrategyDetail | null> {
  const response = await fetch(`${API_BASE}/${id}`, {
    credentials: 'include',
  });

  if (response.status === 404) {
    return null;
  }

  if (!response.ok) {
    throw new Error(`Failed to fetch strategy: ${response.statusText}`);
  }

  return response.json();
}

async function createStrategy(data: SaveStrategyRequest): Promise<SaveStrategyResult> {
  const response = await fetch(API_BASE, {
    method: 'POST',
    headers: { 'Content-Type': 'application/json' },
    credentials: 'include',
    body: JSON.stringify(data),
  });

  if (!response.ok) {
    const error = await response.json().catch(() => ({ message: response.statusText }));
    throw new Error(error.message || 'Failed to create strategy');
  }

  return response.json();
}

async function updateStrategy(
  id: string,
  data: SaveStrategyRequest
): Promise<SaveStrategyResult> {
  const response = await fetch(`${API_BASE}/${id}`, {
    method: 'PUT',
    headers: { 'Content-Type': 'application/json' },
    credentials: 'include',
    body: JSON.stringify(data),
  });

  if (!response.ok) {
    const error = await response.json().catch(() => ({ message: response.statusText }));
    throw new Error(error.message || 'Failed to update strategy');
  }

  return response.json();
}

async function deleteStrategy(id: string): Promise<void> {
  const response = await fetch(`${API_BASE}/${id}`, {
    method: 'DELETE',
    credentials: 'include',
  });

  if (!response.ok) {
    throw new Error(`Failed to delete strategy: ${response.statusText}`);
  }
}

async function exportStrategy(id: string): Promise<{ blob: Blob; filename: string }> {
  const response = await fetch(`${API_BASE}/${id}/export`, {
    credentials: 'include',
  });

  if (!response.ok) {
    throw new Error(`Failed to export strategy: ${response.statusText}`);
  }

  const contentDisposition = response.headers.get('Content-Disposition');
  let filename = 'strategy.json';
  if (contentDisposition) {
    const match = contentDisposition.match(/filename="?([^";\n]+)"?/);
    if (match) {
      filename = match[1];
    }
  }

  const blob = await response.blob();
  return { blob, filename };
}

async function importStrategy(data: ImportStrategyRequest): Promise<SaveStrategyResult> {
  const response = await fetch(`${API_BASE}/import`, {
    method: 'POST',
    headers: { 'Content-Type': 'application/json' },
    credentials: 'include',
    body: JSON.stringify(data),
  });

  if (!response.ok) {
    const error = await response.json().catch(() => ({ message: response.statusText }));
    throw new Error(error.message || 'Failed to import strategy');
  }

  return response.json();
}

// =============================================================================
// Query Keys
// =============================================================================

export const STRATEGIES_QUERY_KEY = ['admin', 'rag-pipeline', 'strategies'] as const;

export function strategyDetailKey(id: string) {
  return [...STRATEGIES_QUERY_KEY, id] as const;
}

// =============================================================================
// Hooks
// =============================================================================

export interface UseRagPipelineStrategiesOptions {
  search?: string;
  includeTemplates?: boolean;
  enabled?: boolean;
}

/**
 * Hook for listing RAG pipeline strategies.
 */
export function useRagPipelineStrategies(options?: UseRagPipelineStrategiesOptions) {
  const { search, includeTemplates = true, enabled = true } = options || {};

  return useQuery({
    queryKey: [...STRATEGIES_QUERY_KEY, { search, includeTemplates }],
    queryFn: () => fetchStrategies(search, includeTemplates),
    enabled,
    staleTime: 30_000, // 30 seconds
  });
}

/**
 * Hook for fetching a single strategy by ID.
 */
export function useRagPipelineStrategy(id: string | null, options?: { enabled?: boolean }) {
  const { enabled = true } = options || {};

  return useQuery({
    queryKey: strategyDetailKey(id ?? ''),
    queryFn: () => (id ? fetchStrategyById(id) : null),
    enabled: enabled && !!id,
    staleTime: 60_000, // 1 minute
  });
}

/**
 * Hook for creating a new strategy.
 */
export function useCreateStrategy() {
  const queryClient = useQueryClient();

  return useMutation({
    mutationFn: createStrategy,
    onSuccess: () => {
      queryClient.invalidateQueries({ queryKey: STRATEGIES_QUERY_KEY });
    },
  });
}

/**
 * Hook for updating an existing strategy.
 */
export function useUpdateStrategy() {
  const queryClient = useQueryClient();

  return useMutation({
    mutationFn: ({ id, ...data }: SaveStrategyRequest & { id: string }) =>
      updateStrategy(id, data),
    onSuccess: (result, variables) => {
      queryClient.invalidateQueries({ queryKey: STRATEGIES_QUERY_KEY });
      queryClient.invalidateQueries({ queryKey: strategyDetailKey(variables.id) });
    },
  });
}

/**
 * Hook for deleting a strategy.
 */
export function useDeleteStrategy() {
  const queryClient = useQueryClient();

  return useMutation({
    mutationFn: deleteStrategy,
    onSuccess: () => {
      queryClient.invalidateQueries({ queryKey: STRATEGIES_QUERY_KEY });
    },
  });
}

/**
 * Hook for importing a strategy from JSON.
 */
export function useImportStrategy() {
  const queryClient = useQueryClient();

  return useMutation({
    mutationFn: importStrategy,
    onSuccess: () => {
      queryClient.invalidateQueries({ queryKey: STRATEGIES_QUERY_KEY });
    },
  });
}

/**
 * Hook that provides save/load/export functionality combined.
 */
export function useStrategyPersistence() {
  const queryClient = useQueryClient();
  const createMutation = useCreateStrategy();
  const updateMutation = useUpdateStrategy();
  const deleteMutation = useDeleteStrategy();
  const importMutation = useImportStrategy();

  const save = useCallback(
    async (
      data: SaveStrategyRequest,
      existingId?: string
    ): Promise<SaveStrategyResult> => {
      if (existingId) {
        return updateMutation.mutateAsync({ id: existingId, ...data });
      }
      return createMutation.mutateAsync(data);
    },
    [createMutation, updateMutation]
  );

  const load = useCallback(
    async (id: string): Promise<RagPipelineStrategyDetail | null> => {
      return fetchStrategyById(id);
    },
    []
  );

  const exportToFile = useCallback(async (id: string): Promise<void> => {
    const { blob, filename } = await exportStrategy(id);

    // Create download link
    const url = URL.createObjectURL(blob);
    const link = document.createElement('a');
    link.href = url;
    link.download = filename;
    document.body.appendChild(link);
    link.click();
    document.body.removeChild(link);
    URL.revokeObjectURL(url);
  }, []);

  const importFromFile = useCallback(
    async (file: File, newName?: string): Promise<SaveStrategyResult> => {
      const jsonContent = await file.text();
      return importMutation.mutateAsync({ jsonContent, newName });
    },
    [importMutation]
  );

  const importFromJson = useCallback(
    async (json: string, newName?: string): Promise<SaveStrategyResult> => {
      return importMutation.mutateAsync({ jsonContent: json, newName });
    },
    [importMutation]
  );

  const remove = useCallback(
    async (id: string): Promise<void> => {
      await deleteMutation.mutateAsync(id);
    },
    [deleteMutation]
  );

  const refresh = useCallback(() => {
    queryClient.invalidateQueries({ queryKey: STRATEGIES_QUERY_KEY });
  }, [queryClient]);

  return {
    save,
    load,
    exportToFile,
    importFromFile,
    importFromJson,
    remove,
    refresh,
    isSaving: createMutation.isPending || updateMutation.isPending,
    isDeleting: deleteMutation.isPending,
    isImporting: importMutation.isPending,
    saveError: createMutation.error || updateMutation.error,
    deleteError: deleteMutation.error,
    importError: importMutation.error,
  };
}

export default useStrategyPersistence;
