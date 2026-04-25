/**
 * Mechanic Extractor — AI Comprehension Validation (ADR-051 Sprint 2 / Task 16)
 *
 * Query hook: singleton `CertificationThresholds` value object.
 * Wraps `api.admin.getThresholds()`.
 *
 * Cache key is shared with `useUpdateThresholds`'s invalidation list so that
 * a successful PUT immediately refetches the bounds rendered by the admin
 * surface mounting `ThresholdsConfigForm`.
 */

'use client';

import { useQuery, type UseQueryResult } from '@tanstack/react-query';

import { api } from '@/lib/api';
import type { CertificationThresholdsDto } from '@/lib/api/schemas/admin-mechanic-extractor-validation.schemas';

import { mechanicValidationKeys } from './mechanicValidationKeys';

export function useThresholds(): UseQueryResult<CertificationThresholdsDto, Error> {
  return useQuery({
    queryKey: mechanicValidationKeys.thresholds.all,
    queryFn: () => api.admin.getThresholds(),
    staleTime: 60_000, // 60s — same as dashboard
  });
}
