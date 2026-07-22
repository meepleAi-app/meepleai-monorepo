'use client';

/**
 * ChatAgentPanel — Issue #2374 G1 (T3).
 * Extended by Issue #2588 A3: forwards optional images param from LiveAgentChat.
 *
 * Wrapper primitive around `LiveAgentChat` that adds the mockup's
 * "magnet" header (emoji avatar + Online pip + agent name + latency +
 * ChatAgent pill) and an accordion-style collapsed semantic.
 *
 * Body delegates to `<LiveAgentChat />` — no duplication of chat logic.
 *
 * §5 frozen contract for #2375 G3:
 *   - data-slot="chat-agent-panel" (E2E selector, never rename)
 *   - data-collapsed reflects the collapsed prop
 *   - header is a <button> (a11y) when onHeaderClick provided, else <div>
 *   - body unmounts when collapsed=true (no hidden focus traps)
 *   - no SSE state inside the primitive (parent owns the stream)
 *   - sessionId is forwarded transparently to LiveAgentChat;
 *     it does NOT change the panel's data-slot/collapsed/header semantics.
 *
 * Token discipline (§3 D-4):
 *   - semantic tokens: bg-card / text-foreground / text-muted-foreground /
 *     border-border
 *   - entity utilities: bg-entity-agent/* / text-entity-agent /
 *     border-entity-agent/*
 *   - NO raw HSL literals.
 *
 * Reference mockup: admin-mockups/design_files/sp4-session-skeleton-parts.jsx L91–135.
 */

import type { ReactElement } from 'react';

import type { ChatImagePreview } from '@/hooks/useChatImageAttachments';
import type { ParticipantRole } from '@/lib/session-live/participant-role';

import { LiveAgentChat } from './LiveAgentChat';

import type { ChatMessage, LiveAgentChatLabels } from './LiveAgentChat';

// ─── Labels ───────────────────────────────────────────────────────────────────

export interface ChatAgentPanelLabels {
  /** Pill text — typically "ChatAgent". */
  readonly title: string;
  /** Pre-resolved (Gate A): aria-label for the agent name span. */
  readonly agentNameAriaLabel: string;
  /** Label for the Online pulse pip — typically "Online". */
  readonly onlineLabel: string;
  /** Pre-resolved (Gate A): aria-label for latency indicator. e.g. "Latenza 42ms". */
  readonly latencyAriaLabel: string;
  /** Labels forwarded to the inner `<LiveAgentChat />`. */
  readonly chatPanelLabels: LiveAgentChatLabels;
}

// ─── Props ────────────────────────────────────────────────────────────────────

export interface ChatAgentPanelProps {
  /** #2375 G3 — forwarded to LiveAgentChat for sessionStorage draft persistence.
   *  Optional: when omitted or null, draft persistence is disabled (no-op). */
  readonly sessionId?: string | null;
  readonly messages: ReadonlyArray<ChatMessage>;
  readonly viewerRole: ParticipantRole;
  readonly viewerId: string;
  /**
   * #2588 A3: extended with optional images param (backward-compatible).
   * Existing callers that pass only (content, visibility) continue to work.
   */
  readonly onSendMessage: (
    content: string,
    visibility: 'private' | 'shared',
    images?: ChatImagePreview[]
  ) => Promise<void> | void;
  readonly agentName: string;
  /** Default '🤖'. */
  readonly agentEmoji?: string;
  readonly latencyMs: number;
  /** Default false; G3 controls. When true, body unmounts. */
  readonly collapsed?: boolean;
  /** G3 wires the accordion FSM. When provided, header becomes a button. */
  readonly onHeaderClick?: () => void;
  readonly compact?: boolean;
  readonly labels: ChatAgentPanelLabels;
  readonly className?: string;
}

// ─── Component ────────────────────────────────────────────────────────────────

export function ChatAgentPanel({
  sessionId,
  messages,
  viewerRole,
  viewerId,
  onSendMessage,
  agentName,
  agentEmoji = '🤖',
  latencyMs,
  collapsed = false,
  onHeaderClick,
  compact = false,
  labels,
  className,
}: ChatAgentPanelProps): ReactElement {
  // `LiveAgentChat.onSendMessage` is sync, but #2375 may attach async sends.
  // We adapt the wider Promise|void prop to a sync wrapper that fires the underlying
  // call (errors are owned by the parent stream). #2588 A3: images forwarded through.
  const handleSendMessage = (
    content: string,
    visibility: 'private' | 'shared',
    images?: ChatImagePreview[]
  ) => {
    void onSendMessage(content, visibility, images);
  };

  // ─── Header content (identical for both <button> and <div> wrappers) ──────
  const headerContent = (
    <>
      {/* Emoji avatar with pulse pip */}
      <span
        aria-hidden="true"
        className="relative flex h-9 w-9 shrink-0 items-center justify-center
          rounded-full bg-entity-agent/20 text-base"
      >
        {agentEmoji}
        {/* Online pulse pip — aria-label exposes "Online" text to AT */}
        <span
          aria-label={labels.onlineLabel}
          role="status"
          className="absolute -bottom-0.5 -right-0.5 inline-block h-2.5 w-2.5
            rounded-full border-2 border-card bg-emerald-500 animate-pulse"
        />
      </span>

      {/* Agent name + latency */}
      <span className="flex min-w-0 flex-1 flex-col gap-0.5 text-left">
        <span
          aria-label={labels.agentNameAriaLabel}
          className="truncate text-sm font-semibold text-foreground"
        >
          {agentName}
        </span>
        <span
          aria-label={labels.latencyAriaLabel}
          className="text-xs font-medium text-foreground/70"
        >
          {latencyMs}ms
        </span>
      </span>

      {/* "ChatAgent" pill — mockup magnet semantic */}
      <span
        className="shrink-0 rounded-full border border-entity-agent/30
          bg-entity-agent/10 px-2 py-0.5 text-xs font-semibold uppercase
          tracking-wider text-entity-agent"
      >
        {labels.title}
      </span>
    </>
  );

  // ─── Header wrapper — <button> when interactive, <div> otherwise ──────────
  const header = onHeaderClick ? (
    <button
      type="button"
      onClick={onHeaderClick}
      aria-expanded={!collapsed}
      className="flex w-full items-center gap-3 border-b border-border/60
        bg-entity-agent/5 px-3 py-2 text-left transition-colors
        hover:bg-entity-agent/10 focus-visible:outline-none
        focus-visible:ring-2 focus-visible:ring-ring"
    >
      {headerContent}
    </button>
  ) : (
    <div
      className="flex w-full items-center gap-3 border-b border-border/60
        bg-entity-agent/5 px-3 py-2"
    >
      {headerContent}
    </div>
  );

  return (
    <section
      data-slot="chat-agent-panel"
      data-collapsed={collapsed ? 'true' : undefined}
      className={`flex flex-col overflow-hidden rounded-lg border
        border-border/60 bg-card ${className ?? ''}`}
    >
      {header}
      {!collapsed && (
        <div className="flex min-h-0 flex-1 flex-col p-3">
          <LiveAgentChat
            sessionId={sessionId ?? null}
            messages={messages}
            viewerRole={viewerRole}
            viewerId={viewerId}
            onSendMessage={handleSendMessage}
            compact={compact}
            labels={labels.chatPanelLabels}
          />
        </div>
      )}
    </section>
  );
}
