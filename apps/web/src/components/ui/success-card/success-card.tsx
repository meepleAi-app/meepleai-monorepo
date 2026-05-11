import type { JSX, ReactNode } from 'react';

import { Btn } from '../btn';

export interface SuccessCardProps {
  readonly emoji: string;
  /** Optional heading. Omit when the parent (e.g. AuthCard) already renders the page title to avoid duplicate headings. */
  readonly title?: string;
  readonly body: string | ReactNode;
  readonly cta: string;
  readonly onCta: () => void;
  readonly className?: string;
}

export function SuccessCard({
  emoji,
  title,
  body,
  cta,
  onCta,
  className,
}: SuccessCardProps): JSX.Element {
  return (
    <div className={`flex flex-col items-center text-center gap-3 p-6 ${className ?? ''}`}>
      <div
        aria-hidden="true"
        className="w-16 h-16 flex items-center justify-center rounded-full bg-[hsl(var(--c-toolkit)/0.1)] text-3xl"
      >
        <span>{emoji}</span>
      </div>
      {title ? (
        <h2 className="font-heading font-bold text-xl text-foreground m-0">{title}</h2>
      ) : null}
      {typeof body === 'string' ? (
        <p className="font-body text-sm text-muted-foreground m-0">{body}</p>
      ) : (
        body
      )}
      <Btn variant="primary" onClick={onCta} className="mt-2">
        {cta}
      </Btn>
    </div>
  );
}
