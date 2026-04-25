/**
 * Mechanic Extractor — AI Comprehension Validation (ADR-051 Sprint 1 / Task 35)
 *
 * Shared TanStack Query key factory for the admin mechanic-extractor validation
 * surface. Pattern mirrors `useAgentDefinitions.ts` (`{ all, lists, list(...),
 * detail(id) }`), scoped per resource.
 */

export const mechanicValidationKeys = {
  golden: {
    all: ['golden'] as const,
    byGame: (sharedGameId: string) => ['golden', sharedGameId] as const,
  },
  dashboard: {
    all: ['validation-dashboard'] as const,
    summary: () => ['validation-dashboard'] as const,
  },
  mechanicAnalysis: {
    all: ['mechanic-analysis'] as const,
    detail: (id: string) => ['mechanic-analysis', id] as const,
  },
  trend: {
    all: ['mechanic-validation-trend'] as const,
    byGame: (sharedGameId: string, take?: number) =>
      take === undefined
        ? (['mechanic-validation-trend', sharedGameId] as const)
        : (['mechanic-validation-trend', sharedGameId, take] as const),
  },
} as const;
