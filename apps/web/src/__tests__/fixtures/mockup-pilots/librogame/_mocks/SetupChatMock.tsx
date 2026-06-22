/**
 * Template K presentational mock for SetupChat panel states.
 *
 * No real standalone component exists for the setup-chat panel as of DS-17
 * Phase D-2 (2026-06-22). The chat panel lives inside `GamebookPlayShell`
 * via `useChatPanelStore` (Zustand) and is not exposed independently. This
 * mock renders the full chat UI from the mockup spec, driven by a single
 * `state` prop.
 *
 * Four states map to the four mockup sections (Stati 1–4):
 *   default          — 4-player setup query + 5-step actionable reply, high confidence (0.92)
 *   low-confidence   — 5-player variant query + disclaimer, confidence bassa (0.42)
 *   out-of-context   — query for a different game + decline + switch-game actions
 *   loading          — typing indicator while agent retrieves from KB
 *
 * Replace this mock with a real chat panel component when it is extracted
 * from GamebookPlayShell. The real component will need:
 *   - useChatPanelStore (Zustand) for conversation state
 *   - SSE streaming from the KB agent endpoint
 *   - confidence badge from the retrieval score
 *   - citation chips linked to PDF page references
 *
 * Uses canonical design tokens only (no hardcoded neutral utilities; the
 * `local/no-hardcoded-color-utility` ESLint rule applies).
 *
 * BGG guard: no BGG refs in this file (#2123). Internal catalog framing only.
 *
 * @mockup admin-mockups/design_files/librogame-runthrough-setup-chat.html
 *   - Stato 1 · default          (setup 4 giocatori, confidence alta 0.92)
 *   - Stato 2 · confidence-bassa (variante 5 giocatori, confidence bassa 0.42)
 *   - Stato 3 · out-of-context   (domanda su altro gioco, decline + switch actions)
 *   - Stato 4 · loading          (typing indicator, 1.8s elapsed, budget 8s)
 *
 * Refs: DS-17 Phase D-2, umbrella #2063, sub-issue #2174.
 */

import type { ReactElement, ReactNode } from 'react';

// ---------------------------------------------------------------------------
// State union type
// ---------------------------------------------------------------------------

export type SetupChatMockState = 'default' | 'low-confidence' | 'out-of-context' | 'loading';

// ---------------------------------------------------------------------------
// Props
// ---------------------------------------------------------------------------

export interface SetupChatMockProps {
  /** Which mockup state to render. Drives the chat panel content. */
  readonly state?: SetupChatMockState;
  /** Game title shown in the session header. */
  readonly gameTitle?: string;
  /** Session name shown in the session header. */
  readonly sessionName?: string;
  /** Callback for the send button (no-op in static snapshot). */
  readonly onSend?: () => void;
}

// ---------------------------------------------------------------------------
// Sub-components (inline, render-only mock)
// ---------------------------------------------------------------------------

/** Sticky top bar with session info */
function SessionHeader({
  sessionName,
  gameTitle,
}: {
  sessionName: string;
  gameTitle: string;
}): ReactElement {
  return (
    <div className="flex shrink-0 items-center gap-3 border-b border-border px-4 py-2">
      <span
        className="h-2.5 w-2.5 shrink-0 rounded-full"
        aria-hidden="true"
        style={{
          background: 'hsl(var(--c-session))',
          boxShadow: '0 0 0 3px hsl(var(--c-session) / 0.2)',
        }}
      />
      <div className="min-w-0 flex-1">
        <div className="truncate font-quicksand text-sm font-bold text-foreground">
          {sessionName}
        </div>
        <div className="font-mono text-[10px] text-muted-foreground">
          🎲 {gameTitle} · Setup pre-sessione
        </div>
      </div>
    </div>
  );
}

/** Tab strip with Chat active */
function TabStrip(): ReactElement {
  return (
    <nav
      className="flex shrink-0 gap-0.5 overflow-x-auto border-b border-border px-3"
      role="tablist"
    >
      {(['💬 Chat', '📖 Storybook', '🧰 Toolbox'] as const).map((label, i) => (
        <button
          key={label}
          type="button"
          role="tab"
          aria-selected={i === 0}
          className="relative shrink-0 px-4 py-3 font-quicksand text-sm font-semibold"
          style={{
            color: i === 0 ? 'hsl(var(--foreground))' : 'hsl(var(--muted-foreground))',
            borderBottom: i === 0 ? '2px solid hsl(var(--c-chat))' : '2px solid transparent',
          }}
        >
          {label}
        </button>
      ))}
    </nav>
  );
}

/** User bubble */
function UserBubble({ children }: { children: ReactNode }): ReactElement {
  return (
    <div
      className="max-w-[85%] self-end rounded-2xl rounded-br-sm px-4 py-3 text-sm leading-relaxed text-white"
      style={{ background: 'hsl(var(--c-chat))' }}
    >
      {children}
    </div>
  );
}

/** Agent bubble wrapper */
function AgentBubble({ children }: { children: ReactNode }): ReactElement {
  return (
    <div
      className="max-w-[90%] self-start rounded-2xl rounded-bl-sm border px-4 py-3 text-sm leading-relaxed text-foreground"
      style={{
        background: 'hsl(var(--c-agent) / 0.08)',
        borderColor: 'hsl(var(--c-agent) / 0.18)',
      }}
    >
      <div className="mb-2 flex items-center gap-2">
        <span
          className="flex h-6 w-6 shrink-0 items-center justify-center rounded-full text-xs"
          aria-hidden="true"
          style={{ background: 'hsl(var(--c-agent) / 0.25)' }}
        >
          🤖
        </span>
        <span className="font-quicksand text-xs font-bold" style={{ color: 'hsl(var(--c-agent))' }}>
          Game Tutor
        </span>
      </div>
      {children}
    </div>
  );
}

/** Citation chip row */
function CitationRow({ chips }: { chips: string[] }): ReactElement {
  return (
    <div
      className="mt-3 flex flex-wrap gap-2 border-t border-dashed pt-3"
      style={{ borderColor: 'hsl(var(--c-agent) / 0.25)' }}
      aria-label="Pagine sorgente"
    >
      {chips.map(chip => (
        <span
          key={chip}
          className="inline-flex cursor-pointer items-center gap-1 rounded-full border border-transparent px-3 py-1 font-mono text-[10px] font-semibold"
          style={{
            background: 'hsl(var(--c-kb) / 0.12)',
            color: 'hsl(var(--c-kb))',
          }}
        >
          {chip}
        </span>
      ))}
    </div>
  );
}

type ConfidenceLevel = 'alta' | 'media' | 'bassa';

/** Confidence badge */
function ConfBadge({ level, label }: { level: ConfidenceLevel; label: string }): ReactElement {
  const colorVar =
    level === 'alta' ? '--c-success' : level === 'media' ? '--c-warning' : '--c-danger';
  return (
    <span
      className="inline-flex items-center gap-1 rounded-full px-2 py-0.5 font-mono text-[10px] font-bold uppercase tracking-wider"
      style={{
        background: `hsl(var(${colorVar}) / 0.15)`,
        color: `hsl(var(${colorVar}))`,
      }}
    >
      {label}
    </span>
  );
}

/** Mini action buttons row */
function ActionsMini({ actions }: { actions: string[] }): ReactElement {
  return (
    <div className="flex gap-1">
      {actions.map(a => (
        <button
          key={a}
          type="button"
          className="rounded-full border border-border bg-transparent px-2 py-0.5 font-mono text-[10px] text-muted-foreground"
        >
          {a}
        </button>
      ))}
    </div>
  );
}

/** Low-confidence disclaimer block */
function Disclaimer({ children }: { children: ReactNode }): ReactElement {
  return (
    <div
      className="mt-3 rounded-sm border-l-[3px] p-3 text-xs text-foreground"
      style={{
        background: 'hsl(var(--c-warning) / 0.08)',
        borderColor: 'hsl(var(--c-warning))',
      }}
    >
      {children}
    </div>
  );
}

/** Out-of-context action pill */
function ActionPill({ children }: { children: ReactNode }): ReactElement {
  return (
    <button
      type="button"
      className="flex cursor-pointer items-center gap-2 rounded-full border border-border bg-card px-3 py-2 text-left font-quicksand text-sm font-semibold text-foreground hover:border-[hsl(var(--c-chat)/0.4)] hover:bg-muted"
    >
      {children}
    </button>
  );
}

/** Typing indicator bubble */
function TypingBubble({ meta }: { meta: string }): ReactElement {
  return (
    <AgentBubble>
      <div className="flex gap-1 px-1 py-2" aria-hidden="true">
        {[0, 1, 2].map(i => (
          <span
            key={i}
            className="h-2 w-2 rounded-full"
            style={{
              background: 'hsl(var(--c-agent))',
              opacity: 0.4,
            }}
          />
        ))}
      </div>
      <div className="mt-1 font-mono text-[10px] text-muted-foreground">{meta}</div>
    </AgentBubble>
  );
}

/** Suggested prompt chips */
function SuggestedRow({ prompts }: { prompts: string[] }): ReactElement {
  return (
    <div className="flex shrink-0 flex-wrap gap-2 px-4 pb-3">
      {prompts.map(p => (
        <button
          key={p}
          type="button"
          className="rounded-full border px-3 py-1.5 font-quicksand text-xs font-semibold"
          style={{
            background: 'hsl(var(--c-chat) / 0.08)',
            color: 'hsl(var(--c-chat))',
            borderColor: 'hsl(var(--c-chat) / 0.2)',
          }}
        >
          {p}
        </button>
      ))}
    </div>
  );
}

/** Input bar at the bottom */
function InputBar({
  value,
  disabled,
  onSend,
}: {
  value?: string;
  disabled?: boolean;
  onSend?: () => void;
}): ReactElement {
  return (
    <div className="flex shrink-0 items-center gap-2 border-t border-border px-4 py-3">
      <input
        type="text"
        defaultValue={value}
        disabled={disabled}
        placeholder="Chiedi una regola o setup…"
        aria-label="Domanda all'agente"
        className="flex-1 rounded-full border border-border bg-muted px-4 py-2.5 font-sans text-sm text-foreground disabled:opacity-60"
      />
      <button
        type="button"
        onClick={onSend}
        disabled={disabled}
        aria-label={disabled ? 'In corso' : 'Invia'}
        className="flex h-11 w-11 shrink-0 items-center justify-center rounded-full text-lg text-white disabled:opacity-50"
        style={{ background: disabled ? 'hsl(var(--muted-foreground))' : 'hsl(var(--c-chat))' }}
      >
        {disabled ? '⏳' : '↑'}
      </button>
    </div>
  );
}

// ---------------------------------------------------------------------------
// Conversation content per state
// ---------------------------------------------------------------------------

function DefaultConversation(): ReactElement {
  return (
    <>
      <UserBubble>come si setupa Eldoria per 4 giocatori?</UserBubble>
      <AgentBubble>
        <p className="mb-2 text-sm">Per una partita a 4 giocatori, segui questi step:</p>
        <ol className="mb-2 space-y-2 pl-5 text-sm">
          <li>
            <strong>Tabellone Avalon</strong> al centro del tavolo, lato A in alto.
          </li>
          <li>
            Distribuisci <strong>4 carte personaggio</strong> casuali — uno per giocatore.
          </li>
          <li>
            Posiziona <strong>5 dadi neri</strong> sulla pista combattimento e{' '}
            <strong>3 dadi bianchi</strong> sulla pista esplorazione.
          </li>
          <li>
            Mescola il mazzo <strong>Encounter</strong> (24 carte) e mettilo coperto vicino al
            tabellone.
          </li>
          <li>
            Il giocatore con la carta <strong>&#34;Iniziativa&#34;</strong> apre la prima fase.
          </li>
        </ol>
        <CitationRow chips={['📖 pag. 4 — Setup base', '📖 pag. 7 — Iniziativa']} />
        <div className="mt-2 flex items-center justify-between gap-2">
          <ConfBadge level="alta" label="🟢 Confidence alta · 0.92" />
          <ActionsMini actions={['👍', '📋', '🔁']} />
        </div>
      </AgentBubble>
    </>
  );
}

function LowConfidenceConversation(): ReactElement {
  return (
    <>
      <UserBubble>e per 5 giocatori?</UserBubble>
      <AgentBubble>
        <p className="mb-2 text-sm">
          Non sono sicuro — il quickstart Press Start copre solo configurazioni 2-4 giocatori. Per
          varianti 5+ ti suggerisco di:
        </p>
        <Disclaimer>
          <strong style={{ color: 'hsl(var(--c-warning))' }}>⚠️ Non sono certo.</strong> Controlla
          pag. 18 del regolamento completo (Rules KB) per la variante &#34;Avventura epica&#34; che
          potrebbe estendere a 5-6 giocatori.
        </Disclaimer>
        <CitationRow chips={['📖 Rules pag. 18 (suggerito)']} />
        <div className="mt-2 flex items-center justify-between gap-2">
          <ConfBadge level="bassa" label="🔴 Confidence bassa · 0.42" />
          <ActionsMini actions={['📖 Rules KB', '🔁']} />
        </div>
      </AgentBubble>
    </>
  );
}

function OutOfContextConversation(): ReactElement {
  return (
    <>
      <UserBubble>qual è il setup ottimale di Tainted Grail?</UserBubble>
      <AgentBubble>
        <p className="mb-3 text-sm">
          <strong>Non ho informazioni su Tainted Grail nel mio knowledge base.</strong> Il mio scope
          è il gioco Eldoria (Press Start + Rules). Cosa vuoi fare?
        </p>
        <div className="mt-2 flex flex-col gap-2">
          <ActionPill>🎲 Cambia gioco a Tainted Grail</ActionPill>
          <ActionPill>🤖 Cerca un agente Tainted Grail</ActionPill>
          <ActionPill>↩️ Resta su Eldoria</ActionPill>
        </div>
        <div className="mt-3 flex items-center justify-between gap-2">
          <ConfBadge level="bassa" label="🔴 Out of scope · 0.0" />
          <ActionsMini actions={['👍 Honest']} />
        </div>
      </AgentBubble>
    </>
  );
}

function LoadingConversation(): ReactElement {
  return (
    <>
      <UserBubble>come si setupa Eldoria per 4 giocatori?</UserBubble>
      <TypingBubble meta="⏱️ Cerco in 24 pag · 1.8s elapsed (budget 8s)" />
    </>
  );
}

// ---------------------------------------------------------------------------
// Main mock component
// ---------------------------------------------------------------------------

export function SetupChatMock({
  state = 'default',
  gameTitle = 'Eldoria',
  sessionName = 'Sera con i ragazzi',
  onSend,
}: SetupChatMockProps): ReactElement {
  const isLoading = state === 'loading';

  const suggestedPrompts = state === 'default' ? ['Componenti necessari?', 'Chi inizia?'] : [];

  return (
    <div
      data-slot="setup-chat-mock"
      data-testid="setup-chat-mock"
      data-theme="light"
      className="flex h-screen flex-col bg-background"
      aria-busy={isLoading}
    >
      <SessionHeader sessionName={sessionName} gameTitle={gameTitle} />
      <TabStrip />

      {/* Conversation log */}
      <div
        role="log"
        aria-live="polite"
        aria-busy={isLoading}
        className="flex flex-1 flex-col gap-4 overflow-y-auto p-4"
      >
        {state === 'default' && <DefaultConversation />}
        {state === 'low-confidence' && <LowConfidenceConversation />}
        {state === 'out-of-context' && <OutOfContextConversation />}
        {state === 'loading' && <LoadingConversation />}
      </div>

      {/* Suggested prompts — only in default state */}
      {suggestedPrompts.length > 0 && <SuggestedRow prompts={suggestedPrompts} />}

      <InputBar
        value={isLoading ? 'come si setupa Eldoria per 4 giocatori?' : undefined}
        disabled={isLoading}
        onSend={onSend}
      />

      {/* forward-refactor notice */}
      <p className="shrink-0 px-4 pb-2 text-[10px] text-muted-foreground">
        <em>
          forward-refactor — SetupChat panel. Richiede useChatPanelStore (Zustand) + SSE streaming
          KB agent + confidence badge from retrieval score.
        </em>
      </p>
    </div>
  );
}
