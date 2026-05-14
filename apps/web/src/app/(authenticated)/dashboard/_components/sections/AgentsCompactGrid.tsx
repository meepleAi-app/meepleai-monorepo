'use client';

import Link from 'next/link';

import type { Agent } from '@/types/domain';

import { DashboardSection } from './DashboardSection';

export interface AgentsCompactGridLabels {
  readonly title: string;
  readonly viewAllLabel: string;
  readonly viewAllHref: string;
  readonly statusActive: string;
  readonly statusIdle: string;
  readonly emptyTitle: string;
  readonly emptyCta: string;
  readonly emptyCtaHref: string;
}

export interface AgentsCompactGridProps {
  readonly agents: ReadonlyArray<Agent>;
  readonly labels: AgentsCompactGridLabels;
  readonly onViewAllClick?: (sectionId: string, viewAllHref: string) => void;
  readonly onEmptyCtaClick?: (sectionId: string, ctaHref: string) => void;
}

export function AgentsCompactGrid({
  agents,
  labels,
  onViewAllClick,
  onEmptyCtaClick,
}: AgentsCompactGridProps) {
  const top = agents.slice(0, 4);

  return (
    <DashboardSection
      sectionId="agents"
      icon="🤖"
      title={labels.title}
      count={agents.length}
      viewAllLabel={labels.viewAllLabel}
      viewAllHref={labels.viewAllHref}
      onViewAllClick={onViewAllClick}
    >
      {top.length === 0 ? (
        <div className="flex flex-col items-center gap-2 rounded-xl border border-dashed border-border bg-muted/30 px-3 py-6 text-center">
          <span aria-hidden="true" className="text-3xl">
            🤖
          </span>
          <p className="text-sm text-muted-foreground">{labels.emptyTitle}</p>
          <Link
            href={labels.emptyCtaHref}
            onClick={() => onEmptyCtaClick?.('agents', labels.emptyCtaHref)}
            className="mt-1 inline-flex items-center rounded-lg bg-foreground px-3 py-1.5 font-bold font-[Quicksand] text-xs text-background"
          >
            {labels.emptyCta}
          </Link>
        </div>
      ) : (
        <div className="grid grid-cols-2 gap-2">
          {top.map(a => (
            <Link
              key={a.id}
              href={`/agents/${a.id}`}
              data-slot="dashboard-agent-card"
              className="flex items-center gap-2 rounded-lg border border-border bg-card p-2 hover:border-border-strong"
            >
              <div
                aria-hidden="true"
                className="flex h-8 w-8 shrink-0 items-center justify-center rounded bg-[hsl(var(--c-agent)/0.12)] text-base text-[hsl(var(--c-agent))]"
              >
                🤖
              </div>
              <div className="min-w-0 flex-1">
                <div className="line-clamp-1 font-bold font-[Quicksand] text-xs text-foreground">
                  {a.name}
                </div>
                <div className="line-clamp-1 font-mono text-[9px] text-muted-foreground">
                  {a.isActive ? labels.statusActive : labels.statusIdle}
                </div>
              </div>
            </Link>
          ))}
        </div>
      )}
    </DashboardSection>
  );
}
