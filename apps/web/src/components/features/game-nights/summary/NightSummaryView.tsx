import type { JSX } from 'react';

import {
  PerGameRecapRow,
  type PerGameRecapGame,
  type PerGameRecapPlayer,
} from '@/components/features/game-nights/summary/PerGameRecapRow';
import { ArchivedBanner } from '@/components/ui/archived-banner';
import { KPIStatCard, KPIStatGrid } from '@/components/ui/kpi-stat-grid';
import { ShareSuccessToast } from '@/components/ui/share-success-toast';

export interface NightSummaryNight {
  readonly title: string;
  readonly dateLine: string;
  readonly location?: string;
  readonly startedAt: string;
  readonly endedAt: string;
  readonly duration: string;
  readonly nightCode?: string;
}

export interface NightSummaryMVP extends PerGameRecapPlayer {
  readonly achievements?: string;
}

export interface NightSummaryPhoto {
  readonly id: string;
  readonly label?: string;
  readonly gradient: readonly [string, string];
}

export interface NightSummaryViewProps {
  readonly night: NightSummaryNight;
  readonly mvp?: NightSummaryMVP | null;
  readonly games: ReadonlyArray<PerGameRecapGame>;
  readonly eventsCount: number;
  readonly photos?: ReadonlyArray<NightSummaryPhoto>;
  readonly mobile?: boolean;
  readonly archived?: boolean;
  readonly shareSuccess?: {
    readonly visible: boolean;
    readonly url?: string;
    readonly subline?: string;
  };
  readonly onShare?: () => void;
  readonly onArchive?: () => void;
  readonly onUnarchive?: () => void;
  readonly onGoToList?: () => void;
  readonly onJumpToSession?: (sessionId: string) => void;
  readonly onAddPhoto?: () => void;
  readonly className?: string;
}

export function NightSummaryView({
  night,
  mvp,
  games,
  eventsCount,
  photos = [],
  mobile = false,
  archived = false,
  shareSuccess,
  onShare,
  onArchive,
  onUnarchive,
  onGoToList,
  onJumpToSession,
  onAddPhoto,
  className,
}: NightSummaryViewProps): JSX.Element {
  const winnersCount = games.filter(g => g.coopMode || g.winner).length;
  const coopGamesCount = games.filter(g => g.coopMode).length;
  const photosEmpty = photos.length === 0;
  const sessionsLabel =
    games.length === 1 ? '1 game · serata breve' : `${games.length} game completati`;

  return (
    <article
      data-archived={archived ? 'true' : 'false'}
      data-mobile={mobile ? 'true' : 'false'}
      className={['relative flex flex-col bg-background min-h-full', className ?? '']
        .filter(Boolean)
        .join(' ')}
    >
      <NightSummaryHero night={night} mvp={mvp ?? null} gamesCount={games.length} mobile={mobile} />

      {archived ? (
        <div className={mobile ? 'px-3.5 pt-3.5' : 'px-8 pt-5'}>
          <ArchivedBanner
            title="Serata archiviata"
            subtitle="Riepilogo salvato · accessibile da /game-nights/archived"
            action={
              onGoToList ? (
                <button
                  type="button"
                  onClick={onGoToList}
                  className="inline-flex items-center gap-1 rounded-md border border-border-strong bg-card px-3 py-2 font-display text-[12.5px] font-extrabold text-foreground cursor-pointer whitespace-nowrap"
                >
                  ← Torna alla lista
                </button>
              ) : undefined
            }
          />
        </div>
      ) : null}

      {/* KPI grid */}
      <section
        aria-label="Stats della serata"
        className={mobile ? 'px-3.5 pt-4 pb-1' : 'px-8 pt-6 pb-2'}
      >
        <KPIStatGrid columns={4}>
          <KPIStatCard
            icon="🎯"
            tone="session"
            label="Sessioni totali"
            value={games.length}
            sub={sessionsLabel}
          />
          <KPIStatCard
            icon="⏱"
            tone="event"
            label="Durata totale"
            value={night.duration}
            sub={`${night.startedAt} → ${night.endedAt}`}
          />
          <KPIStatCard
            icon="📜"
            tone="chat"
            label="Eventi diary"
            value={eventsCount}
            sub={`${(eventsCount / Math.max(games.length, 1)).toFixed(1)} avg / session`}
          />
          <KPIStatCard
            icon="🏆"
            tone="player"
            label="Winner per gioco"
            value={`${winnersCount}/${games.length}`}
            sub={
              coopGamesCount > 0
                ? `${coopGamesCount} coop · ${games.length - coopGamesCount} competitive`
                : 'tutti competitive'
            }
          />
        </KPIStatGrid>
      </section>

      {/* Per-game recap */}
      <section
        aria-label="Per-game recap"
        className={['flex flex-col gap-3', mobile ? 'px-3.5 py-4' : 'px-8 py-5'].join(' ')}
      >
        <div>
          <span className="block font-mono text-[10px] font-extrabold uppercase tracking-wider text-entity-event">
            Per-game recap
          </span>
          <h2 className="mt-0.5 m-0 font-display text-lg font-extrabold tracking-tight text-foreground">
            {games.length === 1
              ? '1 gioco completato'
              : `${games.length} giochi completati · in ordine cronologico`}
          </h2>
        </div>
        <div className="flex flex-col gap-2">
          {games.map(g => (
            <PerGameRecapRow
              key={g.id}
              game={g}
              mobile={mobile}
              onJumpToSession={onJumpToSession}
            />
          ))}
        </div>
      </section>

      {/* Photo gallery */}
      <section
        aria-label="Foto della serata"
        className={['flex flex-col gap-2.5', mobile ? 'px-3.5 pt-2 pb-4' : 'px-8 pt-3 pb-5'].join(
          ' '
        )}
      >
        <div className="flex flex-wrap items-center justify-between gap-1.5">
          <div>
            <span className="block font-mono text-[10px] font-extrabold uppercase tracking-wider text-entity-event">
              Foto della serata
            </span>
            <h2 className="mt-0.5 m-0 font-display text-lg font-extrabold tracking-tight text-foreground">
              {photosEmpty ? 'Gallery vuota' : `${photos.length} foto caricate`}
            </h2>
          </div>
          {!photosEmpty && onAddPhoto ? (
            <button
              type="button"
              onClick={onAddPhoto}
              className="rounded-pill border border-entity-event/30 bg-entity-event/10 px-2.5 py-1 font-mono text-[10px] font-extrabold uppercase tracking-wider text-entity-event cursor-pointer"
            >
              + Aggiungi foto
            </button>
          ) : null}
        </div>
        <PhotoGallery photos={photos} mobile={mobile} onAddFirstPhoto={onAddPhoto} />
      </section>

      {/* Footer CTAs */}
      <footer
        className={[
          'flex shrink-0 flex-wrap items-center gap-2 border-t border-border bg-card',
          mobile ? 'px-3.5 py-3' : 'px-8 py-4',
        ].join(' ')}
      >
        <button
          type="button"
          onClick={onShare}
          className="inline-flex items-center gap-1.5 rounded-md border border-entity-toolkit/30 bg-entity-toolkit/10 px-3 py-2 font-display text-[12.5px] font-extrabold text-entity-toolkit-text cursor-pointer"
        >
          <span aria-hidden="true">📤</span>
          Condividi riepilogo
        </button>
        <button
          type="button"
          onClick={archived ? onUnarchive : onArchive}
          className="inline-flex items-center gap-1.5 rounded-md border border-border-strong bg-transparent px-3 py-2 font-display text-[12.5px] font-extrabold text-foreground cursor-pointer"
        >
          <span aria-hidden="true">{archived ? '↺' : '✓'}</span>
          {archived ? 'Disarchivia' : 'Archivia'}
        </button>
      </footer>

      {/* Share success toast */}
      {shareSuccess?.visible ? (
        <ShareSuccessToast
          visible
          title="Link copiato"
          subline={shareSuccess.subline ?? shareSuccess.url}
        />
      ) : null}
    </article>
  );
}

interface NightSummaryHeroProps {
  readonly night: NightSummaryNight;
  readonly mvp: NightSummaryMVP | null;
  readonly gamesCount: number;
  readonly mobile: boolean;
}

function NightSummaryHero({ night, mvp, gamesCount, mobile }: NightSummaryHeroProps): JSX.Element {
  return (
    <header
      className={[
        'relative overflow-hidden border-b border-entity-event/20',
        mobile ? 'px-4 pt-5 pb-5' : 'px-8 pt-8 pb-7',
      ].join(' ')}
      style={{
        background:
          'linear-gradient(160deg, hsl(var(--c-event) / 0.18) 0%, hsl(var(--c-event) / 0.03) 100%)',
      }}
    >
      {/* Decorative confetti shapes */}
      <span
        aria-hidden="true"
        className="pointer-events-none absolute opacity-40"
        style={{
          top: -40,
          right: -40,
          width: 220,
          height: 220,
          borderRadius: '50%',
          background:
            'radial-gradient(circle at 30% 30%, hsl(var(--c-event) / 0.4), transparent 70%)',
        }}
      />
      <span
        aria-hidden="true"
        className="pointer-events-none absolute opacity-25"
        style={{
          bottom: -30,
          left: -30,
          width: 160,
          height: 160,
          borderRadius: '50%',
          background:
            'radial-gradient(circle at 40% 40%, hsl(var(--c-toolkit) / 0.4), transparent 70%)',
        }}
      />

      {/* Status badge */}
      <div className="relative mb-3 flex items-center gap-2">
        <span className="inline-flex items-center gap-1.5 rounded-pill border border-border-strong bg-card px-2.5 py-1 font-mono text-[10px] font-extrabold uppercase tracking-wider text-muted-foreground">
          <span aria-hidden="true">✓</span>
          Serata completata
        </span>
        {night.nightCode ? (
          <span className="font-mono text-[10px] font-extrabold uppercase tracking-wider text-muted-foreground">
            {night.nightCode} · {gamesCount} {gamesCount === 1 ? 'gioco' : 'giochi'}
          </span>
        ) : null}
      </div>

      {/* Title */}
      <h1
        className={[
          'relative m-0 mb-1.5 font-display font-extrabold leading-tight tracking-tight text-foreground',
          mobile ? 'text-[26px]' : 'text-[36px]',
        ].join(' ')}
      >
        {night.title}
      </h1>

      {/* Date + location */}
      <div
        className={[
          'relative flex flex-wrap items-center gap-x-3.5 gap-y-1 font-mono text-xs font-bold text-muted-foreground',
          mobile ? 'mb-4' : 'mb-5',
        ].join(' ')}
      >
        <span className="inline-flex items-center gap-1.5">
          <span aria-hidden="true">📅</span>
          {night.dateLine}
        </span>
        <span aria-hidden="true">·</span>
        <span className="inline-flex items-center gap-1.5">
          <span aria-hidden="true">⏱</span>
          {night.startedAt} → {night.endedAt} ·{' '}
          <strong className="text-foreground">{night.duration}</strong>
        </span>
        {night.location ? (
          <>
            <span aria-hidden="true">·</span>
            <span className="inline-flex items-center gap-1.5">
              <span aria-hidden="true">📍</span>
              {night.location}
            </span>
          </>
        ) : null}
      </div>

      {/* MVP banner */}
      {mvp ? (
        <div
          className={[
            'relative flex items-center rounded-xl border-2 border-entity-event/40 bg-entity-event/15 shadow-md ring-4 ring-entity-event/[0.08]',
            mobile ? 'gap-3 px-3.5 py-3' : 'gap-4 px-5 py-4 max-w-[540px]',
          ].join(' ')}
        >
          <div className="relative shrink-0">
            <span
              aria-label={`MVP avatar ${mvp.initials}`}
              className={[
                'inline-flex items-center justify-center rounded-full border-2 font-display font-extrabold',
                mobile ? 'h-12 w-12 text-[14px]' : 'h-[60px] w-[60px] text-[17px]',
              ].join(' ')}
              style={{
                background: `hsl(${mvp.color}, 60%, 55%)`,
                color: '#fff',
                borderColor: 'hsl(var(--c-event) / 0.5)' as string,
              }}
            >
              {mvp.initials}
            </span>
            <span
              aria-hidden="true"
              className="absolute -right-2 -top-1.5 drop-shadow"
              style={{ fontSize: mobile ? 20 : 26 }}
            >
              🏆
            </span>
          </div>
          <div className="min-w-0 flex-1">
            <span className="block font-mono text-[10px] font-extrabold uppercase tracking-wider text-entity-event">
              MVP della serata
            </span>
            <h2
              className={[
                'mt-px m-0 font-display font-extrabold leading-tight tracking-tight text-foreground',
                mobile ? 'text-[19px]' : 'text-2xl',
              ].join(' ')}
            >
              {mvp.name}
            </h2>
            {mvp.achievements ? (
              <span className="mt-1 block font-mono text-[10.5px] font-bold text-muted-foreground">
                {mvp.achievements}
              </span>
            ) : null}
          </div>
          <span
            aria-hidden="true"
            className="-mr-1 shrink-0 text-entity-event opacity-35"
            style={{ fontSize: mobile ? 28 : 36 }}
          >
            ✨
          </span>
        </div>
      ) : null}
    </header>
  );
}

interface PhotoGalleryProps {
  readonly photos: ReadonlyArray<NightSummaryPhoto>;
  readonly mobile: boolean;
  readonly onAddFirstPhoto?: () => void;
}

function PhotoGallery({ photos, mobile, onAddFirstPhoto }: PhotoGalleryProps): JSX.Element {
  if (photos.length === 0) {
    return (
      <div
        className={[
          'flex flex-col items-center justify-center gap-2 rounded-lg border border-dashed bg-entity-event/[0.04] py-8 px-4 text-center',
          'border-entity-event/30',
        ].join(' ')}
      >
        <span aria-hidden="true" className="text-3xl opacity-60">
          📷
        </span>
        <span className="font-display text-sm font-extrabold text-foreground">
          Nessuna foto ancora
        </span>
        {onAddFirstPhoto ? (
          <button
            type="button"
            onClick={onAddFirstPhoto}
            className="mt-1 rounded-md border-0 bg-entity-event px-3 py-2 font-display text-[12.5px] font-extrabold cursor-pointer"
            style={{ color: '#fff' }}
          >
            + Aggiungi prima foto
          </button>
        ) : null}
      </div>
    );
  }

  return (
    <ul
      role="list"
      className={['grid gap-2 m-0 p-0 list-none', mobile ? 'grid-cols-3' : 'grid-cols-4'].join(' ')}
    >
      {photos.map(photo => (
        <li
          key={photo.id}
          className="relative aspect-square overflow-hidden rounded-md shadow-sm"
          style={{
            background: `linear-gradient(135deg, ${photo.gradient[0]}, ${photo.gradient[1]})`,
          }}
        >
          {photo.label ? (
            <span
              className="absolute bottom-1 left-1 right-1 truncate rounded-sm px-1.5 py-0.5 font-mono text-[9px] font-bold"
              style={{ background: 'rgba(0,0,0,0.55)', color: '#fff' }}
            >
              {photo.label}
            </span>
          ) : null}
        </li>
      ))}
    </ul>
  );
}
