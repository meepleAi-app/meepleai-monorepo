import type { JSX } from 'react';

import { KBRulesQuickGlance } from '@/components/ui/kb-rules-quick-glance';
import type { KBRule, KBRulesQuickGlanceStatus } from '@/components/ui/kb-rules-quick-glance';
import { SetupChecklist } from '@/components/ui/setup-checklist';
import type { SetupChecklistItem } from '@/components/ui/setup-checklist';

export type TransitionSubmodal = 'skip' | 'end' | null;

export interface TransitionPlayer {
  readonly id: string;
  readonly name: string;
  readonly initials: string;
  readonly color: number;
  readonly score?: number;
  readonly delta?: string;
}

export interface TransitionLastGame {
  readonly id: string;
  readonly title: string;
  readonly publisher?: string;
  readonly emoji?: string;
  readonly cover?: readonly [string, string];
  readonly duration: string;
  readonly endedAt?: string;
  readonly winner: TransitionPlayer & { readonly score: number };
  readonly topThree: ReadonlyArray<TransitionPlayer>;
}

export interface TransitionNextGame {
  readonly id: string;
  readonly title: string;
  readonly publisher?: string;
  readonly emoji?: string;
  readonly cover?: readonly [string, string];
  readonly weight?: number;
  readonly estimated: string;
  readonly rules: ReadonlyArray<KBRule>;
  readonly setup: ReadonlyArray<SetupChecklistItem>;
}

export interface GameTransitionDialogProps {
  readonly open: boolean;
  readonly lastGame: TransitionLastGame;
  readonly nextGame: TransitionNextGame;
  readonly rulesStatus?: KBRulesQuickGlanceStatus;
  readonly submodal?: TransitionSubmodal;
  readonly mobile?: boolean;
  readonly nightCode?: string;
  readonly onClose?: () => void;
  readonly onPlayNext?: () => void;
  readonly onOpenSubmodal?: (which: 'skip' | 'end') => void;
  readonly onConfirmSubmodal?: () => void;
  readonly onCancelSubmodal?: () => void;
  readonly onRetryRules?: () => void;
  readonly onProceedWithoutRules?: () => void;
  readonly className?: string;
}

const DIALOG_TITLE_ID = 'game-transition-dialog-title';

export function GameTransitionDialog({
  open,
  lastGame,
  nextGame,
  rulesStatus = 'ok',
  submodal = null,
  mobile = false,
  nightCode,
  onClose,
  onPlayNext,
  onOpenSubmodal,
  onConfirmSubmodal,
  onCancelSubmodal,
  onRetryRules,
  onProceedWithoutRules,
  className,
}: GameTransitionDialogProps): JSX.Element | null {
  if (!open) return null;

  return (
    <div
      role="dialog"
      aria-modal="true"
      aria-labelledby={DIALOG_TITLE_ID}
      data-mobile={mobile ? 'true' : 'false'}
      className={[
        'relative flex flex-col overflow-hidden bg-card',
        mobile
          ? 'w-full h-full'
          : 'w-[600px] h-[500px] max-h-full rounded-xl border border-border shadow-lg',
        className ?? '',
      ]
        .filter(Boolean)
        .join(' ')}
    >
      <DialogHeader mobile={mobile} nightCode={nightCode} onClose={onClose} />

      <div
        className={[
          'flex-1 min-h-0 overflow-y-auto',
          mobile ? 'flex flex-col' : 'grid grid-cols-2',
        ].join(' ')}
      >
        <RecapPanel game={lastGame} mobile={mobile} />
        <PreviewPanel
          game={nextGame}
          rulesStatus={rulesStatus}
          mobile={mobile}
          onRetryRules={onRetryRules}
          onProceedWithoutRules={onProceedWithoutRules}
        />
      </div>

      <DialogFooter
        mobile={mobile}
        onPlayNext={onPlayNext}
        onSkipClick={onOpenSubmodal ? () => onOpenSubmodal('skip') : undefined}
        onEndClick={onOpenSubmodal ? () => onOpenSubmodal('end') : undefined}
      />

      {submodal === 'skip' ? (
        <ConfirmSubmodal
          tone="warning"
          icon="⏭"
          title={`Saltare ${nextGame.title}?`}
          body="Verrà rimosso dai planned games di questa serata. Resterà in libreria ma il diary non avrà entries per questa session."
          primaryLabel="Salta e prossimo"
          primaryIcon="⏭"
          onConfirm={onConfirmSubmodal}
          onCancel={onCancelSubmodal}
        />
      ) : null}
      {submodal === 'end' ? (
        <ConfirmSubmodal
          tone="danger"
          icon="🛑"
          title="Terminare la serata qui?"
          body="Andrai al Summary con i game registrati. Gli upcoming verranno archiviati come 'non giocati'. Il diary sarà finalizzato."
          primaryLabel="Termina · vai a Summary"
          primaryIcon="🛑"
          onConfirm={onConfirmSubmodal}
          onCancel={onCancelSubmodal}
        />
      ) : null}
    </div>
  );
}

interface DialogHeaderProps {
  readonly mobile: boolean;
  readonly nightCode?: string;
  readonly onClose?: () => void;
}

function DialogHeader({ mobile, nightCode, onClose }: DialogHeaderProps): JSX.Element {
  return (
    <div
      className={[
        'flex items-center gap-2.5 border-b border-border',
        mobile ? 'px-4 py-3.5' : 'px-4 py-3.5',
      ].join(' ')}
    >
      <div
        aria-hidden="true"
        className="flex h-8 w-8 shrink-0 items-center justify-center rounded-md text-base"
        style={{
          background: 'linear-gradient(135deg, hsl(var(--c-event)), hsl(var(--c-session)))',
          color: '#fff',
        }}
      >
        ➡️
      </div>
      <div className="min-w-0 flex-1">
        {nightCode ? (
          <span className="block font-mono text-[9.5px] font-extrabold uppercase tracking-wider text-entity-event">
            Game transition · {nightCode}
          </span>
        ) : (
          <span className="block font-mono text-[9.5px] font-extrabold uppercase tracking-wider text-entity-event">
            Game transition
          </span>
        )}
        <h2
          id={DIALOG_TITLE_ID}
          className="m-0 font-display text-[14.5px] font-extrabold tracking-tight text-foreground"
        >
          Pronti per il prossimo gioco?
        </h2>
      </div>
      {onClose ? (
        <button
          type="button"
          aria-label="Chiudi"
          onClick={onClose}
          className="h-8 w-8 shrink-0 rounded-md border border-border bg-card text-base font-bold text-muted-foreground cursor-pointer"
        >
          ×
        </button>
      ) : null}
    </div>
  );
}

interface DialogFooterProps {
  readonly mobile: boolean;
  readonly onPlayNext?: () => void;
  readonly onSkipClick?: () => void;
  readonly onEndClick?: () => void;
}

function DialogFooter({
  mobile,
  onPlayNext,
  onSkipClick,
  onEndClick,
}: DialogFooterProps): JSX.Element {
  return (
    <div
      className={[
        'flex shrink-0 items-center gap-2 border-t border-border bg-muted',
        mobile ? 'flex-wrap px-4 py-3' : 'px-4 py-3',
      ].join(' ')}
    >
      <button
        type="button"
        onClick={onSkipClick}
        className="inline-flex items-center gap-1.5 rounded-md border border-[hsl(var(--c-warning)/0.4)] bg-transparent px-3 py-2 font-display text-[12.5px] font-extrabold text-[hsl(var(--c-warning))] cursor-pointer"
      >
        <span aria-hidden="true">⏭</span>
        Salta gioco
      </button>
      <button
        type="button"
        onClick={onEndClick}
        className="inline-flex items-center gap-1.5 rounded-md border border-[hsl(var(--c-danger)/0.4)] bg-transparent px-3 py-2 font-display text-[12.5px] font-extrabold text-[hsl(var(--c-danger))] cursor-pointer"
      >
        <span aria-hidden="true">🛑</span>
        Termina serata qui
      </button>
      <div className="flex-1" />
      <button
        type="button"
        onClick={onPlayNext}
        className="inline-flex shrink-0 items-center gap-1.5 rounded-md border-0 px-4 py-2.5 font-display text-[13.5px] font-extrabold cursor-pointer"
        style={{
          background: 'linear-gradient(135deg, hsl(var(--c-session)), hsl(var(--c-chat)))',
          color: '#fff',
          boxShadow: '0 6px 18px hsl(var(--c-session) / 0.4)',
        }}
      >
        ▶ Avvia prossima session
        <span aria-hidden="true">→</span>
      </button>
    </div>
  );
}

interface RecapPanelProps {
  readonly game: TransitionLastGame;
  readonly mobile: boolean;
}

function RecapPanel({ game, mobile }: RecapPanelProps): JSX.Element {
  return (
    <section
      aria-label="Ultimo gioco"
      className={[
        'flex min-w-0 flex-col gap-3 bg-entity-event/[0.04]',
        mobile ? 'border-b border-border' : 'border-r border-border',
        mobile ? 'px-4 py-3.5' : 'px-4 py-4',
      ].join(' ')}
    >
      <div className="flex items-center gap-2">
        <span aria-hidden="true" className="text-sm">
          ⏪
        </span>
        <span className="font-mono text-[10px] font-extrabold uppercase tracking-wider text-entity-event">
          Ultimo gioco
        </span>
      </div>

      <div className="flex items-center gap-3">
        <span
          aria-hidden="true"
          className="flex h-14 w-14 shrink-0 items-center justify-center rounded-md text-[26px] shadow-sm"
          style={
            game.cover
              ? { background: `linear-gradient(135deg, ${game.cover[0]}, ${game.cover[1]})` }
              : { background: 'var(--bg-muted)' }
          }
        >
          {game.emoji ?? '🎲'}
        </span>
        <div className="min-w-0 flex-1">
          <div className="truncate font-display text-base font-extrabold leading-tight text-foreground">
            {game.title}
          </div>
          {(game.publisher ?? game.endedAt) ? (
            <div className="mt-0.5 font-mono text-[10px] font-bold text-muted-foreground">
              {game.publisher}
              {game.publisher && game.endedAt ? ' · finito ' : ''}
              {!game.publisher && game.endedAt ? 'finito ' : ''}
              {game.endedAt}
            </div>
          ) : null}
        </div>
      </div>

      {/* Winner banner */}
      <div className="flex items-center gap-2.5 rounded-md border border-entity-event/30 bg-entity-event/10 px-3 py-2.5 ring-[3px] ring-entity-event/[0.06]">
        <span aria-hidden="true" className="text-[22px]">
          🏆
        </span>
        <div className="min-w-0 flex-1">
          <span className="block font-mono text-[9px] font-extrabold uppercase tracking-wider text-entity-event">
            Winner · {game.duration}
          </span>
          <div className="mt-px font-display text-[14px] font-extrabold text-foreground">
            {game.winner.name}{' '}
            <span className="tabular-nums text-entity-event">{game.winner.score}pt</span>
          </div>
        </div>
        <span
          aria-label={`Winner avatar ${game.winner.initials}`}
          className="inline-flex h-8 w-8 shrink-0 items-center justify-center rounded-full border-2 font-display text-[11px] font-extrabold"
          style={{
            background: `hsl(${game.winner.color}, 60%, 55%)`,
            color: '#fff',
            borderColor: 'hsl(var(--c-event) / 0.4)' as string,
          }}
        >
          {game.winner.initials}
        </span>
      </div>

      {/* Top-3 score list */}
      {game.topThree.length > 0 ? (
        <div className="flex flex-col gap-1">
          <span className="font-mono text-[9.5px] font-extrabold uppercase tracking-wider text-muted-foreground">
            Classifica · Top 3
          </span>
          <ul role="list" className="flex flex-col gap-1 m-0 p-0 list-none">
            {game.topThree.slice(0, 3).map((player, i) => {
              const isWinner = i === 0;
              return (
                <li
                  key={player.id}
                  data-rank={i + 1}
                  className={[
                    'flex items-center gap-2.5 rounded-sm px-2.5 py-1.5',
                    isWinner
                      ? 'border border-entity-event/30 bg-entity-event/[0.08]'
                      : 'border border-border bg-card',
                  ].join(' ')}
                >
                  <span
                    aria-label={`Rank ${i + 1}`}
                    className="inline-flex h-[18px] w-[18px] shrink-0 items-center justify-center rounded-full font-display text-[10px] font-extrabold"
                    style={
                      isWinner
                        ? { background: 'hsl(var(--c-event))', color: '#fff' }
                        : { background: 'var(--bg-muted)', color: 'var(--text-sec)' }
                    }
                  >
                    {i + 1}
                  </span>
                  <span
                    aria-label={`Player ${player.initials}`}
                    className="inline-flex h-[22px] w-[22px] shrink-0 items-center justify-center rounded-full font-display text-[9px] font-extrabold"
                    style={{
                      background: `hsl(${player.color}, 60%, 55%)`,
                      color: '#fff',
                    }}
                  >
                    {player.initials}
                  </span>
                  <div className="min-w-0 flex-1">
                    <div className="font-display text-xs font-extrabold text-foreground">
                      {player.name}
                    </div>
                    {player.delta ? (
                      <div className="truncate font-mono text-[9px] font-bold text-muted-foreground">
                        {player.delta}
                      </div>
                    ) : null}
                  </div>
                  {player.score !== undefined ? (
                    <span
                      className={[
                        'font-mono text-sm font-extrabold tabular-nums',
                        isWinner ? 'text-entity-event' : 'text-foreground',
                      ].join(' ')}
                    >
                      {player.score}
                    </span>
                  ) : null}
                </li>
              );
            })}
          </ul>
        </div>
      ) : null}
    </section>
  );
}

interface PreviewPanelProps {
  readonly game: TransitionNextGame;
  readonly rulesStatus: KBRulesQuickGlanceStatus;
  readonly mobile: boolean;
  readonly onRetryRules?: () => void;
  readonly onProceedWithoutRules?: () => void;
}

function PreviewPanel({
  game,
  rulesStatus,
  mobile,
  onRetryRules,
  onProceedWithoutRules,
}: PreviewPanelProps): JSX.Element {
  return (
    <section
      aria-label="Prossimo gioco"
      className={[
        'flex flex-1 min-w-0 flex-col gap-3 bg-card',
        mobile ? 'px-4 py-3.5' : 'px-4 py-4',
      ].join(' ')}
    >
      <div className="flex items-center gap-2">
        <span aria-hidden="true" className="text-sm">
          ⏩
        </span>
        <span className="font-mono text-[10px] font-extrabold uppercase tracking-wider text-entity-session">
          Prossimo gioco
        </span>
      </div>

      <div className="flex items-center gap-3">
        <span
          aria-hidden="true"
          className="flex h-14 w-14 shrink-0 items-center justify-center rounded-md text-[26px] shadow-sm"
          style={
            game.cover
              ? { background: `linear-gradient(135deg, ${game.cover[0]}, ${game.cover[1]})` }
              : { background: 'var(--bg-muted)' }
          }
        >
          {game.emoji ?? '🎲'}
        </span>
        <div className="min-w-0 flex-1">
          <div className="truncate font-display text-base font-extrabold leading-tight text-foreground">
            {game.title}
          </div>
          {(game.publisher ?? game.weight !== undefined) ? (
            <div className="mt-0.5 font-mono text-[10px] font-bold text-muted-foreground">
              {game.publisher}
              {game.publisher && game.weight !== undefined ? ' · ' : ''}
              {game.weight !== undefined ? `weight ${game.weight.toFixed(2)}` : ''}
            </div>
          ) : null}
        </div>
        <span className="inline-flex shrink-0 items-center gap-1 rounded-pill border border-entity-session/30 bg-entity-session/15 px-2 py-0.5 font-mono text-[11px] font-extrabold tabular-nums text-entity-session">
          <span aria-hidden="true">⏱</span>~{game.estimated}
        </span>
      </div>

      <KBRulesQuickGlance
        status={rulesStatus}
        rules={game.rules}
        onRetry={onRetryRules}
        onProceed={onProceedWithoutRules}
      />

      <SetupChecklist items={game.setup} />
    </section>
  );
}

interface ConfirmSubmodalProps {
  readonly tone: 'warning' | 'danger';
  readonly icon: string;
  readonly title: string;
  readonly body: string;
  readonly primaryLabel: string;
  readonly primaryIcon: string;
  readonly onConfirm?: () => void;
  readonly onCancel?: () => void;
}

function ConfirmSubmodal({
  tone,
  icon,
  title,
  body,
  primaryLabel,
  primaryIcon,
  onConfirm,
  onCancel,
}: ConfirmSubmodalProps): JSX.Element {
  const toneClass =
    tone === 'danger'
      ? {
          fg: 'hsl(var(--c-danger))',
          bgIcon: 'hsl(var(--c-danger) / 0.12)',
          border: 'hsl(var(--c-danger) / 0.32)',
          shadow: '0 4px 14px hsl(var(--c-danger) / 0.35)',
        }
      : {
          fg: 'hsl(var(--c-warning))',
          bgIcon: 'hsl(var(--c-warning) / 0.12)',
          border: 'hsl(var(--c-warning) / 0.32)',
          shadow: '0 4px 14px hsl(var(--c-warning) / 0.35)',
        };

  return (
    <div
      className="absolute inset-0 z-[60] flex items-center justify-center p-4 backdrop-blur-md"
      style={{ background: 'rgba(0,0,0,0.45)' }}
    >
      <div
        role="alertdialog"
        aria-modal="true"
        aria-labelledby="transition-confirm-title"
        className="flex w-full max-w-[380px] flex-col gap-3 rounded-xl bg-card shadow-lg p-4"
        style={{ borderColor: toneClass.border, borderWidth: 1, borderStyle: 'solid' }}
      >
        <div
          aria-hidden="true"
          className="flex h-11 w-11 shrink-0 items-center justify-center rounded-full text-[22px]"
          style={{ background: toneClass.bgIcon, color: toneClass.fg }}
        >
          {icon}
        </div>
        <div>
          <h3
            id="transition-confirm-title"
            className="m-0 font-display text-base font-extrabold tracking-tight text-foreground"
          >
            {title}
          </h3>
          <p className="mt-1.5 m-0 text-[12.5px] leading-relaxed text-muted-foreground">{body}</p>
        </div>
        <div className="flex gap-2">
          <button
            type="button"
            onClick={onConfirm}
            className="flex-[2] inline-flex items-center justify-center gap-1.5 rounded-md border-0 px-2.5 py-2.5 font-display text-[13px] font-extrabold cursor-pointer"
            style={{ background: toneClass.fg, color: '#fff', boxShadow: toneClass.shadow }}
          >
            <span aria-hidden="true">{primaryIcon}</span>
            {primaryLabel}
          </button>
          <button
            type="button"
            onClick={onCancel}
            className="flex-1 rounded-md border border-border-strong bg-transparent px-2.5 py-2.5 font-display text-[13px] font-bold text-muted-foreground cursor-pointer"
          >
            Annulla
          </button>
        </div>
      </div>
    </div>
  );
}
