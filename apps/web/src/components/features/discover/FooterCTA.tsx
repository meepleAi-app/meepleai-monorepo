'use client';

import Link from 'next/link';

export interface FooterCTAProps {
  /** Localised title. */
  readonly title: string;
  /** Localised description / body text. */
  readonly description?: string;
  /** Primary action button (label + href). */
  readonly primaryCta: { readonly label: string; readonly href: string };
  /** Optional secondary action. */
  readonly secondaryCta?: { readonly label: string; readonly href: string };
}

/**
 * FooterCTA — gradient block at the bottom of /discover.
 *
 * Per mockup: toolkit→player gradient, centered title + description + 1–2 CTA
 * buttons. Uses semantic foreground / background tokens for text contrast on
 * top of the entity gradient.
 */
export function FooterCTA({ title, description, primaryCta, secondaryCta }: FooterCTAProps) {
  return (
    <section
      data-slot="discover-footer-cta"
      className="mx-4 my-8 overflow-hidden rounded-2xl px-6 py-8 text-center sm:mx-8 sm:px-10 sm:py-10"
      style={{
        background:
          'linear-gradient(135deg, hsl(var(--c-toolkit) / 0.12) 0%, hsl(var(--c-player) / 0.12) 100%)',
        border: '1px solid var(--border-light, rgba(180, 130, 80, 0.1))',
      }}
    >
      <h2 className="font-bold font-[Quicksand] text-xl sm:text-2xl tracking-tight text-foreground">
        {title}
      </h2>
      {description && (
        <p
          className="mx-auto mt-2 max-w-md text-sm text-muted-foreground"
          style={{ lineHeight: 1.55 }}
        >
          {description}
        </p>
      )}
      <div className="mt-5 flex flex-wrap items-center justify-center gap-3">
        <Link
          href={primaryCta.href}
          className="inline-flex items-center rounded-2xl bg-foreground px-5 py-2 font-bold font-[Quicksand] text-sm text-background transition-opacity hover:opacity-90"
        >
          {primaryCta.label}
        </Link>
        {secondaryCta && (
          <Link
            href={secondaryCta.href}
            className="inline-flex items-center rounded-2xl border border-border bg-card px-5 py-2 font-bold font-[Quicksand] text-sm text-foreground transition-colors hover:border-border-strong"
          >
            {secondaryCta.label}
          </Link>
        )}
      </div>
    </section>
  );
}
