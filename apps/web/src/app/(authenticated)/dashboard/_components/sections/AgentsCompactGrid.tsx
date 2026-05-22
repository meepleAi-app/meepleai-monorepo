'use client';

import Link from 'next/link';

import type { Agent } from '@/types/domain';

import { DashboardSection } from './DashboardSection';
import { EmptySection } from './EmptySection';

export interface AgentsCompactGridLabels {
  readonly title: string;
  readonly viewAllLabel: string;
  readonly viewAllHref: string;
  readonly statusActive: string;
  readonly statusIdle: string;
  readonly callsTemplate: string;
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

/** Deterministic gradient from agent id (matches mockup `grad(h,s)`). */
function agentGradient(seed: string): string {
  let h = 0;
  for (let i = 0; i < seed.length; i++) h = (h * 31 + seed.charCodeAt(i)) % 360;
  const h2 = (h + 340) % 360;
  const h3 = (h + 30) % 360;
  return `linear-gradient(135deg, hsl(${h}, 70%, 55%), hsl(${h2}, 50%, 30%) 60%, hsl(${h3}, 60%, 40%))`;
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
      entity="agent"
      icon="🤖"
      title={labels.title}
      count={agents.length}
      viewAllLabel={labels.viewAllLabel}
      viewAllHref={labels.viewAllHref}
      onViewAllClick={onViewAllClick}
    >
      {top.length === 0 ? (
        <EmptySection
          entity="agent"
          icon="🤖"
          message={labels.emptyTitle}
          cta={labels.emptyCta}
          ctaHref={labels.emptyCtaHref}
          onCtaClick={() => onEmptyCtaClick?.('agents', labels.emptyCtaHref)}
        />
      ) : (
        <div className="grid grid-cols-2 gap-2">
          {top.map(a => {
            const isActive = a.isActive;
            const statusLabel = isActive ? labels.statusActive : labels.statusIdle;
            const callsLabel = labels.callsTemplate.replace(
              '{count}',
              String(a.invocationCount ?? 0)
            );
            const subtitle = a.strategyName ? `${a.strategyName} · ${callsLabel}` : callsLabel;

            return (
              <Link
                key={a.id}
                href={`/agents/${a.id}`}
                data-slot="dashboard-agent-card"
                className="flex items-center gap-2 rounded-[10px] border border-border bg-background px-2.5 py-2 transition-colors hover:border-border-strong"
              >
                <div
                  aria-hidden="true"
                  className="flex h-8 w-8 shrink-0 items-center justify-center rounded-md text-base"
                  style={{
                    background: agentGradient(a.id),
                    filter: 'saturate(1.1)',
                  }}
                >
                  <span style={{ filter: 'drop-shadow(0 1px 2px rgba(0,0,0,0.3))' }}>🤖</span>
                </div>
                <div className="min-w-0 flex-1">
                  <div className="line-clamp-1 font-quicksand text-[11px] font-extrabold text-foreground">
                    {a.name}
                  </div>
                  <div className="line-clamp-1 font-mono text-[9px] font-semibold text-muted-foreground">
                    {subtitle}
                  </div>
                </div>
                <span
                  aria-label={statusLabel}
                  className="h-1.5 w-1.5 shrink-0 rounded-full"
                  style={{
                    background: isActive ? 'hsl(var(--c-toolkit))' : 'var(--text-muted)',
                  }}
                />
              </Link>
            );
          })}
        </div>
      )}
    </DashboardSection>
  );
}
