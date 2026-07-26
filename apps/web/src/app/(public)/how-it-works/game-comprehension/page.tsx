'use client';

/**
 * Public explainer — ME-M2.2 (Issue #531), parent ADR-051.
 *
 * `/how-it-works/game-comprehension` — a public marketing/explainer landing page
 * describing the AI game-comprehension trust chain (PDF → AI rewords → human
 * reviews per-claim → published card cites the exact page), with a live,
 * keyboard-accessible citation demo.
 *
 * The live demo uses HARDCODED sample Catan data so the page renders even if no
 * Catan card is published. It reuses the real {@link MechanicCitationBadge} from
 * #530 for the inline `[p.N]` control, but shows a lightweight illustrative panel
 * (the quote + page + "human-reviewed" badge) on activation rather than mounting
 * the full pdfjs-backed {@link MechanicCitationPanel}, which needs a real PDF.
 */

import { useCallback, useState, type JSX } from 'react';

import Link from 'next/link';

import { MechanicCitationBadge } from '@/components/features/mechanic-card/MechanicCitationBadge';
import { StructuredData, learningResourceSchema } from '@/components/legal/StructuredData';
import { Btn } from '@/components/ui/btn';
import { useTranslation } from '@/hooks/useTranslation';
import type { PublishedMechanicCitationDto } from '@/lib/api/schemas/mechanic-card.schemas';

const CANONICAL_URL = 'https://meepleai.com/how-it-works/game-comprehension';

/** The four links in the provenance chain, in order. */
const CHAIN_STEPS = ['pdf', 'read', 'review', 'card'] as const;
type ChainStepKey = (typeof CHAIN_STEPS)[number];

/** Line-icon per station. Presentational only — decorative (`aria-hidden`). */
const CHAIN_ICON: Record<ChainStepKey, JSX.Element> = {
  pdf: <PdfIcon />,
  read: <ReadIcon />,
  review: <ReviewIcon />,
  card: <CardIcon />,
};

/**
 * Hardcoded sample citation for the live demo. Not fetched — the landing must
 * render even when no Catan card is published. The `pdfId` is a stable dummy
 * UUID (the badge only needs `pdfPage` to be interactive).
 */
const DEMO_PDF_PAGE = 7;

export default function GameComprehensionPage(): JSX.Element {
  const { t } = useTranslation();
  const [panelOpen, setPanelOpen] = useState(false);

  const demoCitation: PublishedMechanicCitationDto = {
    pdfId: '00000000-0000-4000-8000-000000000531',
    pdfPage: DEMO_PDF_PAGE,
    quote: t('pages.gameComprehension.demoQuote'),
  };

  const openPanel = useCallback(() => setPanelOpen(true), []);
  const closePanel = useCallback(() => setPanelOpen(false), []);

  return (
    <main className="bg-background">
      <StructuredData
        data={learningResourceSchema({
          name: t('pages.gameComprehension.metaTitle'),
          description: t('pages.gameComprehension.metaDescription'),
          url: CANONICAL_URL,
        })}
      />

      {/* ── Hero ─────────────────────────────────────────────────────────── */}
      <section
        className="relative overflow-hidden px-4 py-20 md:py-28"
        style={{
          background:
            'linear-gradient(160deg, hsl(var(--c-game) / 0.10), hsl(var(--c-document) / 0.06) 55%, transparent)',
        }}
      >
        <div className="mx-auto max-w-3xl text-center">
          <p className="mb-4 inline-flex items-center gap-2 rounded-full border border-entity-game/30 bg-entity-game/10 px-3 py-1 text-xs font-semibold uppercase tracking-[0.18em] text-entity-game-text">
            {t('pages.gameComprehension.eyebrow')}
          </p>
          <h1
            className="m-0 text-balance text-4xl font-bold tracking-tight text-foreground md:text-6xl"
            style={{ fontFamily: 'var(--f-display)' }}
          >
            {t('pages.gameComprehension.title')}
          </h1>
          <p className="mx-auto mt-6 max-w-2xl text-pretty text-lg leading-relaxed text-muted-foreground">
            {t('pages.gameComprehension.subtitle')}
          </p>
        </div>
      </section>

      {/* ── Trust chain ──────────────────────────────────────────────────── */}
      <section className="mx-auto max-w-6xl px-4 py-16 md:py-20">
        <div className="mx-auto max-w-2xl text-center">
          <h2 className="text-2xl font-bold text-foreground md:text-3xl">
            {t('pages.gameComprehension.chainHeading')}
          </h2>
          <p className="mt-3 text-muted-foreground">
            {t('pages.gameComprehension.chainSubheading')}
          </p>
        </div>

        {/* The chain: a connecting "thread" (the provenance that never breaks)
            runs behind the numbered stations. Vertical on mobile, horizontal on
            large screens. The thread is decorative; the ordered list carries the
            real sequence semantics. */}
        <ol className="relative mt-14 grid grid-cols-1 gap-y-10 sm:grid-cols-2 lg:grid-cols-4 lg:gap-x-6">
          {/* Horizontal thread (lg+): sits behind the numbered nodes. */}
          <span
            aria-hidden="true"
            className="pointer-events-none absolute left-0 right-0 top-6 hidden h-px bg-gradient-to-r from-entity-game/10 via-entity-game/40 to-entity-game/10 lg:block"
          />
          {CHAIN_STEPS.map((key, idx) => (
            <li
              key={key}
              className="relative flex break-inside-avoid flex-col items-center text-center lg:px-3"
            >
              {/* Numbered node — the anchor point on the thread. */}
              <span className="relative z-10 flex h-12 w-12 items-center justify-center rounded-full border border-entity-game/30 bg-card text-lg font-bold text-entity-game-text shadow-sm">
                <span className="sr-only">{`${idx + 1}. `}</span>
                {idx + 1}
              </span>
              <span
                className="mt-5 flex h-11 w-11 items-center justify-center text-entity-game"
                aria-hidden="true"
              >
                {CHAIN_ICON[key]}
              </span>
              <span className="mt-3 text-[11px] font-semibold uppercase tracking-[0.14em] text-muted-foreground">
                {t(`pages.gameComprehension.chain.${key}.label`)}
              </span>
              <h3 className="mt-1 text-lg font-semibold text-foreground">
                {t(`pages.gameComprehension.chain.${key}.title`)}
              </h3>
              <p className="mt-2 text-sm leading-relaxed text-muted-foreground">
                {t(`pages.gameComprehension.chain.${key}.description`)}
              </p>
            </li>
          ))}
        </ol>
      </section>

      {/* ── Live citation demo ───────────────────────────────────────────── */}
      <section className="border-y border-border bg-muted/40 px-4 py-16 md:py-20">
        <div className="mx-auto max-w-3xl">
          <div className="mx-auto max-w-2xl text-center">
            <h2 className="text-2xl font-bold text-foreground md:text-3xl">
              {t('pages.gameComprehension.demoHeading')}
            </h2>
            <p className="mt-3 text-muted-foreground">
              {t('pages.gameComprehension.demoSubheading')}
            </p>
          </div>

          {/* A representative snippet of a published card. */}
          <figure className="mx-auto mt-10 max-w-xl rounded-2xl border border-border bg-card p-6 shadow-sm">
            <figcaption className="flex flex-wrap items-center gap-2 border-b border-border pb-3">
              <span className="text-sm font-semibold text-entity-game-text">
                {t('pages.gameComprehension.demoGameName')}
              </span>
              <span className="text-xs uppercase tracking-wide text-muted-foreground">
                {t('pages.gameComprehension.demoSectionLabel')}
              </span>
            </figcaption>

            <p className="mt-4 text-base leading-relaxed text-foreground">
              {t('pages.gameComprehension.demoClaim')}{' '}
              <MechanicCitationBadge citation={demoCitation} onOpen={openPanel} />
            </p>

            {/* Lightweight illustrative panel (a mock of MechanicCitationPanel —
                no real PDF). Toggled by the badge. Keyboard-dismissable. */}
            {panelOpen && (
              <div
                className="mt-5 rounded-xl border border-entity-game/30 bg-entity-game/5 p-4"
                data-testid="game-comprehension-demo-panel"
              >
                <div className="flex flex-wrap items-center justify-between gap-2">
                  <span className="inline-flex items-center rounded-md border border-entity-game/40 bg-entity-game/10 px-2 py-0.5 text-xs font-semibold text-entity-game-text">
                    {t('pages.gameComprehension.demoReviewedBadge')}
                  </span>
                  <span className="text-xs uppercase tracking-wide text-muted-foreground">
                    {t('pages.gameComprehension.demoPageLabel', { page: DEMO_PDF_PAGE })}
                  </span>
                </div>
                <blockquote className="mt-3 border-l-2 border-entity-game/40 pl-3 text-sm italic leading-relaxed text-foreground">
                  &ldquo;{t('pages.gameComprehension.demoQuote')}&rdquo;
                </blockquote>
                <p className="mt-3 text-xs text-muted-foreground">
                  {t('pages.gameComprehension.demoDocumentName')}
                </p>
                <div className="mt-3 flex justify-end">
                  <Btn variant="ghost" size="sm" onClick={closePanel}>
                    {t('pages.gameComprehension.demoDismiss')}
                  </Btn>
                </div>
              </div>
            )}
          </figure>

          <p className="mx-auto mt-8 max-w-xl text-center text-sm leading-relaxed text-muted-foreground">
            {t('pages.gameComprehension.trustNote')}
          </p>
        </div>
      </section>

      {/* ── CTA ──────────────────────────────────────────────────────────── */}
      <section className="mx-auto max-w-3xl px-4 py-16 text-center md:py-24">
        <h2 className="text-2xl font-bold text-foreground md:text-3xl">
          {t('pages.gameComprehension.ctaHeading')}
        </h2>
        <p className="mx-auto mt-3 max-w-xl text-muted-foreground">
          {t('pages.gameComprehension.ctaSubheading')}
        </p>
        <div className="mt-8 flex flex-col items-center justify-center gap-3 sm:flex-row">
          <Btn variant="primary" entity="game" size="lg" asChild>
            <Link href="/register">{t('pages.gameComprehension.ctaPrimary')}</Link>
          </Btn>
          <Btn variant="ghost" size="lg" asChild>
            <Link href="/how-it-works">{t('pages.gameComprehension.ctaSecondary')}</Link>
          </Btn>
        </div>
      </section>
    </main>
  );
}

/* ── Decorative line icons (aria-hidden, inherit currentColor) ───────────── */

function PdfIcon(): JSX.Element {
  return (
    <svg
      viewBox="0 0 24 24"
      fill="none"
      stroke="currentColor"
      strokeWidth="1.5"
      className="h-8 w-8"
    >
      <path
        strokeLinecap="round"
        strokeLinejoin="round"
        d="M14 3v4a1 1 0 0 0 1 1h4M5 3h9l5 5v11a2 2 0 0 1-2 2H5a2 2 0 0 1-2-2V5a2 2 0 0 1 2-2Z"
      />
      <path strokeLinecap="round" d="M8 13h8M8 17h5" />
    </svg>
  );
}

function ReadIcon(): JSX.Element {
  return (
    <svg
      viewBox="0 0 24 24"
      fill="none"
      stroke="currentColor"
      strokeWidth="1.5"
      className="h-8 w-8"
    >
      <path
        strokeLinecap="round"
        strokeLinejoin="round"
        d="M12 6c-2-1.5-4.5-1.5-7-1v12c2.5-.5 5-.5 7 1m0-13c2-1.5 4.5-1.5 7-1v12c-2.5-.5-5-.5-7 1m0-13v13"
      />
      <path strokeLinecap="round" strokeLinejoin="round" d="m16.5 15 1.5 1.5 3-3" />
    </svg>
  );
}

function ReviewIcon(): JSX.Element {
  return (
    <svg
      viewBox="0 0 24 24"
      fill="none"
      stroke="currentColor"
      strokeWidth="1.5"
      className="h-8 w-8"
    >
      <circle cx="12" cy="8" r="3.5" strokeLinecap="round" strokeLinejoin="round" />
      <path strokeLinecap="round" strokeLinejoin="round" d="M5 20a7 7 0 0 1 14 0" />
      <path strokeLinecap="round" strokeLinejoin="round" d="m9.5 8 1.6 1.6L14 6.5" />
    </svg>
  );
}

function CardIcon(): JSX.Element {
  return (
    <svg
      viewBox="0 0 24 24"
      fill="none"
      stroke="currentColor"
      strokeWidth="1.5"
      className="h-8 w-8"
    >
      <rect
        x="3"
        y="5"
        width="18"
        height="14"
        rx="2"
        strokeLinecap="round"
        strokeLinejoin="round"
      />
      <path strokeLinecap="round" d="M7 10h6" />
      <path strokeLinecap="round" strokeLinejoin="round" d="M15 14h2.5" opacity="0.7" />
      <path strokeLinecap="round" d="M7 14h4" />
    </svg>
  );
}
