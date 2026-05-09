/**
 * SessionShareCard — Wave D.3 v2 component (Issue #756).
 *
 * Preview-only social share card with light/dark theme toggle and 4 share
 * channel buttons (Twitter, Instagram, WhatsApp, Copy link). The "Download
 * PNG" button is rendered DISABLED with a "Coming soon" tooltip per
 * contract §10 (out-of-scope D.3 — backend image generation deferred).
 *
 * Mockup mapping:
 *   - admin-mockups/design_files/sp4-session-summary.jsx (ShareCard)
 *
 * Contract reference: docs/frontend/contracts/sessions-id-summary-hooks.md §5.10 + §10 + §11.
 *
 * MeepleCard divergence (Gate C): full preview surface with theme-driven
 * background, watermark, mini-podium composition, and external share CTAs.
 * MeepleCard cannot represent this composite preview/dialog. DIVERGE.
 *
 * Theme isolation (Gate, contract §11): the `theme` prop only drives this
 * component's preview surface — it does NOT affect the page theme. Page
 * stays in default light theme regardless of preview toggle.
 *
 * A11y:
 *   - Theme toggle: `<div role="radiogroup">` + `<button role="radio" aria-checked>`.
 *   - Share buttons are standard `<button type="button">`.
 *   - Disabled PNG button: `disabled` + `aria-disabled` + `title="Coming soon"`.
 *
 * Pure component: orchestrator owns share callbacks (clipboard / window.open /
 * Web Share API delegation).
 */

'use client';

import type { ReactElement } from 'react';

import clsx from 'clsx';

import type { RankedParticipant } from '@/lib/sessions-summary/tie-groups';

export type ShareCardTheme = 'light' | 'dark';

export type ShareChannel = 'twitter' | 'instagram' | 'whatsapp' | 'copy';

export interface SessionShareCardLabels {
  readonly title: string;
  readonly previewLight: string;
  readonly previewDark: string;
  readonly themeToggleAriaLabel?: string;
  readonly shareTwitter: string;
  readonly shareInstagram: string;
  readonly shareWhatsApp: string;
  readonly copyLink: string;
  readonly downloadPng: string;
  readonly downloadPngDisabled: string;
  /** Resolved headline e.g., "Marco vince Wingspan" — orchestrator builds. */
  readonly previewHeadline: string;
  /** Resolved meta line e.g., "23 apr 2026 · 1h 24min · 4 giocatori". */
  readonly previewMeta: string;
}

export interface SessionShareCardProps {
  /** Top-3 ranked participants (orchestrator pre-trims). Visual mini podium. */
  readonly podium: readonly RankedParticipant[];
  readonly theme: ShareCardTheme;
  readonly onThemeChange: (theme: ShareCardTheme) => void;
  readonly onShare: (channel: ShareChannel) => void;
  readonly labels: SessionShareCardLabels;
  readonly className?: string;
}

interface PreviewPodiumCellProps {
  readonly participant: RankedParticipant;
  readonly place: 1 | 2 | 3;
  readonly theme: ShareCardTheme;
}

function hashHue(seed: string): number {
  let h = 0;
  for (let i = 0; i < seed.length; i += 1) {
    h = (h * 31 + seed.charCodeAt(i)) | 0;
  }
  return Math.abs(h) % 360;
}

function PreviewPodiumCell({ participant, place, theme }: PreviewPodiumCellProps): ReactElement {
  const isWinner = place === 1;
  const hue = hashHue(participant.userId ?? participant.id ?? participant.displayName);
  const size = isWinner ? 56 : 44;
  const fontSize = isWinner ? 24 : 18;
  return (
    <div
      className="flex flex-col items-center gap-1"
      data-slot="share-preview-podium-cell"
      data-place={place}
    >
      {isWinner && (
        <span className="text-xl" aria-hidden="true">
          🏆
        </span>
      )}
      <div
        className="flex items-center justify-center rounded-full font-extrabold text-white"
        style={{
          width: size,
          height: size,
          background: `linear-gradient(135deg, hsl(${hue}, 70%, 65%), hsl(${hue}, 60%, 42%))`,
          border: isWinner ? `2px solid var(--color-entity-toolkit)` : 'none',
          fontSize,
        }}
        aria-hidden="true"
      >
        {participant.displayName.charAt(0).toUpperCase()}
      </div>
      <div
        className={clsx(
          'tabular-nums leading-none',
          isWinner ? 'text-2xl font-extrabold' : 'text-base font-bold'
        )}
        style={{
          color: isWinner
            ? 'var(--color-entity-toolkit)'
            : theme === 'dark'
              ? 'rgba(255,255,255,0.92)'
              : '#0f0c1e',
        }}
      >
        {participant.totalScore}
      </div>
    </div>
  );
}

export function SessionShareCard({
  podium,
  theme,
  onThemeChange,
  onShare,
  labels,
  className,
}: SessionShareCardProps): ReactElement {
  // Reorder podium for the preview: [2nd, 1st, 3rd] for visual flank-elevation.
  const preview = (() => {
    const sorted = [...podium].slice(0, 3);
    const first = sorted.find(p => p.rank === 1);
    const second = sorted.find(p => p.rank === 2);
    const third = sorted.find(p => p.rank >= 3);
    const out: ReadonlyArray<{ p: RankedParticipant; place: 1 | 2 | 3 }> = [
      ...(second ? [{ p: second, place: 2 as const }] : []),
      ...(first ? [{ p: first, place: 1 as const }] : []),
      ...(third ? [{ p: third, place: 3 as const }] : []),
    ];
    return out;
  })();

  return (
    <section
      data-slot="session-share-card"
      data-theme={theme}
      className={clsx('flex flex-col gap-3', className)}
    >
      <div className="flex items-baseline justify-between">
        <h3 className="font-display text-base font-extrabold text-foreground">
          <span aria-hidden="true" className="mr-1.5">
            📤
          </span>
          {labels.title}
        </h3>
        <div
          role="radiogroup"
          aria-label={labels.themeToggleAriaLabel ?? labels.title}
          className="inline-flex overflow-hidden rounded-md border border-border bg-card"
        >
          {(['light', 'dark'] as const).map(t => {
            const active = t === theme;
            const lbl = t === 'light' ? labels.previewLight : labels.previewDark;
            return (
              <button
                key={t}
                type="button"
                role="radio"
                aria-checked={active}
                onClick={() => onThemeChange(t)}
                data-slot="share-theme-option"
                data-theme-option={t}
                data-active={active || undefined}
                className={clsx(
                  'px-3 py-1 font-mono text-[10px] font-extrabold uppercase tracking-wide',
                  active
                    ? 'bg-entity-session/14 text-entity-session'
                    : 'bg-transparent text-muted-foreground hover:text-foreground'
                )}
              >
                {lbl}
              </button>
            );
          })}
        </div>
      </div>
      {/* Preview surface — theme-driven inline styles to keep page-theme isolated */}
      <div
        data-slot="share-preview"
        data-preview-theme={theme}
        className="relative mx-auto w-full max-w-xl overflow-hidden rounded-lg border-2 border-entity-session/30 aspect-[4/3]"
        style={{
          background: theme === 'dark' ? '#0f0c1e' : '#fff',
          // TODO #807-followup: two-entity radial gradient (session+toolkit) — keep inline until CSS vars support alpha stops
          backgroundImage:
            theme === 'dark'
              ? 'radial-gradient(circle at 20% 0%, hsla(240,60%,55%,0.3) 0%, transparent 60%), radial-gradient(circle at 80% 100%, hsla(142,70%,31%,0.18) 0%, transparent 50%)'
              : 'radial-gradient(circle at 20% 0%, hsla(240,60%,55%,0.18) 0%, transparent 60%), radial-gradient(circle at 80% 100%, hsla(142,70%,31%,0.1) 0%, transparent 50%)',
        }}
      >
        <span aria-hidden="true" className="absolute right-4 top-3 text-5xl opacity-20">
          🦜
        </span>
        <div className="relative z-10 flex h-full flex-col justify-between p-6">
          <div>
            <div className="inline-flex items-center gap-1 rounded-full bg-entity-toolkit/18 px-2 py-0.5 font-mono text-[9px] font-extrabold uppercase tracking-wider text-[hsl(142,70%,25%)]">
              <span aria-hidden="true">🏆</span>
            </div>
            <h4
              className="mt-2 font-display text-xl font-extrabold tracking-tight"
              style={{ color: theme === 'dark' ? '#fff' : '#0f0c1e' }}
              data-slot="share-preview-headline"
            >
              {labels.previewHeadline}
            </h4>
            <p
              className="mt-1 font-mono text-[11px] font-bold"
              style={{ color: theme === 'dark' ? 'rgba(255,255,255,0.7)' : 'hsl(220, 8%, 46%)' }}
              data-slot="share-preview-meta"
            >
              {labels.previewMeta}
            </p>
          </div>
          {preview.length > 0 && (
            <div
              className="flex items-end justify-around gap-2 pb-2"
              data-slot="share-preview-podium"
            >
              {preview.map(({ p, place }) => (
                <PreviewPodiumCell key={p.id} participant={p} place={place} theme={theme} />
              ))}
            </div>
          )}
          <div
            className="flex items-center justify-between font-mono text-[10px] font-bold"
            style={{ color: theme === 'dark' ? 'rgba(255,255,255,0.5)' : 'hsl(220, 8%, 46%)' }}
          >
            <span className="inline-flex items-center gap-1">
              {/* TODO #807-followup: two-entity brand gradient (game+event) — keep inline */}
              <span
                aria-hidden="true"
                className="inline-flex h-3.5 w-3.5 items-center justify-center rounded-sm bg-gradient-to-br from-[hsl(25,95%,39%)] to-[hsl(350,89%,48%)] text-[8px] font-extrabold text-white"
              >
                M
              </span>
              MeepleAI
            </span>
            <span>meepleai.app</span>
          </div>
        </div>
      </div>
      {/* Share button row */}
      <div data-slot="share-button-row" className="flex flex-wrap justify-center gap-1.5">
        <button
          type="button"
          onClick={() => onShare('twitter')}
          data-slot="share-button"
          data-channel="twitter"
          className="inline-flex items-center gap-1.5 rounded-md border border-entity-session/30 bg-card px-3 py-1.5 font-display text-[11px] font-bold text-foreground hover:bg-muted/40 focus-visible:outline-none focus-visible:ring-2 focus-visible:ring-ring"
        >
          <span aria-hidden="true">𝕏</span>
          {labels.shareTwitter}
        </button>
        <button
          type="button"
          onClick={() => onShare('instagram')}
          data-slot="share-button"
          data-channel="instagram"
          className="inline-flex items-center gap-1.5 rounded-md border border-entity-session/30 bg-card px-3 py-1.5 font-display text-[11px] font-bold text-foreground hover:bg-muted/40 focus-visible:outline-none focus-visible:ring-2 focus-visible:ring-ring"
        >
          <span aria-hidden="true">📸</span>
          {labels.shareInstagram}
        </button>
        <button
          type="button"
          onClick={() => onShare('whatsapp')}
          data-slot="share-button"
          data-channel="whatsapp"
          className="inline-flex items-center gap-1.5 rounded-md border border-entity-session/30 bg-card px-3 py-1.5 font-display text-[11px] font-bold text-foreground hover:bg-muted/40 focus-visible:outline-none focus-visible:ring-2 focus-visible:ring-ring"
        >
          <span aria-hidden="true">💬</span>
          {labels.shareWhatsApp}
        </button>
        <button
          type="button"
          onClick={() => onShare('copy')}
          data-slot="share-button"
          data-channel="copy"
          className="inline-flex items-center gap-1.5 rounded-md border border-entity-session/30 bg-card px-3 py-1.5 font-display text-[11px] font-bold text-foreground hover:bg-muted/40 focus-visible:outline-none focus-visible:ring-2 focus-visible:ring-ring"
        >
          <span aria-hidden="true">🔗</span>
          {labels.copyLink}
        </button>
        <button
          type="button"
          disabled
          aria-disabled="true"
          title={labels.downloadPngDisabled}
          data-slot="share-button"
          data-channel="png"
          data-disabled="true"
          className="inline-flex cursor-not-allowed items-center gap-1.5 rounded-md border border-border bg-muted/40 px-3 py-1.5 font-display text-[11px] font-bold text-muted-foreground opacity-60"
        >
          <span aria-hidden="true">⬇</span>
          {labels.downloadPng}
        </button>
      </div>
    </section>
  );
}
