/**
 * InviteHostCard — host avatar + welcome message panel.
 *
 * Wave A.5b (Issue #611). Mirrors mockup `sp3-accept-invite.jsx` lines 124-161
 * (HostAvatar + name + "Online" dot + italicized welcome message in
 * « guillemets »). Host color is derived from `hostColor` HSL hue (0–360 from
 * backend `hostUserId` deterministic hash) and applied as bg/border at 7%/25%
 * opacity for the panel envelope.
 *
 * Avatar rules:
 *  - Round (50%), inline-flex centered, `aria-hidden="true"` (initials are
 *    decorative — the next sibling shows the host name in plain text)
 *  - 44px default, 38px when `compact`
 *  - White text on saturated HSL bg (60% sat, 55% lightness)
 *
 * The "Online" dot is decorative; the spec deliberately does NOT expose any
 * presence/online state on the public RSVP DTO. Future PR: optional prop.
 */

import type { JSX } from 'react';

import clsx from 'clsx';

export interface InviteHostCardProps {
  /** Display name shown to recipient (denormalized backend column). */
  readonly hostDisplayName: string;
  /** Initials for avatar fallback (1–2 char, computed by caller). */
  readonly hostInitials: string;
  /** Hue 0–360 for the avatar/panel tint, deterministic from hostUserId. */
  readonly hostHue: number;
  /** Optional welcome message rendered in guillemets, italic. */
  readonly welcomeMessage?: string | null;
  /** Localized "HOST" eyebrow label. */
  readonly hostLabel: string;
  /** Localized fallback when welcomeMessage is empty. */
  readonly noMessageLabel: string;
  /** Localized aria-label for "online" status pip. */
  readonly onlineLabel?: string;
  /** Localized aria-label for the avatar (e.g. "Avatar di Marco"). */
  readonly avatarAriaLabel: string;
  readonly compact?: boolean;
  readonly className?: string;
}

export function InviteHostCard({
  hostDisplayName,
  hostInitials,
  hostHue,
  welcomeMessage,
  hostLabel,
  noMessageLabel,
  onlineLabel = 'Online',
  avatarAriaLabel,
  compact = false,
  className,
}: InviteHostCardProps): JSX.Element {
  const avatarSize = compact ? 38 : 44;
  const fontSize = Math.round(avatarSize * 0.36);

  return (
    <div
      data-slot="invite-host-card"
      className={clsx('flex items-start gap-3 rounded-lg border', className)}
      style={{
        padding: compact ? '11px 12px' : '14px 14px',
        background: `hsl(${hostHue} 60% 55% / 0.07)`,
        borderColor: `hsl(${hostHue} 60% 55% / 0.25)`,
      }}
    >
      <span
        role="img"
        aria-label={avatarAriaLabel}
        className="inline-flex flex-shrink-0 select-none items-center justify-center rounded-full font-display font-extrabold text-white"
        style={{
          width: avatarSize,
          height: avatarSize,
          background: `hsl(${hostHue} 60% 55%)`,
          fontSize,
        }}
      >
        <span aria-hidden="true">{hostInitials}</span>
      </span>

      <div className="min-w-0 flex-1">
        <div className="mb-1 flex items-center gap-1.5">
          <span className="font-mono text-[9px] font-bold uppercase tracking-[0.08em] text-[hsl(var(--text-muted))]">
            {hostLabel}
          </span>
          <span className="font-display text-[13px] font-bold text-foreground">
            {hostDisplayName}
          </span>
          <span
            role="status"
            aria-label={onlineLabel}
            className="ml-0.5 h-[5px] w-[5px] flex-shrink-0 rounded-full bg-[hsl(var(--c-toolkit))]"
          />
        </div>
        <div
          className={clsx(
            'text-[12px] leading-relaxed',
            welcomeMessage ? 'italic text-[hsl(var(--text-sec))]' : 'text-[hsl(var(--text-muted))]'
          )}
        >
          {welcomeMessage ? `«${welcomeMessage}»` : noMessageLabel}
        </div>
      </div>
    </div>
  );
}
