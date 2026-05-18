/**
 * ExpiredOrCancelledError — terminal surface for 410 Gone on /join/event/[code]
 * (issue #1169). Used for tokens that are Expired, Revoked, or whose game
 * night was Cancelled by the host.
 *
 * The `kind` discriminator only changes copy — both states are terminal and
 * irrecoverable from the public surface; the CTA invites the recipient to
 * request a fresh invite from the host. We deliberately don't expose a "retry"
 * affordance because the underlying entity is gone.
 */

import Link from 'next/link';

export type ExpiredOrCancelledKind = 'expired' | 'cancelled';

export interface ExpiredOrCancelledErrorLabels {
  readonly expired: {
    readonly heading: string;
    readonly body: string;
  };
  readonly cancelled: {
    readonly heading: string;
    readonly body: string;
  };
  readonly requestNewInviteCta: string;
  readonly homeCta: string;
}

export interface ExpiredOrCancelledErrorProps {
  readonly kind: ExpiredOrCancelledKind;
  readonly labels: ExpiredOrCancelledErrorLabels;
}

export function ExpiredOrCancelledError({
  kind,
  labels,
}: ExpiredOrCancelledErrorProps): React.JSX.Element {
  const copy = kind === 'expired' ? labels.expired : labels.cancelled;
  const icon = kind === 'expired' ? '⌛' : '🚫';

  return (
    <section
      data-slot="public-join-event-gone"
      data-kind={kind}
      role="alert"
      aria-live="polite"
      className="flex flex-col items-center gap-4 px-5 py-8 text-center"
    >
      <div
        aria-hidden="true"
        className="flex h-16 w-16 items-center justify-center rounded-full border border-border bg-muted text-2xl"
      >
        {icon}
      </div>
      <div className="flex flex-col gap-1.5">
        <h1 className="font-display text-lg font-extrabold text-foreground">{copy.heading}</h1>
        <p className="text-sm text-muted-foreground">{copy.body}</p>
      </div>
      <div className="flex flex-col gap-2 sm:flex-row sm:gap-3">
        <Link
          href="/contact"
          className="inline-flex items-center justify-center rounded-md border-0 bg-primary px-3.5 py-2.5 font-display text-sm font-bold text-primary-foreground no-underline hover:bg-primary/90"
        >
          {labels.requestNewInviteCta}
        </Link>
        <Link
          href="/"
          className="inline-flex items-center justify-center rounded-md border border-border bg-muted px-3.5 py-2.5 font-display text-sm font-bold text-foreground no-underline hover:bg-muted/80"
        >
          {labels.homeCta}
        </Link>
      </div>
    </section>
  );
}
