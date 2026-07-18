'use client';

import { useTranslation } from '@/hooks/useTranslation';
import type { GameSessionDto } from '@/lib/api/schemas/games.schemas';

import { catanPieceColor } from './catan-palette';
import { buildCatanSummaryStandings } from './catan-summary-standings';

interface CatanSummaryFlavorProps {
  readonly session: GameSessionDto;
  readonly className?: string;
}

/**
 * #3022 — presentational Catan summary: winner hero + final standings from real
 * per-player scores (scoreData joined to scorePlayers by id) + colors.
 */
export function CatanSummaryFlavor({
  session,
  className,
}: CatanSummaryFlavorProps): React.JSX.Element {
  const { t } = useTranslation();
  const rows = buildCatanSummaryStandings(
    session.scoringType,
    session.scoreData,
    session.scorePlayers
  );

  if (rows.length === 0) {
    return (
      <section
        data-slot="catan-summary-flavor"
        className={`rounded-2xl border border-border bg-card p-4 text-center text-[13px] text-muted-foreground ${className ?? ''}`}
      >
        {t('pages.sessionSummary.flavor.catan.empty')}
      </section>
    );
  }

  const standingsTitle = t('pages.sessionSummary.flavor.catan.standingsTitle');
  const vpUnit = t('pages.sessionSummary.flavor.catan.vpUnit');

  // Winner precedence: BE-authoritative winnerName (matched to a row) → scoreData isWinner
  // → none (never auto-crown when nobody won).
  const heroRow =
    (session.winnerName != null
      ? rows.find(r => r.playerName === session.winnerName)
      : undefined) ??
    rows.find(r => r.isWinner) ??
    null;
  const heroName = heroRow?.playerName ?? session.winnerName ?? null;
  const maxScore = rows.reduce((m, r) => Math.max(m, r.score), 0);

  return (
    <section
      data-slot="catan-summary-flavor"
      aria-label={standingsTitle}
      className={`flex flex-col gap-4 rounded-2xl border border-border bg-card p-4 ${className ?? ''}`}
    >
      {heroName != null && (
        <header data-slot="catan-summary-hero" className="flex items-center gap-3">
          <span
            aria-hidden="true"
            className="flex h-11 w-11 shrink-0 items-center justify-center rounded-full border border-border-strong text-lg"
            style={{ backgroundColor: catanPieceColor(heroRow?.color ?? '') }}
          >
            👑
          </span>
          <div className="flex flex-col">
            <span className="text-base font-semibold text-foreground">
              {t('pages.sessionSummary.flavor.catan.winnerTemplate', { name: heroName })}
            </span>
            <span className="text-[13px] text-muted-foreground">
              {heroRow != null ? `${heroRow.score} ${vpUnit} · ` : ''}
              {t('pages.sessionSummary.flavor.catan.durationTemplate', {
                minutes: session.durationMinutes,
              })}
            </span>
          </div>
        </header>
      )}

      <ol data-slot="catan-summary-standings" className="flex flex-col gap-1.5">
        {rows.map((row, i) => (
          <li
            key={`${row.playerName}-${i}`}
            data-slot="catan-summary-row"
            className="flex items-center gap-2 text-[13px]"
          >
            <span className="w-5 tabular-nums text-muted-foreground">{i + 1}°</span>
            <span
              aria-hidden="true"
              className="h-3.5 w-3.5 shrink-0 rounded-full border border-border-strong"
              style={{ backgroundColor: catanPieceColor(row.color ?? '') }}
            />
            <span data-testid="catan-summary-row-name" className="flex-1 truncate text-foreground">
              {row.playerName}
            </span>
            <span className="h-1.5 w-24 overflow-hidden rounded-full bg-muted" aria-hidden="true">
              <span
                className="block h-full rounded-full bg-entity-session"
                style={{ width: `${maxScore > 0 ? (row.score / maxScore) * 100 : 0}%` }}
              />
            </span>
            <span className="w-12 text-right tabular-nums text-foreground">
              {row.score} {vpUnit}
            </span>
          </li>
        ))}
      </ol>
    </section>
  );
}
