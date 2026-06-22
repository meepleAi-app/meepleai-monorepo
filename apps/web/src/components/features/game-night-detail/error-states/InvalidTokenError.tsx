/**
 * InvalidTokenError — terminal surface for malformed / unknown tokens on the
 * public /join/event/[code] route (issue #1169). Mirrors the FSM
 * `token-invalid` state surfaced by `useGameNightInvitation` on 404.
 *
 * Pure presentational, i18n-agnostic (caller resolves all labels). The body
 * copy intentionally avoids leaking *which* token was attempted to keep the
 * page robust against typosquatting / token-enumeration probes.
 */

import Link from 'next/link';

export interface InvalidTokenErrorLabels {
  readonly heading: string;
  readonly body: string;
  readonly homeCta: string;
}

export interface InvalidTokenErrorProps {
  readonly labels: InvalidTokenErrorLabels;
}

export function InvalidTokenError({ labels }: InvalidTokenErrorProps): React.JSX.Element {
  return (
    <section
      data-slot="public-join-event-invalid-token"
      role="alert"
      aria-live="polite"
      className="flex flex-col items-center gap-4 px-5 py-8 text-center"
    >
      <div
        aria-hidden="true"
        className="flex h-16 w-16 items-center justify-center rounded-full border border-border bg-muted text-2xl"
      >
        🔍
      </div>
      <div className="flex flex-col gap-1.5">
        <h1 className="font-display text-lg font-extrabold text-foreground">{labels.heading}</h1>
        <p className="text-sm text-muted-foreground">{labels.body}</p>
      </div>
      <Link
        href="/"
        className="inline-flex items-center justify-center rounded-md border border-border bg-muted px-3.5 py-2.5 font-display text-sm font-bold text-foreground no-underline hover:bg-muted/80"
      >
        {labels.homeCta}
      </Link>
    </section>
  );
}
