/* eslint-disable local/no-hardcoded-color-utility -- text-white / button color on style-prop colored bg or entity-colored CTA; mockup .e-bg pattern. DS-12 will introduce primitives encoding bg via className. */
'use client';

/**
 * LibroGameDetailView — full storyboard-compliant detail page for libro games.
 *
 * Replaces the legacy `GameDetailDesktop` shell on /library/[gameId] when the
 * game qualifies as a libro game. Renders 1:1 the layout from
 * admin-mockups/design_files/nanolith-runthrough-game-detail.html (state-01).
 *
 * Scope (Iter B1): hero card with session+game gradient cover, nano-mark badge,
 * 4-cell meta grid, connection pip bar with real ManaPips, CTA primary, tabs
 * Info/AI Chat/Toolbox/Toolkit (B2).
 *
 * Theme: forces `data-theme="light"` on the root so the warm cream palette
 * from the storyboard is preserved regardless of the user's global preference.
 * The libro-game flow is opinionated about its surface.
 */

import { useState, type ReactElement } from 'react';

import { useRouter } from 'next/navigation';

import { NanolithCampaignCTA } from '@/components/features/gamebook/NanolithCampaignCTA';
import type { LibraryGameDetail } from '@/hooks/queries/useLibrary';

// ─── Types ──────────────────────────────────────────────────────────────────

type TabId = 'info' | 'chat' | 'toolbox' | 'toolkit';

export interface LibroGameDetailViewProps {
  readonly gameDetail: LibraryGameDetail;
}

// ─── Component ──────────────────────────────────────────────────────────────

export function LibroGameDetailView({ gameDetail }: LibroGameDetailViewProps): ReactElement {
  const router = useRouter();
  const [tab, setTab] = useState<TabId>('info');

  const playersLabel =
    gameDetail.minPlayers && gameDetail.maxPlayers
      ? gameDetail.minPlayers === gameDetail.maxPlayers
        ? `${gameDetail.minPlayers}`
        : `${gameDetail.minPlayers}–${gameDetail.maxPlayers}`
      : '—';
  const playTimeLabel = gameDetail.playingTimeMinutes
    ? formatPlayTime(gameDetail.playingTimeMinutes)
    : '—';
  const rating = gameDetail.averageRating?.toFixed(1) ?? '—';
  const yearLabel = gameDetail.gameYearPublished ? String(gameDetail.gameYearPublished) : '—';

  return (
    <div
      data-theme="light"
      data-slot="libro-game-detail"
      className="libro-game-detail min-h-screen bg-[#f7f3ee] text-[#2b1f12]"
    >
      {/* App bar — back + breadcrumb */}
      <header className="flex items-center gap-3 border-b border-[rgba(180,130,80,0.1)] px-4 py-3">
        <button
          type="button"
          onClick={() => router.push('/library')}
          aria-label="Torna alla libreria"
          className="flex h-9 w-9 items-center justify-center rounded-[10px] bg-[#efe6d9] text-lg hover:bg-[rgba(180,130,80,0.12)]"
        >
          ←
        </button>
        <span className="font-mono text-[11px] text-[#9a8870]">
          <strong className="font-semibold text-[#5a4a38]">Libreria</strong> ·{' '}
          {gameDetail.gameTitle}
        </span>
      </header>

      <main className="mx-auto max-w-4xl px-4 py-4">
        {/* Hero card */}
        <article className="overflow-hidden rounded-[18px] border border-[rgba(180,130,80,0.1)] bg-card shadow-[0_2px_8px_rgba(90,60,20,0.08)]">
          {/* Cover */}
          <div
            className="relative flex items-end p-5 text-white"
            style={{
              aspectRatio: '12 / 4',
              background:
                'radial-gradient(circle at 30% 30%, hsl(var(--c-session) / 0.55), transparent 55%), ' +
                'radial-gradient(circle at 75% 70%, hsl(var(--c-game) / 0.45), transparent 60%), ' +
                'linear-gradient(135deg, #1a1530, #0d0a1a)',
            }}
          >
            <span
              className="absolute right-4 top-4 rounded-[6px] bg-card/20 px-2 py-0.5 font-mono text-[11px] text-white backdrop-blur"
              data-slot="nano-mark"
            >
              In collezione
              {gameDetail.bggId != null && ` · BGG #${gameDetail.bggId}`}
            </span>
            <div>
              <h1 className="font-quicksand text-[32px] font-bold leading-[1.05]">
                {gameDetail.gameTitle}
              </h1>
              {gameDetail.gamePublisher && (
                <span className="mt-0.5 block font-mono text-sm opacity-85">
                  {gameDetail.gamePublisher}
                  {gameDetail.gameYearPublished && ` · ${gameDetail.gameYearPublished}`}
                </span>
              )}
            </div>
          </div>

          {/* Meta grid */}
          <div className="grid grid-cols-4 gap-2 p-4">
            <MetaStat value={playersLabel} label="giocatori" />
            <MetaStat value={playTimeLabel} label="durata" />
            <MetaStat value={rating} label="BGG" />
            <MetaStat value={yearLabel} label="anno" />
          </div>

          {/* Connection pip bar */}
          <div className="flex flex-wrap gap-2 px-4 pb-4">
            <Pip kind="kb" count={gameDetail.hasRagAccess ? 1 : 0} label="KB" emoji="📄" />
            <Pip kind="chat" count={0} label="chat" emoji="💬" />
            <Pip kind="agent" count={gameDetail.hasRagAccess ? 1 : 0} label="Tutor" emoji="🤖" />
            <Pip kind="player" count={0} label="giocatori" emoji="👤" />
            <Pip kind="session" count={gameDetail.timesPlayed} label="partite" emoji="🎯" />
          </div>
        </article>

        {/* Primary CTA */}
        <div className="px-0 pt-4">
          <NanolithCampaignCTA gameId={gameDetail.gameId} gameTitle={gameDetail.gameTitle} />
        </div>

        {/* Tabs */}
        <nav
          role="tablist"
          aria-label="Sezioni del gioco"
          className="mt-4 flex gap-1 overflow-x-auto border-b border-[rgba(180,130,80,0.1)]"
        >
          <TabButton id="info" active={tab === 'info'} onClick={() => setTab('info')}>
            Info
          </TabButton>
          <TabButton id="chat" active={tab === 'chat'} onClick={() => setTab('chat')}>
            AI Chat
          </TabButton>
          <TabButton id="toolbox" active={tab === 'toolbox'} onClick={() => setTab('toolbox')}>
            Toolbox
          </TabButton>
          <TabButton id="toolkit" active={tab === 'toolkit'} onClick={() => setTab('toolkit')}>
            Toolkit
          </TabButton>
        </nav>

        <section
          id={`panel-${tab}`}
          role="tabpanel"
          aria-labelledby={`tab-${tab}`}
          className="py-4"
        >
          {tab === 'info' && <InfoPanel detail={gameDetail} />}
          {tab === 'chat' && <PlaceholderPanel label="AI Chat" />}
          {tab === 'toolbox' && <PlaceholderPanel label="Toolbox" />}
          {tab === 'toolkit' && <PlaceholderPanel label="Toolkit" />}
        </section>
      </main>
    </div>
  );
}

// ─── Subcomponents ──────────────────────────────────────────────────────────

function MetaStat({ value, label }: { value: string; label: string }): ReactElement {
  return (
    <div className="flex flex-col">
      <span className="font-mono text-[15px] font-bold text-[#2b1f12] tabular-nums">{value}</span>
      <span className="mt-0.5 text-[11px] text-[#9a8870]">{label}</span>
    </div>
  );
}

function Pip({
  kind,
  count,
  label,
  emoji,
}: {
  kind: 'kb' | 'chat' | 'agent' | 'player' | 'session';
  count: number;
  label: string;
  emoji: string;
}): ReactElement {
  const empty = count === 0;
  return (
    <span
      className={
        empty
          ? 'inline-flex items-center gap-1 rounded-full border border-dashed border-[rgba(180,130,80,0.32)] px-3 py-1 font-mono text-[11px] font-semibold text-[#9a8870] opacity-70'
          : 'inline-flex items-center gap-1 rounded-full px-3 py-1 font-mono text-[11px] font-semibold tabular-nums'
      }
      style={
        !empty
          ? {
              background: `hsl(var(--c-${kind}) / 0.12)`,
              color: `hsl(var(--c-${kind}))`,
            }
          : undefined
      }
      aria-label={`${label} ${count}`}
    >
      <span aria-hidden="true">{emoji}</span>
      {count > 0 ? count : label}
    </span>
  );
}

function TabButton({
  id,
  active,
  onClick,
  children,
}: {
  id: string;
  active: boolean;
  onClick: () => void;
  children: React.ReactNode;
}): ReactElement {
  return (
    <button
      type="button"
      role="tab"
      id={`tab-${id}`}
      aria-selected={active}
      aria-controls={`panel-${id}`}
      onClick={onClick}
      className={
        active
          ? 'relative shrink-0 bg-transparent px-4 py-3 font-quicksand text-[15px] font-semibold text-[#2b1f12] after:absolute after:inset-x-3 after:-bottom-px after:h-[2px] after:rounded-full after:bg-[hsl(var(--c-game))]'
          : 'shrink-0 bg-transparent px-4 py-3 font-quicksand text-[15px] font-semibold text-[#5a4a38] hover:text-[#2b1f12]'
      }
    >
      {children}
    </button>
  );
}

function InfoPanel({ detail }: { detail: LibraryGameDetail }): ReactElement {
  return (
    <div className="grid gap-4">
      <div>
        <h3 className="mb-2 font-quicksand text-[15px] font-bold uppercase tracking-wide text-[#9a8870]">
          Descrizione
        </h3>
        <p className="text-[15px] leading-relaxed text-[#5a4a38]">
          {detail.description ??
            `${detail.gameTitle} — campagna libro game. Avvia la modalità per setup tutorial, Q&A regole e traduzione storybook.`}
        </p>
      </div>

      {/* KB status row */}
      <div>
        <h3 className="mb-2 font-quicksand text-[15px] font-bold uppercase tracking-wide text-[#9a8870]">
          Knowledge base
        </h3>
        <div className="flex items-center gap-3 rounded-[10px] border border-[hsl(var(--c-kb)/0.2)] bg-[hsl(var(--c-kb)/0.08)] p-3">
          <span
            aria-hidden="true"
            className="flex h-9 w-9 items-center justify-center rounded-[10px] bg-[hsl(var(--c-kb)/0.18)] text-lg"
          >
            📄
          </span>
          <div>
            <div className="font-quicksand font-semibold text-[hsl(var(--c-kb))]">
              {detail.hasRagAccess
                ? 'Regole indicizzate · pronte per Q&A'
                : 'KB non ancora indicizzato'}
            </div>
            <div className="mt-0.5 text-[11px] text-[#5a4a38]">
              {detail.hasRagAccess
                ? "L'agente può citare paragrafi durante la sessione"
                : 'Carica il PDF regole per abilitare Q&A'}
            </div>
          </div>
        </div>
      </div>
    </div>
  );
}

function PlaceholderPanel({ label }: { label: string }): ReactElement {
  return (
    <div className="rounded-[14px] border border-dashed border-[rgba(180,130,80,0.32)] bg-card p-6 text-center">
      <p className="text-[15px] text-[#5a4a38]">
        Pannello <strong>{label}</strong> · in arrivo con la prossima iter.
      </p>
    </div>
  );
}

// ─── Helpers ────────────────────────────────────────────────────────────────

function formatPlayTime(minutes: number): string {
  if (minutes < 60) return `${minutes}min`;
  const hours = minutes / 60;
  if (Number.isInteger(hours)) return `${hours}h`;
  const low = Math.floor(hours);
  const high = Math.ceil(hours);
  return `${low}–${high}h`;
}
