/**
 * useAdminCategories - TanStack Query hooks for admin/categories CRUD.
 *
 * Issue #1440 — Phase 2 BE wiring. Replaces the Phase 1 hardcoded
 * INITIAL_CATEGORIES + local-state pattern in `categories-table.tsx`.
 *
 * Cache key: `['admin-categories']`. Mutations invalidate the same key
 * to refresh the list (no optimistic updates because the gameCount field
 * is derived server-side — only the server knows the post-mutation value).
 */

import {
  useMutation,
  useQuery,
  type UseMutationResult,
  type UseQueryResult,
} from '@tanstack/react-query';

import {
  createAdminCategory,
  deleteAdminCategory,
  listAdminCategories,
  updateAdminCategory,
  type CategoryDto,
  type CreateCategoryRequest,
  type UpdateCategoryRequest,
} from '@/lib/api/admin-categories';
import { getQueryClient } from '@/lib/queryClient';

export const adminCategoriesKeys = {
  all: ['admin-categories'] as const,
};

export function useAdminCategories(): UseQueryResult<CategoryDto[], Error> {
  return useQuery({
    queryKey: adminCategoriesKeys.all,
    queryFn: listAdminCategories,
    staleTime: 60_000,
  });
}

export function useCreateAdminCategory(): UseMutationResult<
  CategoryDto,
  Error,
  CreateCategoryRequest
> {
  const queryClient = getQueryClient();
  return useMutation({
    mutationFn: createAdminCategory,
    onSuccess: () => {
      void queryClient.invalidateQueries({ queryKey: adminCategoriesKeys.all });
    },
  });
}

export function useUpdateAdminCategory(): UseMutationResult<
  CategoryDto,
  Error,
  { id: string; payload: UpdateCategoryRequest }
> {
  const queryClient = getQueryClient();
  return useMutation({
    mutationFn: ({ id, payload }) => updateAdminCategory(id, payload),
    onSuccess: () => {
      void queryClient.invalidateQueries({ queryKey: adminCategoriesKeys.all });
    },
  });
}

export function useDeleteAdminCategory(): UseMutationResult<void, Error, string> {
  const queryClient = getQueryClient();
  return useMutation({
    mutationFn: deleteAdminCategory,
    onSuccess: () => {
      void queryClient.invalidateQueries({ queryKey: adminCategoriesKeys.all });
    },
  });
}
