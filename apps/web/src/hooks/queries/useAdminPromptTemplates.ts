/**
 * useAdminPromptTemplates - TanStack Query hook for the admin Prompts surface.
 *
 * Issue #1442 Phase 1b — read-only wiring; no mutations in this iteration
 * (View / Edit buttons remain stubs until the dedicated editor lands).
 */

import { useQuery, type UseQueryResult } from '@tanstack/react-query';

import { listAdminPromptTemplates, type PromptTemplateDto } from '@/lib/api/admin-prompt-templates';

export const adminPromptTemplatesKeys = {
  all: ['admin-prompt-templates'] as const,
};

export function useAdminPromptTemplates(): UseQueryResult<PromptTemplateDto[], Error> {
  return useQuery({
    queryKey: adminPromptTemplatesKeys.all,
    queryFn: listAdminPromptTemplates,
    staleTime: 60_000,
  });
}
