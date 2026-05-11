/**
 * AgentsResultsGrid — Wave B.2 v2 component (Issue #634).
 *
 * Mapped from `admin-mockups/design_files/sp4-agents-index.jsx` (AgentCardGrid).
 * Spec: docs/superpowers/specs/2026-04-30-v2-migration-wave-b-2-agents.md §3.2
 *
 * MeepleCard zero-fork (CLAUDE.md mandate): wraps the existing `MeepleCard`
 * from `@/components/ui/data-display/meeple-card` 1:1 — no fork, no entity
 * extension, no spec extension. Each card is wrapped in a Next.js `<Link>` to
 * `/agents/{id}` for navigation.
 *
 * Field mapping (AgentDto → MeepleCard):
 *   - title         = agent.name
 *   - subtitle      = agent.gameName (when present)
 *   - status        = deriveStatus(agent) mapped to CardStatus
 *   - tags          = [agent.type, agent.strategyName]
 *   - imageUrl      = undefined (MeepleCard renders entity-typed placeholder)
 *
 * Layout: CSS-grid auto-fit with min track 320px and max viewport 1280px,
 * centered via `mx-auto`. This keeps card width responsive without media
 * queries and matches the SP4 mockup grid spec.
 *
 * Component is dumb: no fetch, no filter, no sort. Orchestrator
 * (AgentsLibraryView, Commit 3) owns the derivation pipeline.
 */

import type { ReactElement } from 'react';

import clsx from 'clsx';
import Link from 'next/link';

import { MeepleCard } from '@/components/ui/data-display/meeple-card';
import type { CardStatus } from '@/components/ui/data-display/meeple-card/types';
import { deriveStatus, type AgentDerivedStatus } from '@/lib/agents/derive-status';
import type { AgentDto } from '@/lib/api/schemas/agents.schemas';

export interface AgentsResultsGridProps {
  readonly agents: readonly AgentDto[];
  readonly compact?: boolean;
  readonly className?: string;
}

const STATUS_TO_CARD_STATUS: Readonly<Record<AgentDerivedStatus, CardStatus>> = {
  attivo: 'active',
  'in-setup': 'setup',
  archiviato: 'archived',
};

export function AgentsResultsGrid({
  agents,
  compact = false,
  className,
}: AgentsResultsGridProps): ReactElement {
  return (
    <div
      data-slot="agents-results-grid"
      className={clsx(
        compact
          ? 'grid grid-cols-1 gap-3 px-4 sm:grid-cols-2'
          : 'grid grid-cols-[repeat(auto-fit,minmax(320px,1fr))] gap-4 px-4 sm:px-8 max-w-[1280px] mx-auto',
        className
      )}
    >
      {agents.map(agent => {
        const derived = deriveStatus(agent);
        const cardStatus = STATUS_TO_CARD_STATUS[derived];
        const tags = [agent.type, agent.strategyName];

        return (
          <Link
            key={agent.id}
            href={`/agents/${agent.id}`}
            className="block focus-visible:outline-none focus-visible:ring-2 focus-visible:ring-ring rounded-2xl"
            data-slot="agents-results-grid-link"
            data-agent-id={agent.id}
            data-agent-status={derived}
          >
            <MeepleCard
              entity="agent"
              variant="grid"
              id={agent.id}
              title={agent.name}
              subtitle={agent.gameName ?? undefined}
              status={cardStatus}
              tags={tags}
            />
          </Link>
        );
      })}
    </div>
  );
}
