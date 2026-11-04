/**
 * /invites/unsubscribe/[key] — client body (#4846).
 *
 * A small state machine: `idle` (asks) → `submitting` → `done` | `invalid` | `rate-limited` | `error`. The last
 * two keep the button, so the person can try again; `invalid` (a key nobody holds) and `done` do not.
 *
 * The call is made on the tap and never on load: see the shell in `page.tsx`.
 *
 * When the button goes away because the request ended, focus moves to the heading of what replaced it: a keyboard or
 * screen reader user whose focused element is removed would otherwise be dropped on the page body, with no idea
 * that anything happened.
 */

'use client';

import { useCallback, useEffect, useRef, useState, type JSX } from 'react';

import { ErrorBanner } from '@/components/ui/invites';
import { Button } from '@/components/ui/primitives/button';
import { useTranslation } from '@/hooks/useTranslation';
import {
  InvitationNotFoundError,
  unsubscribeFromInvitationEmails,
} from '@/lib/api/game-night-invitations';

type Phase =
  | { readonly kind: 'idle' }
  | { readonly kind: 'submitting' }
  | { readonly kind: 'done' }
  | { readonly kind: 'invalid' }
  | { readonly kind: 'rate-limited'; readonly retryAfter: number | null }
  | { readonly kind: 'error' };

export interface UnsubscribePageClientProps {
  readonly unsubscribeKey: string;
}

const KEY = 'pages.invites.unsubscribe';

export function UnsubscribePageClient({ unsubscribeKey }: UnsubscribePageClientProps): JSX.Element {
  const { t } = useTranslation();
  const [phase, setPhase] = useState<Phase>({ kind: 'idle' });
  const outcomeRef = useRef<HTMLHeadingElement>(null);

  const submit = useCallback(async (): Promise<void> => {
    setPhase({ kind: 'submitting' });
    try {
      const result = await unsubscribeFromInvitationEmails(unsubscribeKey);
      setPhase(
        result.kind === 'unsubscribed'
          ? { kind: 'done' }
          : { kind: 'rate-limited', retryAfter: result.retryAfter }
      );
    } catch (error) {
      setPhase(error instanceof InvitationNotFoundError ? { kind: 'invalid' } : { kind: 'error' });
    }
  }, [unsubscribeKey]);

  // The two outcomes that replace the button.
  const ended = phase.kind === 'done' || phase.kind === 'invalid';
  useEffect(() => {
    if (ended) outcomeRef.current?.focus();
  }, [ended]);

  const submitting = phase.kind === 'submitting';

  return (
    <main className="mx-auto flex w-full max-w-md flex-col gap-6 px-4 py-12 sm:px-0">
      {phase.kind === 'done' ? (
        <section aria-live="polite" className="flex flex-col gap-3" data-testid="unsubscribe-done">
          <h1 ref={outcomeRef} tabIndex={-1} className="text-2xl font-semibold outline-none">
            {t(`${KEY}.done.heading`)}
          </h1>
          <p className="text-muted-foreground">{t(`${KEY}.done.body`)}</p>
        </section>
      ) : phase.kind === 'invalid' ? (
        <ErrorBanner tone="error">
          <strong ref={outcomeRef} tabIndex={-1} className="outline-none">
            {t(`${KEY}.invalid.heading`)}
          </strong>
          <span className="block">{t(`${KEY}.invalid.body`)}</span>
        </ErrorBanner>
      ) : (
        <>
          <section className="flex flex-col gap-3">
            <h1 className="text-2xl font-semibold">{t(`${KEY}.heading`)}</h1>
            <p className="text-muted-foreground">{t(`${KEY}.intro`)}</p>
          </section>

          {phase.kind === 'rate-limited' ? (
            <ErrorBanner tone="warning">
              {t(`${KEY}.rateLimited`, { seconds: phase.retryAfter ?? 60 })}
            </ErrorBanner>
          ) : null}
          {phase.kind === 'error' ? (
            <ErrorBanner tone="error">{t(`${KEY}.error`)}</ErrorBanner>
          ) : null}

          <Button type="button" onClick={submit} disabled={submitting} className="w-full sm:w-auto">
            {submitting
              ? t(`${KEY}.submitting`)
              : phase.kind === 'error'
                ? t(`${KEY}.retryCta`)
                : t(`${KEY}.confirmCta`)}
          </Button>
        </>
      )}
    </main>
  );
}
