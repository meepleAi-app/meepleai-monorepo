/**
 * PlayRecordHeroPodium — Task 0.6 (Issue #1488 / Epic #1475 Phase D).
 *
 * Variant-driven hero block rendered at the top of `/play-records/[id]`
 * detail view. Built once per record; switches between 5 visual variants
 * matching the user's perspective (derived via `derivePerspective`).
 *
 * Variants & visual treatment (mockup `sp4-play-records-detail.jsx` HeroPodium):
 *
 *   1. **won**         — celebratory: 🏆 badge + 3-place podium with crown
 *                        on winner; "{name} vince {game}" banner.
 *   2. **tied**        — 🤝 badge + multi-crown on shared 1st-place spots;
 *                        "Pareggio a {score} punti" banner.
 *   3. **cooperative** — 🤝 neutral badge + classifica without crown;
 *                        "Partita cooperativa di {game}" banner.
 *   4. **inprogress**  — pulsing live badge + "{game} in corso · turno N"
 *                        banner; podium with current scores (may be null).
 *   5. **planned**     — 📅 badge + "Avvia partita" CTA; cover-emphasized
 *                        layout with date metadata.
 *
 * **Distinct from `features/session-summary/SessionSummaryHero`**:
 *   That component consumes `RankedParticipant` from `lib/sessions-summary`
 *   and only supports completed games. Play-records covers the full record
 *   lifecycle (`Planned → InProgress → Completed → Archived`) and accepts
 *   the leaner `rankedScores` shape derived directly from `PlayRecordDto`.
 *   Per plan R3: both retained until cross-route convergence is validated.
 *
 * Pure component: every string passed via `labels`. Layout responsive
 * (mobile compact via parent `useMediaQuery`). A11y: `<section role`...
 * + aria-labelledby; live variant carries `role="status"` on its badge.
 *
 * @see plan `docs/superpowers/plans/2026-05-29-play-records-reskin.md` Task 0 Step 6
 * @see mockup `admin-mockups/design_files/sp4-play-records-detail.jsx`
 */
'use client';

import type { ReactElement, ReactNode } from 'react';
import { useId } from 'react';

import clsx from 'clsx';

export type PlayRecordHeroVariant = 'won' | 'tied' | 'cooperative' | 'inprogress' | 'planned';

export interface RankedScore {
  readonly playerId: string;
  readonly name: string;
  /** Player score; may be null for InProgress records before scoring. */
  readonly score: number | null;
  /** Whether this player is in `winnerPlayerIds` of the record. */
  readonly isWinner: boolean;
}

export interface PlayRecordHeroPodiumLabels {
  // badge labels (compact pill above the title)
  readonly variantWon: string;
  readonly variantTied: string;
  readonly variantCooperative: string;
  readonly variantInProgress: string;
  readonly variantPlanned: string;

  // banner builders (must produce already-localised strings)
  readonly bannerWon: (winnerName: string, gameName: string) => string;
  readonly bannerTied: (score: number) => string;
  readonly bannerCooperative: (gameName: string) => string;
  readonly bannerInProgress: (gameName: string, turn?: number) => string;
  readonly bannerPlanned: (gameName: string) => string;

  // metadata helpers
  readonly metaPlayers: (n: number) => string;

  // planned variant CTA
  readonly ctaStart: string;
}

export interface PlayRecordHeroGameSummary {
  readonly id: string | null;
  readonly name: string;
  readonly coverUrl?: string;
  /** Fallback emoji when no `coverUrl` (or for freeform records, EC-2). */
  readonly coverEmoji?: string;
}

export interface PlayRecordHeroMetadata {
  /** Pre-formatted relative date (e.g. from `formatRelativeDate`). */
  readonly date: string;
  /** Pre-formatted duration (e.g. "2h 15min"); null for InProgress/Planned. */
  readonly duration: string | null;
  readonly playerCount: number;
  /** Current turn number for InProgress records (optional). */
  readonly turn?: number;
}

export interface HeroPerspectiveBinding {
  readonly kind: 'won' | 'lost' | 'tie' | 'cooperative' | 'spectator' | 'pending';
  readonly currentUserPlayerId: string | null;
}

export interface PlayRecordHeroPodiumProps {
  readonly variant: PlayRecordHeroVariant;
  readonly game: PlayRecordHeroGameSummary;
  /** Already sorted descending by score (consumers responsible for ranking). */
  readonly rankedScores: ReadonlyArray<RankedScore>;
  readonly metadata: PlayRecordHeroMetadata;
  readonly perspective: HeroPerspectiveBinding;
  readonly labels: PlayRecordHeroPodiumLabels;
  /** Click handler for the "Avvia partita" CTA (planned variant only). */
  readonly onStart?: () => void;
  readonly className?: string;
}

// ── helpers ─────────────────────────────────────────────────────────────────

/** Deterministic HSL hue from player id/name (FE-only color seed). */
function hashHue(seed: string): number {
  let h = 0;
  for (let i = 0; i < seed.length; i += 1) {
    h = (h * 31 + seed.charCodeAt(i)) | 0;
  }
  return Math.abs(h) % 360;
}

function getInitials(name: string): string {
  return name.trim().slice(0, 2).toUpperCase();
}

/** Reorder top-3 for visual podium [2nd, 1st, 3rd]. Solo/duo collapse. */
function buildPodiumLayout(
  scores: ReadonlyArray<RankedScore>
): ReadonlyArray<{ entry: RankedScore; place: number }> {
  const top3 = scores.slice(0, 3);
  if (top3.length === 0) return [];
  if (top3.length === 1) return [{ entry: top3[0], place: 1 }];
  if (top3.length === 2)
    return [
      { entry: top3[1], place: 2 },
      { entry: top3[0], place: 1 },
    ];
  return [
    { entry: top3[1], place: 2 },
    { entry: top3[0], place: 1 },
    { entry: top3[2], place: 3 },
  ];
}

function variantBadgeLabel(
  variant: PlayRecordHeroVariant,
  labels: PlayRecordHeroPodiumLabels
): string {
  switch (variant) {
    case 'won':
      return labels.variantWon;
    case 'tied':
      return labels.variantTied;
    case 'cooperative':
      return labels.variantCooperative;
    case 'inprogress':
      return labels.variantInProgress;
    case 'planned':
      return labels.variantPlanned;
  }
}

function variantBadgeClassName(variant: PlayRecordHeroVariant): string {
  // entity-tinted pill; live variant additionally has pulse + dot.
  switch (variant) {
    case 'won':
    case 'tied':
    case 'cooperative':
      return 'bg-entity-toolkit/16 text-entity-toolkit ring-entity-toolkit/30';
    case 'inprogress':
      return 'mai-pulse bg-entity-session/14 text-entity-session ring-entity-session/30';
    case 'planned':
      return 'bg-entity-event/14 text-entity-event ring-entity-event/30';
  }
}

function buildBanner(
  variant: PlayRecordHeroVariant,
  game: PlayRecordHeroGameSummary,
  rankedScores: ReadonlyArray<RankedScore>,
  labels: PlayRecordHeroPodiumLabels,
  metadata: PlayRecordHeroMetadata
): string {
  const top = rankedScores[0];
  switch (variant) {
    case 'won':
      return labels.bannerWon(top?.name ?? '', game.name);
    case 'tied':
      return labels.bannerTied(top?.score ?? 0);
    case 'cooperative':
      return labels.bannerCooperative(game.name);
    case 'inprogress':
      return labels.bannerInProgress(game.name, metadata.turn);
    case 'planned':
      return labels.bannerPlanned(game.name);
  }
}

// ── sub-components ──────────────────────────────────────────────────────────

interface CoverProps {
  readonly game: PlayRecordHeroGameSummary;
}

function HeroCover({ game }: CoverProps): ReactElement {
  return (
    <div
      data-slot="hero-cover"
      aria-hidden="true"
      className="flex h-14 w-14 shrink-0 items-center justify-center rounded-lg bg-muted text-3xl sm:h-16 sm:w-16"
    >
      {game.coverEmoji ?? '🎲'}
    </div>
  );
}

interface PodiumPlaceProps {
  readonly entry: RankedScore;
  readonly place: number;
  readonly variant: PlayRecordHeroVariant;
  readonly isCurrentUser: boolean;
  readonly showCrown: boolean;
}

function PodiumPlace({
  entry,
  place,
  variant,
  isCurrentUser,
  showCrown,
}: PodiumPlaceProps): ReactElement {
  const isFirst = place === 1;
  const hue = hashHue(entry.playerId || entry.name);
  // Avatar sizing — first place taller, flanks shorter
  const avatarSize = isFirst ? 'h-16 w-16 sm:h-20 sm:w-20' : 'h-12 w-12 sm:h-14 sm:w-14';
  const fontSize = isFirst ? 'text-xl sm:text-2xl' : 'text-base sm:text-lg';
  const scoreSize = isFirst ? 'text-xl sm:text-2xl' : 'text-base sm:text-lg';

  return (
    <div
      data-slot="podium-place"
      data-place={place}
      data-current-user={isCurrentUser ? 'true' : undefined}
      className="flex flex-col items-center gap-1"
    >
      {showCrown && (
        <span
          data-slot="podium-winner-crown"
          aria-hidden="true"
          className="text-base leading-none sm:text-xl"
        >
          👑
        </span>
      )}
      <div
        className={clsx(
          'relative flex items-center justify-center rounded-full font-extrabold text-white shadow-md', // eslint-disable-line local/no-hardcoded-color-utility -- text-white on style-prop gradient bg (.e-bg pattern; mockup HeroPodium)
          avatarSize,
          fontSize,
          isCurrentUser && 'ring-2 ring-entity-session/60 ring-offset-2 ring-offset-background'
        )}
        style={{
          background: `linear-gradient(135deg, hsl(${hue}, 70%, 62%), hsl(${hue}, 60%, 42%))`,
        }}
      >
        {getInitials(entry.name)}
      </div>
      <div className="text-center text-xs font-extrabold text-foreground sm:text-sm">
        {entry.name}
      </div>
      <div
        className={clsx(
          'leading-none font-extrabold tabular-nums',
          scoreSize,
          variant === 'won' || variant === 'tied' ? 'text-entity-toolkit' : 'text-muted-foreground'
        )}
      >
        {entry.score === null ? '—' : entry.score}
      </div>
    </div>
  );
}

// ── main component ──────────────────────────────────────────────────────────

export function PlayRecordHeroPodium({
  variant,
  game,
  rankedScores,
  metadata,
  perspective,
  labels,
  onStart,
  className,
}: PlayRecordHeroPodiumProps): ReactElement {
  const headlineId = useId();
  const layout = buildPodiumLayout(rankedScores);
  const banner = buildBanner(variant, game, rankedScores, labels, metadata);

  const showPodium = variant !== 'planned';
  const showCta = variant === 'planned';
  // Crown logic:
  //  - won → only rank-1 (single crown)
  //  - tied → all top scorers tied at the highest score
  //  - cooperative/inprogress/planned → no crown
  const crownPredicate = (entry: RankedScore): boolean => {
    if (variant === 'won') return entry.isWinner && rankedScores[0]?.playerId === entry.playerId;
    if (variant === 'tied') return entry.isWinner;
    return false;
  };

  const metaParts: ReactNode[] = [metadata.date];
  if (metadata.duration !== null) {
    metaParts.push(metadata.duration);
  }
  metaParts.push(labels.metaPlayers(metadata.playerCount));
  const metaText = metaParts.join(' · ');

  return (
    <section
      data-slot="play-record-hero-podium"
      data-variant={variant}
      aria-labelledby={headlineId}
      className={clsx(
        'relative overflow-hidden border-b border-border bg-card px-4 py-6 sm:px-8 sm:py-8',
        className
      )}
    >
      <div className="relative z-10 mx-auto flex max-w-3xl flex-col items-center gap-3 text-center">
        {/* badge */}
        <span
          data-slot="hero-badge"
          role={variant === 'inprogress' ? 'status' : undefined}
          className={clsx(
            'inline-flex items-center gap-1 rounded-full px-2.5 py-1 font-mono text-[10px] font-extrabold uppercase tracking-wider ring-1',
            variantBadgeClassName(variant)
          )}
        >
          {variant === 'inprogress' && (
            <span
              aria-hidden="true"
              className="inline-block h-1.5 w-1.5 rounded-full bg-entity-session"
            />
          )}
          {variantBadgeLabel(variant, labels)}
        </span>

        {/* cover + title block */}
        <div className="flex flex-col items-center gap-3 sm:flex-row sm:items-start sm:justify-center">
          {variant === 'planned' && <HeroCover game={game} />}
          <h1
            id={headlineId}
            className="font-display text-xl font-extrabold tracking-tight text-foreground sm:text-2xl"
          >
            {banner}
          </h1>
        </div>

        {/* meta strip */}
        <div className="font-mono text-xs font-bold text-muted-foreground">{metaText}</div>

        {/* podium */}
        {showPodium && layout.length > 0 && (
          <div data-slot="podium-row" className="mt-3 flex items-end justify-center gap-3 sm:gap-6">
            {layout.map(({ entry, place }) => (
              <PodiumPlace
                key={entry.playerId}
                entry={entry}
                place={place}
                variant={variant}
                isCurrentUser={
                  perspective.currentUserPlayerId !== null &&
                  perspective.currentUserPlayerId === entry.playerId
                }
                showCrown={crownPredicate(entry)}
              />
            ))}
          </div>
        )}

        {/* CTA — planned variant */}
        {showCta && (
          <button
            type="button"
            onClick={onStart}
            className="mt-3 inline-flex items-center gap-1 rounded-md bg-entity-session px-4 py-2 text-sm font-extrabold text-white shadow-md transition-shadow hover:shadow-lg focus:outline-none focus-visible:ring-2 focus-visible:ring-entity-session/60 focus-visible:ring-offset-2"
          >
            <span aria-hidden="true">▶</span>
            <span>{labels.ctaStart}</span>
          </button>
        )}
      </div>
    </section>
  );
}
