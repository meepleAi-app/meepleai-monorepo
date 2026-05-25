/**
 * VersionTimeline — published version history rail for `/toolkits/[id]`.
 *
 * Wave 3 (#1479). Maps the mockup VersionsTab (sp4-toolkit-detail.jsx:743-841).
 * Pure presentational, labels injected, dates pre-formatted by the caller.
 *
 * Source data: ToolkitVersion[] (version, publishedAt, changelog, isCurrent).
 * The backend has no `kind` field — it is optional, defaulting to the toolkit
 * accent dot. `changelog` is a single string (not notes[]) and is split on
 * newlines into bullet lines. Owner actions (edit notes / yank) are deferred:
 * the v1 backend has no yank workflow yet.
 */

'use client';

import type { JSX } from 'react';

import clsx from 'clsx';

export type VersionKind = 'major' | 'minor' | 'patch';

export interface VersionTimelineItem {
  readonly version: string;
  /** Pre-formatted, locale-aware timestamp supplied by the orchestrator. */
  readonly publishedAtLabel: string;
  readonly changelog: string;
  readonly isCurrent: boolean;
  readonly kind?: VersionKind;
}

export interface VersionTimelineLabels {
  readonly currentBadge: string;
  readonly empty: string;
}

export interface VersionTimelineProps {
  readonly versions: readonly VersionTimelineItem[];
  readonly labels: VersionTimelineLabels;
  readonly className?: string;
}

const DOT_CLASS: Record<VersionKind, string> = {
  major: 'bg-entity-event',
  minor: 'bg-entity-toolkit',
  patch: 'bg-muted-foreground',
};

function changelogLines(changelog: string): readonly string[] {
  return changelog
    .split('\n')
    .map(line => line.trim())
    .filter(Boolean);
}

export function VersionTimeline({
  versions,
  labels,
  className,
}: VersionTimelineProps): JSX.Element {
  if (versions.length === 0) {
    return (
      <div
        data-slot="toolkit-detail-version-timeline"
        className={clsx('rounded-lg border border-border bg-card p-6 text-center', className)}
      >
        <p className="font-mono text-xs text-muted-foreground">{labels.empty}</p>
      </div>
    );
  }

  return (
    <div data-slot="toolkit-detail-version-timeline" className={clsx('flex flex-col', className)}>
      {versions.map((v, i) => {
        const isLast = i === versions.length - 1;
        const dotClass = v.kind ? DOT_CLASS[v.kind] : 'bg-entity-toolkit';
        const lines = changelogLines(v.changelog);
        return (
          <div
            key={v.version}
            data-slot="toolkit-detail-version-item"
            className={clsx('flex gap-3.5', !isLast && 'pb-3.5')}
          >
            <div className="relative flex w-3.5 shrink-0 justify-center pt-1.5">
              {!isLast && (
                <div
                  data-slot="toolkit-detail-version-connector"
                  aria-hidden="true"
                  className="absolute bottom-[-14px] left-1/2 top-[18px] w-0.5 -translate-x-1/2 bg-border"
                />
              )}
              <span
                data-slot="toolkit-detail-version-dot"
                aria-hidden="true"
                className={clsx(
                  'relative z-[1] h-3.5 w-3.5 rounded-full',
                  dotClass,
                  v.isCurrent && 'ring-4 ring-entity-toolkit/10'
                )}
              />
            </div>

            <div
              data-slot="toolkit-detail-version-item-card"
              className="flex-1 rounded-lg border border-border bg-card p-3.5"
            >
              <div className="mb-1 flex flex-wrap items-center gap-2">
                <h3 className="m-0 font-mono text-sm font-bold text-foreground">v{v.version}</h3>
                {v.kind && (
                  <span
                    data-slot="toolkit-detail-version-kind"
                    className="rounded-full bg-muted px-2 py-0.5 font-mono text-[9px] font-bold uppercase tracking-wider text-muted-foreground"
                  >
                    {v.kind}
                  </span>
                )}
                {v.isCurrent && (
                  <span
                    data-slot="toolkit-detail-version-current"
                    className="rounded-full bg-entity-toolkit/15 px-2 py-0.5 font-mono text-[9px] font-bold uppercase tracking-wider text-entity-toolkit"
                  >
                    {labels.currentBadge}
                  </span>
                )}
                <span className="ml-auto font-mono text-[11px] text-muted-foreground">
                  {v.publishedAtLabel}
                </span>
              </div>
              {lines.length > 0 && (
                <ul className="m-0 list-disc pl-4 text-[12.5px] leading-relaxed text-muted-foreground">
                  {lines.map((line, j) => (
                    <li key={j}>{line}</li>
                  ))}
                </ul>
              )}
            </div>
          </div>
        );
      })}
    </div>
  );
}
