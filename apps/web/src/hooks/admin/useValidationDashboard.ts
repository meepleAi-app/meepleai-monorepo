/**
 * Mechanic Extractor — AI Comprehension Validation (ADR-051 Sprint 1 / Task 35)
 *
 * Query hook: per-game certification dashboard rows.
 * Wraps `api.admin.getDashboard()`.
 */

'use client';

import { useQuery, type UseQueryResult } from '@tanstack/react-query';

import { api } from '@/lib/api';
import type { ValidationDashboardDto } from '@/lib/api/schemas/admin-mechanic-extractor-validation.schemas';

import { mechanicValidationKeys } from './mechanicValidationKeys';

export function useValidationDashboard(): UseQueryResult<ValidationDashboardDto, Error> {
  return useQuery({
    queryKey: mechanicValidationKeys.dashboard.summary(),
    queryFn: () => api.admin.getDashboard(),
    staleTime: 60_000, // 60s
  });
}
