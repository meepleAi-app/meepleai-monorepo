/**
 * AgentHero - v2 Wave C.2 (Issue #581)
 *
 * Mapped from `admin-mockups/design_files/sp4-agent-detail.jsx` (AgentHero).
 * Spec: docs/superpowers/specs/2026-04-26-v2-design-migration.md (Phase 1+2)
 * Tracking: docs/frontend/v2-migration-matrix.md (Issue #581)
 *
 * Renamed from AgentCharacterSheet stub — matches mockup nomenclature.
 *
 * Pure presentational component (no hooks, no useTranslation, no useQuery):
 *   - All i18n strings injected via `labels` (orchestrator resolves upfront).
 *   - Variant `'active'` | `'draft'` | `'archived'` controls CTA bar + banners.
 *   - Optional CTAs absent → button NOT rendered (no disabled placeholder).
 *   - Radial gradient using entity-agent violet palette.
 *
 * A11y contract (Phase 0.5 sez. 4.2 + 4.4):
 *   - Setup banner: `role="status"` (informational)
 *   - Archived banner: `role="alert"` (read-only signaling)
 *   - CTA contrast pre-emption: 700-shade Tailwind for white text (Wave C.1 lesson)
 *     - bg-emerald-700 (active Play)
 *     - bg-amber-700 (draft Setup)
 *     - bg-violet-700 (archived Unarchive — entity agent purple)
 *
 * AC: T A M V
 */

'use client';

import type { ReactElement } from 'react';

import clsx from 'clsx';

export type AgentHeroVariant = 'active' | 'draft' | 'archived';

export interface AgentHeroLabels {
  readonly back: string;
  readonly backAriaLabel: string;
  readonly activeBadge: string;
  readonly draftBadge: string;
  readonly archivedBadge: string;
  readonly metaType: string;
  readonly metaModel: string;
  readonly metaCreated: string;
  readonly metaLastUsed: string;
  readonly metaLastUsedNever: string;
  readonly metaInvocations: string;
  readonly metaGameNone: string;
  readonly ctaPlay: string;
  readonly ctaSetup: string;
  readonly ctaUnarchive: string;
  readonly ctaShare: string;
  readonly ctaShareAriaLabel: string;
  readonly setupBannerTitle: string;
  readonly setupBannerSubtitle: string;
  readonly archivedBannerTitle: string;
  readonly archivedBannerSubtitle: string;
}

export interface AgentHeroMeta {
  readonly type?: string;
  readonly model?: string;
  readonly createdAt?: string;
  readonly lastUsed?: string;
  readonly invocations?: number;
}

export interface AgentHeroProps {
  readonly variant: AgentHeroVariant;
  readonly name: string;
  readonly avatar: string;
  readonly persona: string | null;
  readonly meta: AgentHeroMeta;
  /** 'Avvia chat' CTA — only rendered when provided (active variant) */
  readonly ctaPlay?: () => void;
  /** 'Continua setup' CTA — only rendered when provided (draft variant) */
  readonly ctaSetup?: () => void;
  /** 'Riattiva' CTA — only rendered when provided (archived variant) */
  readonly ctaUnarchive?: () => void;
  /** Share CTA — only rendered when provided */
  readonly ctaShare?: () => void;
  readonly labels: AgentHeroLabels;
  readonly className?: string;
}

export function AgentHero(props: AgentHeroProps): ReactElement {
  const {
    variant,
    name,
    avatar,
    persona,
    meta,
    ctaPlay,
    ctaSetup,
    ctaUnarchive,
    ctaShare,
    labels,
    className,
  } = props;

  // Compose meta line from non-null parts.
  const metaParts: string[] = [];
  if (meta.model) metaParts.push(labels.metaModel.replace('{model}', meta.model));
  if (meta.createdAt) metaParts.push(labels.metaCreated.replace('{date}', meta.createdAt));
  if (meta.lastUsed) metaParts.push(labels.metaLastUsed.replace('{date}', meta.lastUsed));
  if (meta.invocations !== undefined) {
    metaParts.push(labels.metaInvocations.replace('{count}', String(meta.invocations)));
  }

  return (
    <section
      data-slot="agent-detail-hero"
      data-variant={variant}
      className={clsx('relative bg-background', className)}
    >
      {/* Draft banner */}
      {variant === 'draft' ? (
        <div
          role="status"
          data-slot="agent-detail-hero-setup-banner"
          className="flex flex-wrap items-center gap-2.5 border-b border-amber-200 bg-amber-50 px-4 py-2.5 dark:border-amber-900/40 dark:bg-amber-950/20 sm:px-8"
        >
          <span aria-hidden="true" className="text-base">
            ⚙
          </span>
          <div className="flex flex-1 flex-wrap items-baseline gap-x-2 gap-y-0.5">
            <span className="font-display text-[12.5px] font-bold text-amber-800 dark:text-amber-200">
              {labels.setupBannerTitle}
            </span>
            <span className="font-display text-[11.5px] text-amber-700 dark:text-amber-300">
              {labels.setupBannerSubtitle}
            </span>
          </div>
          {ctaSetup ? (
            <button
              type="button"
              onClick={ctaSetup}
              className="inline-flex shrink-0 items-center gap-1.5 rounded-md bg-amber-700 px-3 py-1.5 font-display text-[11.5px] font-extrabold text-white shadow-sm hover:bg-amber-800 focus-visible:outline-none focus-visible:ring-2 focus-visible:ring-amber-700 focus-visible:ring-offset-2"
            >
              {labels.ctaSetup}
            </button>
          ) : null}
        </div>
      ) : null}

      {/* Archived banner */}
      {variant === 'archived' ? (
        <div
          role="alert"
          data-slot="agent-detail-hero-archived-banner"
          className="flex flex-wrap items-center gap-2.5 border-b border-border bg-muted px-4 py-2.5 sm:px-8"
        >
          <span aria-hidden="true" className="text-base text-muted-foreground">
            ⊘
          </span>
          <div className="flex flex-1 flex-wrap items-baseline gap-x-2 gap-y-0.5">
            <span className="font-display text-[12.5px] font-bold text-foreground">
              {labels.archivedBannerTitle}
            </span>
            <span className="font-display text-[11.5px] text-muted-foreground">
              {labels.archivedBannerSubtitle}
            </span>
          </div>
          {ctaUnarchive ? (
            <button
              type="button"
              onClick={ctaUnarchive}
              className="inline-flex shrink-0 items-center gap-1.5 rounded-md bg-violet-700 px-3 py-1.5 font-display text-[11.5px] font-extrabold text-white shadow-sm hover:bg-violet-800 focus-visible:outline-none focus-visible:ring-2 focus-visible:ring-violet-700 focus-visible:ring-offset-2"
            >
              ↻ {labels.ctaUnarchive}
            </button>
          ) : null}
        </div>
      ) : null}

      {/* Hero body */}
      <div
        data-slot="agent-detail-hero-body"
        className="relative border-b border-border/50 px-4 py-6 sm:px-8 sm:py-8"
        style={{
          background:
            'radial-gradient(circle at 20% 30%, hsla(270, 80%, 60%, 0.18) 0%, transparent 60%), radial-gradient(circle at 80% 70%, hsla(270, 80%, 60%, 0.08) 0%, transparent 50%)',
        }}
      >
        <div className="relative z-10 flex flex-col gap-5 sm:flex-row sm:items-center sm:gap-7">
          {/* Avatar */}
          <div
            aria-hidden="true"
            className="flex h-20 w-20 shrink-0 items-center justify-center self-start rounded-xl border-2 text-5xl sm:h-24 sm:w-24 sm:text-[56px]"
            style={{
              background: 'hsla(270, 80%, 60%, 0.16)',
              borderColor: 'hsla(270, 80%, 60%, 0.3)',
              boxShadow: '0 8px 24px hsla(270, 80%, 60%, 0.25)',
              color: 'hsl(270, 80%, 55%)',
            }}
          >
            {avatar}
          </div>

          {/* Name + persona + meta */}
          <div className="min-w-0 flex-1">
            {/* Badge row */}
            <div className="mb-2 flex flex-wrap items-center gap-1.5">
              <span
                className="inline-flex items-center gap-1.5 rounded-full px-2.5 py-1 font-mono text-[9px] font-extrabold uppercase tracking-[0.08em]"
                style={{
                  background: 'hsla(270, 80%, 60%, 0.14)',
                  color: 'hsl(270, 80%, 40%)',
                }}
              >
                <span aria-hidden="true">🤖</span>
                Agent
              </span>
              {variant === 'active' ? (
                <span className="rounded-full bg-emerald-100 px-2.5 py-1 font-mono text-[9px] font-extrabold uppercase tracking-[0.08em] text-emerald-700 dark:bg-emerald-950/40 dark:text-emerald-300">
                  ● {labels.activeBadge}
                </span>
              ) : null}
              {variant === 'draft' ? (
                <span className="rounded-full bg-amber-100 px-2.5 py-1 font-mono text-[9px] font-extrabold uppercase tracking-[0.08em] text-amber-700 dark:bg-amber-950/40 dark:text-amber-300">
                  ⚙ {labels.draftBadge}
                </span>
              ) : null}
              {variant === 'archived' ? (
                <span className="rounded-full bg-slate-100 px-2.5 py-1 font-mono text-[9px] font-extrabold uppercase tracking-[0.08em] text-slate-500 dark:bg-slate-800 dark:text-slate-400">
                  ⊘ {labels.archivedBadge}
                </span>
              ) : null}
            </div>

            {/* Agent name */}
            <h1 className="mb-1 font-display text-[26px] font-extrabold leading-[1.05] tracking-[-0.02em] text-foreground sm:text-[34px]">
              {name}
            </h1>

            {/* Persona subtitle */}
            {persona ? (
              <div
                className="mb-3 font-display text-[13px] font-bold sm:text-[14px]"
                style={{ color: 'hsl(270, 80%, 50%)' }}
              >
                {persona}
              </div>
            ) : null}

            {/* Meta line */}
            {metaParts.length > 0 ? (
              <div className="flex flex-wrap items-center gap-x-2.5 gap-y-1 font-mono text-[11px] font-semibold text-muted-foreground sm:text-[12px]">
                {metaParts.map((part, i) => (
                  <span key={i}>
                    {i > 0 ? (
                      <span aria-hidden="true" className="mr-2.5">
                        ·
                      </span>
                    ) : null}
                    {part}
                  </span>
                ))}
              </div>
            ) : null}
          </div>

          {/* Action bar */}
          <div className="flex shrink-0 flex-wrap items-center gap-2 self-start sm:self-auto">
            {/* Play CTA — active variant only */}
            {variant === 'active' && ctaPlay ? (
              <button
                type="button"
                onClick={ctaPlay}
                className="inline-flex items-center gap-1.5 rounded-lg bg-emerald-700 px-4 py-2.5 font-display text-[13px] font-extrabold text-white shadow-md hover:bg-emerald-800 focus-visible:outline-none focus-visible:ring-2 focus-visible:ring-emerald-700 focus-visible:ring-offset-2"
                // eslint-disable-next-line meepleai/no-inline-hsl-v2 -- TODO #807-followup: decorative green glow (hue 140, sat 60%) — near toolkit but distinct shade; JS style boxShadow
                style={{ boxShadow: '0 4px 14px hsla(140, 60%, 35%, 0.4)' }}
              >
                <span aria-hidden="true">💬</span>
                {labels.ctaPlay}
              </button>
            ) : null}

            {/* Share CTA */}
            {ctaShare ? (
              <button
                type="button"
                onClick={ctaShare}
                aria-label={labels.ctaShareAriaLabel}
                className="inline-flex items-center gap-1.5 rounded-lg border border-border px-3.5 py-2.5 font-display text-[13px] font-bold text-foreground hover:bg-muted focus-visible:outline-none focus-visible:ring-2 focus-visible:ring-ring focus-visible:ring-offset-2"
              >
                <span aria-hidden="true">↗</span>
                <span className="hidden sm:inline">{labels.ctaShare}</span>
              </button>
            ) : null}
          </div>
        </div>
      </div>
    </section>
  );
}
