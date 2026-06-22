import type { JSX } from 'react';
import { useState } from 'react';

import {
  CrossGameDiaryTimeline,
  type DiaryEvent,
  type DiaryGameRef,
  type DiaryPlayerRef,
} from '@/components/features/game-nights/live/CrossGameDiaryTimeline';
import {
  PlannedGamesPane,
  type PlannedGame,
} from '@/components/features/game-nights/live/PlannedGamesPane';
import { AutoSaveToast } from '@/components/ui/auto-save-toast';

export type NightLiveStatus = 'live' | 'paused' | 'transition';
export type MobileTab = 'current' | 'planned' | 'diary';

export interface NightLiveHubNight {
  readonly title: string;
  readonly shortTitle?: string;
  readonly nightCode?: string;
}

export interface NightLiveHubCurrentGame {
  readonly id: string;
  readonly sessionId: string;
  readonly title: string;
  readonly emoji?: string;
  readonly cover?: readonly [string, string];
  readonly score?: string;
}

export interface NightLiveHubProps {
  readonly night: NightLiveHubNight;
  readonly status?: NightLiveStatus;
  readonly current: number;
  readonly total: number;
  readonly elapsed: string;
  readonly confirmedPlayers?: number;
  readonly totalPlayers?: number;
  readonly plannedGames: ReadonlyArray<PlannedGame>;
  readonly currentGame: NightLiveHubCurrentGame | null;
  readonly diaryEvents: ReadonlyArray<DiaryEvent>;
  readonly diaryGames: ReadonlyArray<DiaryGameRef>;
  readonly diaryPlayers: ReadonlyArray<DiaryPlayerRef>;
  readonly autoSaveToast?: { readonly visible: boolean; readonly timestamp: string };
  readonly mobile?: boolean;
  readonly initialMobileTab?: MobileTab;
  readonly onBack?: () => void;
  readonly onPauseToggle?: () => void;
  readonly onTransition?: () => void;
  readonly onEnd?: () => void;
  readonly onJumpToSession?: (sessionId: string) => void;
  readonly onAddGame?: () => void;
  readonly className?: string;
}

const STATUS_LABEL: Record<NightLiveStatus, string> = {
  live: 'Live',
  paused: 'In pausa',
  transition: 'Transition',
};

const STATUS_TONE: Record<NightLiveStatus, 'session' | 'warning' | 'event'> = {
  live: 'session',
  paused: 'warning',
  transition: 'event',
};

export function NightLiveHub({
  night,
  status = 'live',
  current,
  total,
  elapsed,
  confirmedPlayers,
  totalPlayers,
  plannedGames,
  currentGame,
  diaryEvents,
  diaryGames,
  diaryPlayers,
  autoSaveToast,
  mobile = false,
  initialMobileTab = 'current',
  onBack,
  onPauseToggle,
  onTransition,
  onEnd,
  onJumpToSession,
  onAddGame,
  className,
}: NightLiveHubProps): JSX.Element {
  const isPaused = status === 'paused';

  return (
    <div
      data-night-status={status}
      data-mobile={mobile ? 'true' : 'false'}
      className={[
        'relative flex h-full min-h-0 flex-col overflow-hidden bg-background',
        className ?? '',
      ]
        .filter(Boolean)
        .join(' ')}
    >
      <NightLiveTopBar
        night={night}
        status={status}
        current={current}
        total={total}
        elapsed={elapsed}
        confirmedPlayers={confirmedPlayers}
        totalPlayers={totalPlayers}
        compact={mobile}
        isPaused={isPaused}
        onBack={onBack}
        onPauseToggle={onPauseToggle}
        onTransition={onTransition}
        onEnd={onEnd}
      />

      {mobile ? (
        <MobileBody
          initialTab={initialMobileTab}
          plannedGames={plannedGames}
          currentGame={currentGame}
          diaryEvents={diaryEvents}
          diaryGames={diaryGames}
          diaryPlayers={diaryPlayers}
          onJumpToSession={onJumpToSession}
          onAddGame={onAddGame}
        />
      ) : (
        <div className="flex min-h-0 flex-1">
          <PlannedGamesPane games={plannedGames} onAddGame={onAddGame} />
          <CurrentGameCard
            game={currentGame}
            score={currentGame?.score}
            onJumpToSession={onJumpToSession}
          />
          <aside
            aria-label="Cross-game diary"
            className="flex w-[320px] shrink-0 flex-col border-l border-border bg-card p-3 overflow-y-auto"
          >
            <CrossGameDiaryTimeline
              events={diaryEvents}
              games={diaryGames}
              players={diaryPlayers}
            />
          </aside>
        </div>
      )}

      {autoSaveToast?.visible ? (
        <AutoSaveToast visible timestamp={autoSaveToast.timestamp} />
      ) : null}
    </div>
  );
}

interface NightLiveTopBarProps {
  readonly night: NightLiveHubNight;
  readonly status: NightLiveStatus;
  readonly current: number;
  readonly total: number;
  readonly elapsed: string;
  readonly confirmedPlayers?: number;
  readonly totalPlayers?: number;
  readonly compact: boolean;
  readonly isPaused: boolean;
  readonly onBack?: () => void;
  readonly onPauseToggle?: () => void;
  readonly onTransition?: () => void;
  readonly onEnd?: () => void;
}

function NightLiveTopBar({
  night,
  status,
  current,
  total,
  elapsed,
  confirmedPlayers,
  totalPlayers,
  compact,
  isPaused,
  onBack,
  onPauseToggle,
  onTransition,
  onEnd,
}: NightLiveTopBarProps): JSX.Element {
  const tone = STATUS_TONE[status];
  const toneBgClass =
    tone === 'session'
      ? 'bg-entity-session/15 border-entity-session/30 text-entity-session'
      : tone === 'event'
        ? 'bg-entity-event/15 border-entity-event/30 text-entity-event'
        : 'bg-[hsl(var(--c-warning)/0.14)] border-[hsl(var(--c-warning)/0.3)] text-[hsl(var(--c-warning))]';

  return (
    <header
      className={[
        'sticky top-0 z-20 flex flex-col bg-card/80 backdrop-blur-md',
        'border-b border-entity-event/25',
      ].join(' ')}
    >
      <div
        className={[
          'flex items-center gap-2',
          compact ? 'px-3 py-2.5 min-h-[56px]' : 'px-4 py-3 gap-3 min-h-[64px]',
        ].join(' ')}
      >
        {onBack ? (
          <button
            type="button"
            aria-label="Indietro"
            onClick={onBack}
            className="shrink-0 rounded-md border border-border bg-card font-display text-base font-extrabold text-muted-foreground cursor-pointer h-[34px] w-[34px]"
          >
            ←
          </button>
        ) : null}

        <div className="flex min-w-0 flex-1 flex-col gap-0.5">
          <div className="flex flex-wrap items-center gap-1.5">
            <span
              aria-label={`Status: ${STATUS_LABEL[status]}`}
              className={[
                'inline-flex items-center gap-1 rounded-pill border px-2 py-0.5',
                'font-mono text-[9.5px] font-extrabold uppercase tracking-wider',
                toneBgClass,
              ].join(' ')}
            >
              {isPaused ? (
                <span aria-hidden="true">⏸</span>
              ) : (
                <span
                  aria-hidden="true"
                  className="motion-safe:animate-pulse inline-block h-1.5 w-1.5 rounded-full"
                  style={{
                    background:
                      tone === 'session' ? 'hsl(var(--c-session))' : 'hsl(var(--c-event))',
                  }}
                />
              )}
              {STATUS_LABEL[status]}
            </span>
            {night.nightCode ? (
              <span className="font-mono text-[9.5px] font-extrabold uppercase tracking-wider text-muted-foreground">
                {night.nightCode}
              </span>
            ) : null}
          </div>
          <div
            className={[
              'flex items-center gap-1.5 truncate font-display font-extrabold text-foreground',
              compact ? 'text-[13px]' : 'text-[14.5px]',
            ].join(' ')}
          >
            <span aria-hidden="true">🎉</span>
            <span className="truncate">
              {compact && night.shortTitle ? night.shortTitle : night.title}
            </span>
          </div>
        </div>

        {!compact ? (
          <div className="flex shrink-0 items-center gap-3 rounded-md border border-entity-event/20 bg-entity-event/[0.08] px-3.5 py-1.5">
            <div className="text-center">
              <span className="block font-mono text-[9px] font-extrabold uppercase tracking-wider text-muted-foreground">
                Game
              </span>
              <span className="font-display text-base font-extrabold leading-none tabular-nums text-entity-event">
                {current}
                <span className="text-muted-foreground">/{total}</span>
              </span>
            </div>
            <span className="h-[22px] w-px bg-border" />
            <div className="text-center">
              <span className="block font-mono text-[9px] font-extrabold uppercase tracking-wider text-muted-foreground">
                Elapsed
              </span>
              <span className="font-mono text-[13.5px] font-extrabold leading-tight tabular-nums text-foreground">
                {elapsed}
              </span>
            </div>
            {confirmedPlayers !== undefined && totalPlayers !== undefined ? (
              <>
                <span className="h-[22px] w-px bg-border" />
                <div className="text-center">
                  <span className="block font-mono text-[9px] font-extrabold uppercase tracking-wider text-muted-foreground">
                    Player
                  </span>
                  <span className="font-mono text-[13.5px] font-extrabold leading-tight tabular-nums text-entity-player">
                    {confirmedPlayers}/{totalPlayers}
                  </span>
                </div>
              </>
            ) : null}
          </div>
        ) : null}

        <div className="flex shrink-0 items-center gap-1.5">
          <ToolbarButton
            icon={isPaused ? '▶' : '⏸'}
            label={compact ? null : isPaused ? 'Riprendi' : 'Pausa'}
            tone={isPaused ? 'session' : null}
            onClick={onPauseToggle}
          />
          <ToolbarButton
            icon="➡️"
            label={compact ? null : 'Transition'}
            tone="event"
            onClick={onTransition}
          />
          <ToolbarButton icon="🛑" label={compact ? null : 'End'} tone="danger" onClick={onEnd} />
        </div>
      </div>

      {compact ? (
        <div className="flex items-center gap-2 border-t border-border-light bg-card px-3 py-1.5 font-mono text-[10.5px] font-bold text-foreground/70">
          <span>
            <strong className="tabular-nums text-entity-event">
              Game {current}/{total}
            </strong>
          </span>
          <span className="text-muted-foreground">·</span>
          <span>
            Elapsed <strong className="tabular-nums text-foreground">{elapsed}</strong>
          </span>
          {confirmedPlayers !== undefined && totalPlayers !== undefined ? (
            <>
              <span className="text-muted-foreground">·</span>
              <span>
                <strong className="tabular-nums text-entity-player">
                  {confirmedPlayers}/{totalPlayers}
                </strong>{' '}
                player
              </span>
            </>
          ) : null}
        </div>
      ) : null}
    </header>
  );
}

interface ToolbarButtonProps {
  readonly icon: string;
  readonly label: string | null;
  readonly tone: 'session' | 'event' | 'danger' | null;
  readonly onClick?: () => void;
}

function ToolbarButton({ icon, label, tone, onClick }: ToolbarButtonProps): JSX.Element {
  const cls =
    tone === 'session'
      ? 'border-entity-session/30 bg-entity-session/10 text-entity-session'
      : tone === 'event'
        ? 'border-entity-event/30 bg-entity-event/10 text-entity-event'
        : tone === 'danger'
          ? 'border-[hsl(var(--c-danger)/0.3)] bg-[hsl(var(--c-danger)/0.1)] text-[hsl(var(--c-danger))]'
          : 'border-border bg-card text-muted-foreground';

  return (
    <button
      type="button"
      aria-label={label ?? icon}
      onClick={onClick}
      className={[
        'inline-flex items-center gap-1.5 rounded-md border cursor-pointer',
        'font-display text-[12.5px] font-extrabold',
        label ? 'px-3 py-1.5' : 'px-2 py-1.5',
        cls,
      ].join(' ')}
    >
      <span aria-hidden="true">{icon}</span>
      {label}
    </button>
  );
}

interface CurrentGameCardProps {
  readonly game: NightLiveHubCurrentGame | null;
  readonly score?: string;
  readonly onJumpToSession?: (sessionId: string) => void;
  readonly compact?: boolean;
}

function CurrentGameCard({
  game,
  score,
  onJumpToSession,
  compact = false,
}: CurrentGameCardProps): JSX.Element {
  if (!game) {
    return (
      <section
        aria-label="Current game"
        className="flex flex-1 items-center justify-center bg-card font-mono text-xs text-muted-foreground p-6"
      >
        Nessun gioco corrente · in attesa transition
      </section>
    );
  }

  return (
    <section
      aria-label="Current game"
      className={[
        'flex min-w-0 flex-1 flex-col gap-4 overflow-y-auto bg-card',
        compact ? 'p-3.5' : 'px-6 py-5',
      ].join(' ')}
    >
      <div className="flex flex-wrap items-center gap-2">
        <span className="font-mono text-[10px] font-extrabold uppercase tracking-wider text-muted-foreground">
          Current game
        </span>
        {score ? (
          <span className="inline-flex items-center gap-1.5 rounded-pill border border-entity-session/30 bg-entity-session/15 px-2 py-0.5 font-mono text-[9.5px] font-extrabold uppercase tracking-wider text-entity-session">
            <span
              aria-hidden="true"
              className="motion-safe:animate-pulse inline-block h-1.5 w-1.5 rounded-full bg-entity-session"
            />
            {score}
          </span>
        ) : null}
      </div>

      <div className="overflow-hidden rounded-xl border border-entity-session/25 ring-4 ring-entity-session/[0.08] shadow-md">
        <div
          aria-hidden="true"
          className={['flex items-end p-4', compact ? 'h-[140px]' : 'h-[200px]'].join(' ')}
          style={
            game.cover
              ? {
                  background: `linear-gradient(135deg, ${game.cover[0]}, ${game.cover[1]})`,
                }
              : { background: 'var(--bg-muted)' }
          }
        >
          <span
            className="font-display text-3xl font-extrabold drop-shadow-lg"
            style={{ color: '#fff' }}
          >
            {game.emoji ?? '🎲'}
          </span>
        </div>
        <div className="flex items-center justify-between gap-3 px-4 py-3">
          <div className="min-w-0 flex-1">
            <h2 className="truncate font-display text-lg font-extrabold text-foreground">
              {game.title}
            </h2>
            <span className="font-mono text-[10px] font-bold text-muted-foreground">
              {game.sessionId}
            </span>
          </div>
          {onJumpToSession ? (
            <button
              type="button"
              onClick={() => onJumpToSession(game.sessionId)}
              className="shrink-0 rounded-md border border-entity-session/30 bg-entity-session/10 px-3 py-1.5 font-display text-[12.5px] font-extrabold text-entity-session cursor-pointer"
            >
              Apri sessione live →
            </button>
          ) : null}
        </div>
      </div>
    </section>
  );
}

interface MobileBodyProps {
  readonly initialTab: MobileTab;
  readonly plannedGames: ReadonlyArray<PlannedGame>;
  readonly currentGame: NightLiveHubCurrentGame | null;
  readonly diaryEvents: ReadonlyArray<DiaryEvent>;
  readonly diaryGames: ReadonlyArray<DiaryGameRef>;
  readonly diaryPlayers: ReadonlyArray<DiaryPlayerRef>;
  readonly onJumpToSession?: (sessionId: string) => void;
  readonly onAddGame?: () => void;
}

function MobileBody({
  initialTab,
  plannedGames,
  currentGame,
  diaryEvents,
  diaryGames,
  diaryPlayers,
  onJumpToSession,
  onAddGame,
}: MobileBodyProps): JSX.Element {
  const [tab, setTab] = useState<MobileTab>(initialTab);
  const TABS: ReadonlyArray<{
    id: MobileTab;
    icon: string;
    label: string;
    tone: 'session' | 'game' | 'event';
  }> = [
    { id: 'current', icon: '🎯', label: 'Current', tone: 'session' },
    { id: 'planned', icon: '🎲', label: 'Planned', tone: 'game' },
    { id: 'diary', icon: '📜', label: 'Diary', tone: 'event' },
  ];

  return (
    <>
      <div className="flex min-h-0 flex-1 flex-col overflow-y-auto bg-background">
        {tab === 'current' ? (
          <CurrentGameCard game={currentGame} onJumpToSession={onJumpToSession} compact />
        ) : null}
        {tab === 'planned' ? (
          <PlannedGamesPane games={plannedGames} onAddGame={onAddGame} compact />
        ) : null}
        {tab === 'diary' ? (
          <div className="p-3">
            <CrossGameDiaryTimeline
              events={diaryEvents}
              games={diaryGames}
              players={diaryPlayers}
              compact
            />
          </div>
        ) : null}
      </div>
      <nav
        role="tablist"
        aria-label="Hub mobile tabs"
        className="flex shrink-0 border-t border-border bg-card/80 backdrop-blur-md"
      >
        {TABS.map(t => {
          const active = tab === t.id;
          const toneClass =
            t.tone === 'session'
              ? active
                ? 'bg-entity-session/[0.08] border-t-entity-session text-entity-session'
                : 'border-t-transparent text-muted-foreground'
              : t.tone === 'game'
                ? active
                  ? 'bg-entity-game/[0.08] border-t-entity-game text-entity-game'
                  : 'border-t-transparent text-muted-foreground'
                : active
                  ? 'bg-entity-event/[0.08] border-t-entity-event text-entity-event'
                  : 'border-t-transparent text-muted-foreground';

          return (
            <button
              key={t.id}
              type="button"
              role="tab"
              aria-selected={active}
              aria-controls={`night-live-pane-${t.id}`}
              onClick={() => setTab(t.id)}
              className={[
                'flex flex-1 flex-col items-center gap-px py-2.5 px-1',
                'border-t-2 bg-transparent cursor-pointer',
                'font-display text-[11px] font-extrabold',
                toneClass,
              ].join(' ')}
            >
              <span aria-hidden="true" className="text-base">
                {t.icon}
              </span>
              {t.label}
            </button>
          );
        })}
      </nav>
    </>
  );
}
