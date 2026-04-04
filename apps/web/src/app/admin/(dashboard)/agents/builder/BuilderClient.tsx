'use client';

/**
 * BuilderClient — client boundary for /admin/agents/builder.
 * Issue #5110 — Integrate visual pipeline builder into /admin/agents/builder
 *
 * Renders StrategyBuilder canvas.
 * Kept separate so page.tsx can remain a Server Component for metadata.
 */

import { StrategyBuilder } from '@/components/rag-dashboard/builder/StrategyBuilder';

export function BuilderClient() {
  return (
    <StrategyBuilder
      userTier="Admin"
      showValidation
      showConfig
      className="min-h-[calc(100vh-12rem)]"
    />
  );
}
