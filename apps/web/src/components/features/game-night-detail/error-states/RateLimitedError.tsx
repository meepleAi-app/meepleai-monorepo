/**
 * RateLimitedError — surface for 429 Too Many Requests on /join/event/[code]
 * (issue #1169). The backend caps the public endpoints at 60 req/min/IP (GET)
 * and 10 req/min/IP (POST). When the user hits the cap we render this banner
 * with a live countdown sourced from the `Retry-After` response header.
 *
 * Countdown:
 *   - Stops at 0 and reveals the "Retry now" CTA.
 *   - Disabled when `retryAfterSeconds` is null (header missing) — we just
 *     show the static retry CTA.
 *   - Pure presentational: the parent passes the initial seconds value and
 *     handles the retry click (typically `useGameNightInvitation.refetch()`
 *     or `useRespondToInvitation.reset()`).
 */

'use client';

import { useEffect, useState } from 'react';

import { Button } from '@/components/ui/primitives/button';

export interface RateLimitedErrorLabels {
  readonly heading: string;
  readonly body: string;
  readonly countdown: (secondsLeft: number) => string;
  readonly retryCta: string;
}

export interface RateLimitedErrorProps {
  /** Seconds parsed from the `Retry-After` header; null when missing. */
  readonly retryAfterSeconds: number | null;
  readonly labels: RateLimitedErrorLabels;
  readonly onRetry: () => void;
}

export function RateLimitedError({
  retryAfterSeconds,
  labels,
  onRetry,
}: RateLimitedErrorProps): React.JSX.Element {
  const initialSeconds =
    retryAfterSeconds !== null && retryAfterSeconds > 0 ? retryAfterSeconds : 0;
  const [secondsLeft, setSecondsLeft] = useState<number>(initialSeconds);

  useEffect(() => {
    // Re-seed when the parent passes a fresh retryAfter (e.g. user retried
    // and got rate-limited again with a different window).
    setSecondsLeft(retryAfterSeconds !== null && retryAfterSeconds > 0 ? retryAfterSeconds : 0);
  }, [retryAfterSeconds]);

  useEffect(() => {
    if (secondsLeft <= 0) return;
    const id = setInterval(() => {
      setSecondsLeft(prev => (prev > 0 ? prev - 1 : 0));
    }, 1000);
    return () => clearInterval(id);
  }, [secondsLeft]);

  const canRetry = secondsLeft <= 0;

  return (
    <section
      data-slot="public-join-event-rate-limited"
      role="alert"
      aria-live="polite"
      className="flex flex-col items-center gap-4 px-5 py-8 text-center"
    >
      <div
        aria-hidden="true"
        className="flex h-16 w-16 items-center justify-center rounded-full border border-border bg-muted text-2xl"
      >
        ⏱️
      </div>
      <div className="flex flex-col gap-1.5">
        <h1 className="font-display text-lg font-extrabold text-foreground">{labels.heading}</h1>
        <p className="text-sm text-muted-foreground">{labels.body}</p>
      </div>
      {canRetry ? (
        <Button
          type="button"
          variant="outline"
          data-slot="public-join-event-rate-limited-retry"
          onClick={onRetry}
        >
          {labels.retryCta}
        </Button>
      ) : (
        <p
          data-slot="public-join-event-rate-limited-countdown"
          className="font-mono text-sm font-bold text-muted-foreground"
          aria-live="polite"
        >
          {labels.countdown(secondsLeft)}
        </p>
      )}
    </section>
  );
}
