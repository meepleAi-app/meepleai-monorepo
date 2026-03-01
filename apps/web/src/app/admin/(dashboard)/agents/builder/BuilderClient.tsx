'use client';

/**
 * BuilderClient — client boundary for /admin/agents/builder.
 * Issue #5110 — Integrate visual pipeline builder into /admin/agents/builder
 *
 * Renders AgentsNavConfig (MiniNav) + StrategyBuilder canvas.
 * Kept separate so page.tsx can remain a Server Component for metadata.
 */

import { AgentsNavConfig } from '../NavConfig';
import { StrategyBuilder } from '@/components/rag-dashboard/builder/StrategyBuilder';

export function BuilderClient() {
  return (
    <>
      <AgentsNavConfig />
      <StrategyBuilder
        userTier="Admin"
        showValidation
        showConfig
        className="min-h-[calc(100vh-12rem)]"
      />
    </>
  );
}
