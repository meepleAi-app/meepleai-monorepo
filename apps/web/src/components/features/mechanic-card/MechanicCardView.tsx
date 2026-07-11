'use client';

/**
 * MechanicCardView — public, login-gated mechanic card (ME-M1.6, Issue #528).
 *
 * Renders the published `PublishedMechanicCardDto` returned by
 * `GET /api/v1/games/{sharedGameId}/card`:
 *   - Header: game name + "AI-generated, human-reviewed" badge linking to the
 *     game-comprehension explainer (#531, forward reference — landing not built yet).
 *   - The present sections (Summary | Mechanics | Victory | Resources | Phases | FAQ),
 *     already grouped + ordered by the backend, each with its reworded claims.
 *   - Each claim: reworded text + inline `[p.N]` citation badges that open the
 *     shared {@link PdfQuoteHighlighter} at the cited page/quote.
 *   - Footer: the shared ADR-051 attribution ({@link MechanicAnalysisFooterAttribution}).
 *
 * Designed as a rulebook reference card a player can print at the table:
 * mobile-first single column, `print:` utilities strip chrome, semantic tokens
 * keep it legible on the default light (cream) theme AND on paper.
 *
 * On a 404 (no published card, or suppressed/taken down) the hook returns
 * `data === null`; this component calls Next's `notFound()`.
 *
 * NOTE: guardrail validation badges (T1–T4) are admin-only telemetry and are
 * deliberately NOT rendered here — they are not part of the public DTO.
 */

import { useState, type ReactElement } from 'react';

import Link from 'next/link';
import { notFound } from 'next/navigation';

import { MechanicAnalysisFooterAttribution } from '@/components/admin/mechanic-extractor/MechanicAnalysisFooterAttribution';
import { Badge } from '@/components/ui/data-display/badge';
import { useMechanicCard } from '@/hooks/queries/useMechanicCard';
import { MECHANIC_SECTION_LABELS, MechanicSection } from '@/lib/api/schemas/mechanic-card.schemas';
import type {
  PublishedMechanicCardDto,
  PublishedMechanicCitationDto,
  PublishedMechanicSectionDto,
} from '@/lib/api/schemas/mechanic-card.schemas';

import { MechanicCitationBadge } from './MechanicCitationBadge';
import { MechanicCitationPanel } from './MechanicCitationPanel';

const HOW_IT_WORKS_HREF = '/how-it-works/game-comprehension';
const TAKEDOWN_HREF = '/legal/takedown';

/**
 * Resolve a human label for a section. The parsed DTO carries the numeric enum
 * value, but stay tolerant of the raw PascalCase name too (backend serializer
 * emits `"Faq"` etc.) so the label is never a bare `Section 5`.
 */
function sectionLabel(section: PublishedMechanicSectionDto['section']): string {
  if (typeof section === 'number') {
    return MECHANIC_SECTION_LABELS[section] ?? `Section ${section}`;
  }
  const numeric = MechanicSection[section as keyof typeof MechanicSection];
  if (numeric !== undefined) return MECHANIC_SECTION_LABELS[numeric] ?? String(section);
  return String(section);
}

export interface MechanicCardViewProps {
  /**
   * SharedGame id from the route param, normalized at the page boundary.
   * MUST be `string | null` — never `undefined` or the string `'undefined'`.
   */
  readonly gameId: string | null;
}

// ─── Shells ──────────────────────────────────────────────────────────────────

function CardShell({ children }: { children: React.ReactNode }): ReactElement {
  return (
    <main
      data-slot="mechanic-card"
      className="mx-auto w-full max-w-[900px] px-4 py-8 print:px-0 print:py-0 sm:px-6 sm:py-12"
    >
      {children}
    </main>
  );
}

function LoadingShell(): ReactElement {
  return (
    <CardShell>
      <div
        data-slot="mechanic-card-loading"
        data-testid="mechanic-card-loading"
        aria-busy="true"
        className="flex flex-col gap-6"
      >
        <div className="h-10 w-2/3 animate-pulse rounded-lg bg-muted" />
        <div className="h-6 w-40 animate-pulse rounded-full bg-muted" />
        <div className="mt-4 h-32 w-full animate-pulse rounded-2xl bg-muted" />
        <div className="h-32 w-full animate-pulse rounded-2xl bg-muted" />
      </div>
    </CardShell>
  );
}

function ErrorShell({ onRetry }: { onRetry: () => void }): ReactElement {
  return (
    <CardShell>
      <div
        data-slot="mechanic-card-error"
        className="flex min-h-[40vh] flex-col items-center justify-center gap-4 text-center"
      >
        <span aria-hidden="true" className="text-4xl">
          ⚠️
        </span>
        <h1 className="font-display text-xl font-extrabold text-foreground">
          Couldn&apos;t load this card
        </h1>
        <p className="max-w-sm text-sm text-muted-foreground">
          Something went wrong while loading the card. Please try again.
        </p>
        <button
          type="button"
          onClick={onRetry}
          data-slot="mechanic-card-error-retry"
          data-testid="mechanic-card-error-retry"
          className="rounded-md border border-border bg-transparent px-4 py-2 font-display text-sm font-bold text-foreground transition-colors hover:bg-muted focus-visible:outline-none focus-visible:ring-2 focus-visible:ring-ring"
        >
          Retry
        </button>
      </div>
    </CardShell>
  );
}

// ─── Presentational pieces ───────────────────────────────────────────────────

function ClaimItem({
  claim,
  onOpenCitation,
}: {
  claim: PublishedMechanicCardDto['sections'][number]['claims'][number];
  onOpenCitation: (citation: PublishedMechanicCitationDto) => void;
}): ReactElement {
  return (
    <li
      data-slot="mechanic-card-claim"
      data-testid={`mechanic-card-claim-${claim.id}`}
      className="flex flex-col gap-1.5 border-t border-border/60 py-3 first:border-t-0 first:pt-0"
    >
      <p className="text-[15px] leading-relaxed text-foreground">{claim.claim}</p>
      {claim.citations.length > 0 && (
        <div className="flex flex-wrap items-center gap-1.5">
          <span className="sr-only">Sources:</span>
          {claim.citations.map(citation => (
            <MechanicCitationBadge
              key={`${citation.pdfId}-${citation.pdfPage}`}
              citation={citation}
              onOpen={() => onOpenCitation(citation)}
            />
          ))}
        </div>
      )}
    </li>
  );
}

function SectionBlock({
  section,
  index,
  onOpenCitation,
}: {
  section: PublishedMechanicSectionDto;
  index: number;
  onOpenCitation: (citation: PublishedMechanicCitationDto) => void;
}): ReactElement {
  const label = sectionLabel(section.section);
  return (
    <section
      data-slot="mechanic-card-section"
      data-testid={`mechanic-card-section-${section.section}`}
      className="break-inside-avoid rounded-2xl border border-border bg-card p-5 sm:p-6 print:rounded-none print:border-x-0 print:border-t-0 print:p-0 print:pt-4"
      aria-labelledby={`mechanic-card-section-heading-${section.section}`}
    >
      <div className="mb-3 flex items-baseline gap-3 border-b border-border/60 pb-2">
        {/* Sections ARE an ordered sequence (Summary→…→FAQ), so a rulebook-style
            chapter numeral encodes real order rather than decorating. */}
        <span
          aria-hidden="true"
          className="font-display text-sm font-bold tabular-nums text-entity-game"
        >
          {String(index + 1).padStart(2, '0')}
        </span>
        <h2
          id={`mechanic-card-section-heading-${section.section}`}
          className="font-display text-lg font-extrabold tracking-tight text-foreground"
        >
          {label}
        </h2>
      </div>
      <ul className="flex flex-col">
        {section.claims.map(claim => (
          <ClaimItem key={claim.id} claim={claim} onOpenCitation={onOpenCitation} />
        ))}
      </ul>
    </section>
  );
}

// ─── Main view ───────────────────────────────────────────────────────────────

export function MechanicCardView({ gameId }: MechanicCardViewProps): ReactElement {
  const { data, isLoading, isError, refetch } = useMechanicCard(gameId);
  // AD-2: exactly ONE panel instance. Clicking another badge swaps `activeCitation`.
  const [activeCitation, setActiveCitation] = useState<PublishedMechanicCitationDto | null>(null);
  const [panelOpen, setPanelOpen] = useState(false);

  const openCitation = (citation: PublishedMechanicCitationDto): void => {
    setActiveCitation(citation);
    setPanelOpen(true);
  };

  if (isLoading) {
    return <LoadingShell />;
  }

  if (isError) {
    return <ErrorShell onRetry={() => void refetch()} />;
  }

  // `data === null` → backend 404 (no published card OR suppressed/taken down).
  if (data == null) {
    notFound();
  }

  const card = data;
  const publishedDate = formatPublishedDate(card.publishedAt);

  return (
    <CardShell>
      <article className="flex flex-col gap-6">
        {/* Header */}
        <header data-slot="mechanic-card-header" className="flex flex-col gap-3">
          <div className="flex flex-wrap items-center gap-2">
            <Badge
              variant="outline"
              data-slot="mechanic-card-ai-badge"
              className="border-entity-game/40 bg-entity-game/10 text-entity-game print:border-border print:bg-transparent print:text-foreground"
            >
              <Link
                href={HOW_IT_WORKS_HREF}
                className="focus-visible:outline-none focus-visible:ring-2 focus-visible:ring-ring"
              >
                AI-generated, human-reviewed
              </Link>
            </Badge>
            <span className="text-xs uppercase tracking-wide text-muted-foreground">
              {card.language}
            </span>
          </div>

          <h1
            data-testid="mechanic-card-title"
            className="font-display text-3xl font-extrabold leading-tight tracking-tight text-foreground sm:text-4xl"
          >
            {card.gameName}
          </h1>

          <p className="text-sm text-muted-foreground">
            {card.title}
            {card.publisher ? ` · ${card.publisher}` : ''}
            {' · '}
            <span className="whitespace-nowrap">v{card.version}</span>
            {publishedDate ? ` · ${publishedDate}` : ''}
          </p>
        </header>

        {/* Sections */}
        {card.sections.length === 0 ? (
          <p
            data-slot="mechanic-card-empty"
            className="rounded-2xl border border-border bg-muted p-6 text-center text-sm text-muted-foreground"
          >
            This card doesn&apos;t have any content yet.
          </p>
        ) : (
          <div className="flex flex-col gap-4 print:gap-0">
            {card.sections.map((section, index) => (
              <SectionBlock
                key={String(section.section)}
                section={section}
                index={index}
                onOpenCitation={openCitation}
              />
            ))}
          </div>
        )}

        {/* Footer attribution (shared, ADR-051) + public takedown link (#529).
            The takedown link is card-only — the shared attribution component is
            reused on the admin review page, which must NOT show it. */}
        <footer data-slot="mechanic-card-footer">
          <MechanicAnalysisFooterAttribution />
          <p className="mt-2 text-center text-xs text-muted-foreground print:hidden">
            <Link
              href={TAKEDOWN_HREF}
              data-slot="mechanic-card-takedown-link"
              data-testid="mechanic-card-takedown-link"
              className="underline hover:text-foreground focus-visible:outline-none focus-visible:ring-2 focus-visible:ring-ring"
            >
              Report a copyright concern
            </Link>
          </p>
        </footer>
      </article>

      {/* AD-2: a single citation panel drives every badge; swapping content on
          click, restoring focus + announcing on open. */}
      <MechanicCitationPanel
        citation={activeCitation}
        card={card}
        isOpen={panelOpen}
        onClose={() => setPanelOpen(false)}
      />
    </CardShell>
  );
}

/** Format an ISO date as a short, locale-independent published date. */
function formatPublishedDate(iso: string): string | null {
  const date = new Date(iso);
  if (Number.isNaN(date.getTime())) return null;
  return date.toLocaleDateString('en-US', { year: 'numeric', month: 'short', day: 'numeric' });
}
