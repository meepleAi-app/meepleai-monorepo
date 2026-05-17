/**
 * OverviewTabPanel — `?tab=overview` content for `/toolkits/[id]`.
 *
 * Enabled tab per spec Path C. Renders description + meta dl (license,
 * version, size, published) + ToolkitIncludesGrid (agent + kb + tools
 * count summary). Author info handled in hero, not duplicated here.
 */

'use client';

import type { JSX } from 'react';

import { ToolkitIncludesGrid } from '@/components/features/toolkit-detail';
import type { ToolkitDetail } from '@/lib/api/schemas/toolkit-marketplace.schemas';

import { tabPanelId } from './ToolkitTabs';

export interface OverviewTabPanelLabels {
  readonly descriptionHeading: string;
  readonly metaHeading: string;
  readonly meta: {
    readonly license: string;
    readonly version: string;
    readonly size: string;
    readonly published: string;
    readonly unknown: string;
  };
  readonly includesHeading: string;
  readonly includes: {
    readonly agent: string;
    readonly kbDocs: string;
    readonly tools: string;
  };
}

export interface OverviewTabPanelProps {
  readonly toolkit: ToolkitDetail;
  readonly labels: OverviewTabPanelLabels;
  readonly hidden?: boolean;
}

function formatBytes(bytes: number | null | undefined, unknown: string): string {
  if (bytes == null) return unknown;
  if (bytes < 1024) return `${bytes} B`;
  if (bytes < 1024 * 1024) return `${(bytes / 1024).toFixed(1)} KB`;
  return `${(bytes / (1024 * 1024)).toFixed(1)} MB`;
}

function formatDate(iso: string | null | undefined, unknown: string): string {
  if (!iso) return unknown;
  try {
    return new Intl.DateTimeFormat(undefined, { dateStyle: 'medium' }).format(new Date(iso));
  } catch {
    return unknown;
  }
}

export function OverviewTabPanel({ toolkit, labels, hidden }: OverviewTabPanelProps): JSX.Element {
  const unknown = labels.meta.unknown;

  return (
    <div
      role="tabpanel"
      id={tabPanelId('overview')}
      aria-labelledby="tab-toolkit-detail-overview"
      data-slot="toolkit-detail-tabpanel-overview"
      hidden={hidden}
      className="flex flex-col gap-6"
    >
      <section>
        <h2 className="mb-2 font-bold font-[Quicksand] text-lg text-foreground">
          {labels.descriptionHeading}
        </h2>
        <p className="text-sm leading-relaxed text-muted-foreground whitespace-pre-line">
          {toolkit.description || unknown}
        </p>
      </section>

      <section>
        <h2 className="mb-2 font-bold font-[Quicksand] text-lg text-foreground">
          {labels.metaHeading}
        </h2>
        <dl className="grid grid-cols-2 gap-3 text-xs sm:grid-cols-4">
          <div>
            <dt className="font-mono uppercase tracking-wider text-muted-foreground">
              {labels.meta.license}
            </dt>
            <dd className="mt-1 font-semibold text-foreground">{toolkit.license ?? unknown}</dd>
          </div>
          <div>
            <dt className="font-mono uppercase tracking-wider text-muted-foreground">
              {labels.meta.version}
            </dt>
            <dd className="mt-1 font-semibold text-foreground">{toolkit.currentVersion}</dd>
          </div>
          <div>
            <dt className="font-mono uppercase tracking-wider text-muted-foreground">
              {labels.meta.size}
            </dt>
            <dd className="mt-1 font-semibold text-foreground">
              {formatBytes(toolkit.sizeBytes ?? null, unknown)}
            </dd>
          </div>
          <div>
            <dt className="font-mono uppercase tracking-wider text-muted-foreground">
              {labels.meta.published}
            </dt>
            <dd className="mt-1 font-semibold text-foreground">
              {formatDate(toolkit.publishedAt, unknown)}
            </dd>
          </div>
        </dl>
      </section>

      <section>
        <h2 className="mb-2 font-bold font-[Quicksand] text-lg text-foreground">
          {labels.includesHeading}
        </h2>
        <ToolkitIncludesGrid
          agentName={toolkit.agent.name}
          kbDocsCount={toolkit.kbDocsCount}
          toolsCount={toolkit.toolsCount}
          labels={labels.includes}
        />
      </section>
    </div>
  );
}
