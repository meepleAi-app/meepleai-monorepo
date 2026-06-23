'use client';

/**
 * LiveAgentChat — Wave D.2 Interactions sub-PR (Issue #750).
 * Extended by Issue #2375 G3 with sessionStorage draft + smart auto-scroll.
 *
 * Role variants:
 *   Spectator: visibility forced 'shared' (no private toggle visible)
 *   Player+Host: both visibility options (private/shared toggle)
 *
 * #2375 G3:
 *   - sessionId prop → useChatDraft persists input across collapse/expand cycles
 *   - useScrollAnchor → smart auto-scroll: auto when at bottom, toast when scrolled up
 *   - data-at-bottom attribute reflects current anchor state (E2E selector)
 *
 * Gate C: DIVERGES from MeepleCard — live chat panel, not a card pattern.
 *
 * data-slot="live-agent-chat" — required by unit tests.
 * data-viewer-role={viewerRole} — role variant assertion in unit tests.
 * data-at-bottom={isAtBottom ? 'true' : 'false'} — #2375 G3 scroll anchor state.
 */

import { type ReactElement, useEffect, useRef, useState, type RefObject } from 'react';

import { Send } from 'lucide-react';
import { useIntl } from 'react-intl';

import { type ChatCitation, ChatCitationCard } from '@/components/chat/panel/ChatCitationCard';
import type { ParticipantRole } from '@/lib/session-live/participant-role';
import { useChatDraft } from '@/lib/session-live/use-chat-draft';
import { useScrollAnchor } from '@/lib/session-live/use-scroll-anchor';

// ─── Types ────────────────────────────────────────────────────────────────────

export interface ChatMessage {
  readonly id: string;
  readonly senderId: string;
  readonly senderName: string;
  readonly content: string;
  readonly visibility: 'private' | 'shared';
  readonly timestamp: string;
  readonly citations?: readonly ChatCitation[];
  /**
   * AC-CHAT-3: when true, the agent answered without any grounding citations.
   * Shows a discrete disclaimer below the bubble. NEVER true on system status messages
   * (those are injected by SessionLiveView without this flag).
   */
  readonly isNonGrounded?: boolean;
}

// ─── Labels ───────────────────────────────────────────────────────────────────

export interface LiveAgentChatLabels {
  readonly title: string;
  readonly inputAriaLabel: string;
  readonly sendAriaLabel: string;
  readonly visibilityPrivate: string;
  readonly visibilityShared: string;
  readonly emptyMessage: string;
  /** #2375 G3: aria-label on the "N nuovi messaggi" toast button. */
  readonly newMessagesToastAriaLabel: string;
}

// ─── Props ────────────────────────────────────────────────────────────────────

export interface LiveAgentChatProps {
  /** #2375 G3: sessionId used as sessionStorage key for draft persistence. */
  readonly sessionId: string | null;
  readonly messages: ReadonlyArray<ChatMessage>;
  readonly viewerRole: ParticipantRole;
  readonly viewerId: string;
  readonly onSendMessage: (content: string, visibility: 'private' | 'shared') => void;
  readonly compact?: boolean;
  readonly labels: LiveAgentChatLabels;
  readonly className?: string;
}

// ─── Component ────────────────────────────────────────────────────────────────

export function LiveAgentChat({
  sessionId,
  messages,
  viewerRole,
  viewerId,
  onSendMessage,
  compact = false,
  labels,
  className,
}: LiveAgentChatProps): ReactElement {
  const intl = useIntl();
  const { draft, setDraft, clearDraft } = useChatDraft({ sessionId });

  // Spectator forced to 'shared'; Player+Host can toggle
  const [visibility, setVisibility] = useState<'private' | 'shared'>('shared');

  const containerRef = useRef<HTMLDivElement>(null);
  const bottomRef = useRef<HTMLDivElement>(null);

  const { isAtBottom, scrollToBottom } = useScrollAnchor({
    // Cast: HTMLDivElement extends HTMLElement; hook only reads .current via null guard.
    containerRef: containerRef as RefObject<HTMLElement | null>,
    bottomRef: bottomRef as RefObject<HTMLElement | null>,
    trigger: messages.length,
  });

  // Track new arrivals while scrolled up to display "N nuovi messaggi" toast.
  const [newMessageCount, setNewMessageCount] = useState<number>(0);
  const prevMessageCountRef = useRef<number>(messages.length);

  useEffect(() => {
    const prev = prevMessageCountRef.current;
    const next = messages.length;
    if (next > prev) {
      if (isAtBottom) {
        scrollToBottom();
        setNewMessageCount(0);
      } else {
        setNewMessageCount(c => c + (next - prev));
      }
    }
    prevMessageCountRef.current = next;
  }, [messages.length, isAtBottom, scrollToBottom]);

  // C1 fix: reset counter when user manually scrolls to bottom (isAtBottom flips true
  // without a new message arrival — the counter-increment branch above is not taken,
  // so we need a dedicated effect to clear the stale count).
  useEffect(() => {
    if (isAtBottom) {
      setNewMessageCount(0);
    }
  }, [isAtBottom]);

  const isSpectator = viewerRole === 'Spectator';

  const toastLabel = intl.formatMessage(
    { id: 'pages.sessionLive.chat.newMessagesToast' },
    { count: newMessageCount }
  );

  const handleSubmit = (e: React.FormEvent): void => {
    e.preventDefault();
    const trimmed = draft.trim();
    if (!trimmed) return;
    onSendMessage(trimmed, isSpectator ? 'shared' : visibility);
    clearDraft();
  };

  const handleToastClick = (): void => {
    scrollToBottom();
    setNewMessageCount(0);
  };

  return (
    <section
      data-slot="live-agent-chat"
      data-viewer-role={viewerRole}
      data-at-bottom={isAtBottom ? 'true' : 'false'}
      aria-label={labels.title}
      className={`flex flex-col ${compact ? 'gap-2' : 'gap-3'} ${className ?? ''}`}
    >
      <h3 className="shrink-0 text-xs font-semibold uppercase tracking-wider text-muted-foreground">
        {labels.title}
      </h3>

      {/* Messages list */}
      <div
        ref={containerRef}
        className="relative flex min-h-0 flex-1 flex-col gap-2 overflow-y-auto"
        aria-live="polite"
        aria-atomic="false"
        aria-relevant="additions"
      >
        {messages.length === 0 ? (
          <p className="py-4 text-center text-sm text-muted-foreground">{labels.emptyMessage}</p>
        ) : (
          messages.map(msg => {
            const isOwn = msg.senderId === viewerId;
            const isPrivate = msg.visibility === 'private';
            return (
              <div
                key={msg.id}
                className={`flex flex-col gap-0.5 ${isOwn ? 'items-end' : 'items-start'}`}
                data-message-id={msg.id}
                data-visibility={msg.visibility}
              >
                {!isOwn && <span className="text-xs text-muted-foreground">{msg.senderName}</span>}
                <div
                  className={`max-w-[85%] rounded-lg px-3 py-1.5 text-sm bg-card text-foreground ${
                    isPrivate ? 'border border-amber-700/40' : ''
                  }`}
                >
                  {msg.content}
                  {isPrivate && (
                    <span className="ml-2 text-xs text-amber-400/70">
                      {labels.visibilityPrivate}
                    </span>
                  )}
                </div>
                {msg.citations && msg.citations.length > 0 && (
                  <div data-slot="chat-citations" className="mt-1 flex flex-col gap-1">
                    {msg.citations.map((c, i) => (
                      <ChatCitationCard key={i} citation={c} />
                    ))}
                  </div>
                )}
                {/* AC-CHAT-3: non-grounded disclaimer — only for agent messages with
                    zero citations and explicit isNonGrounded flag.
                    NEVER shown on system status messages (those lack the flag). */}
                {msg.isNonGrounded === true && !isOwn && (
                  <p
                    data-slot="chat-nongrounded-disclaimer"
                    className="mt-1 flex items-center gap-1 text-xs text-muted-foreground"
                  >
                    <span aria-hidden="true">⚠️</span>
                    {intl.formatMessage({
                      id: 'pages.sessionLive.chatAgent.nonGroundedDisclaimer',
                    })}
                  </p>
                )}
              </div>
            );
          })
        )}
        <div ref={bottomRef} aria-hidden="true" data-slot="chat-bottom-sentinel" />

        {/* #2375 G3 — "N nuovi messaggi" pill, shown when scrolled up + new arrivals */}
        {!isAtBottom && newMessageCount > 0 && (
          <div className="sticky bottom-2 flex justify-center">
            <button
              type="button"
              onClick={handleToastClick}
              aria-label={labels.newMessagesToastAriaLabel}
              data-slot="chat-new-messages-toast"
              className="rounded-full border border-entity-agent/40 bg-entity-agent/15
                px-3 py-1 text-xs font-semibold text-entity-agent shadow-sm
                hover:bg-entity-agent/25 focus-visible:outline-none
                focus-visible:ring-2 focus-visible:ring-ring"
            >
              {toastLabel}
            </button>
          </div>
        )}
      </div>

      {/* Send form */}
      <form onSubmit={handleSubmit} className="flex shrink-0 flex-col gap-2">
        {/* Visibility toggle — hidden for Spectator */}
        {!isSpectator && (
          <div className="flex gap-2" role="group" aria-label="Visibilità messaggio">
            <button
              type="button"
              aria-pressed={visibility === 'shared'}
              onClick={() => setVisibility('shared')}
              className={`rounded-md px-2 py-1 text-xs font-medium transition-colors ${
                visibility === 'shared'
                  ? 'bg-muted text-foreground'
                  : 'text-muted-foreground hover:text-foreground'
              }`}
            >
              {labels.visibilityShared}
            </button>
            <button
              type="button"
              aria-pressed={visibility === 'private'}
              onClick={() => setVisibility('private')}
              className={`rounded-md px-2 py-1 text-xs font-medium transition-colors ${
                visibility === 'private'
                  ? 'bg-amber-700/60 text-amber-100'
                  : 'text-muted-foreground hover:text-foreground'
              }`}
            >
              {labels.visibilityPrivate}
            </button>
          </div>
        )}

        <div className="flex gap-2">
          <input
            type="text"
            value={draft}
            onChange={e => setDraft(e.target.value)}
            aria-label={labels.inputAriaLabel}
            placeholder={labels.inputAriaLabel}
            className="min-w-0 flex-1 rounded-lg border border-border/60 bg-card
              px-3 py-2 text-sm text-foreground placeholder:text-muted-foreground
              focus-visible:outline-none focus-visible:ring-2 focus-visible:ring-ring"
          />
          <button
            type="submit"
            aria-label={labels.sendAriaLabel}
            disabled={!draft.trim()}
            className="flex shrink-0 items-center justify-center rounded-lg border
              border-border/60 bg-card px-3 py-2 text-foreground
              transition-colors hover:bg-muted
              disabled:cursor-not-allowed disabled:opacity-40
              focus-visible:outline-none focus-visible:ring-2 focus-visible:ring-ring"
          >
            <Send className="h-4 w-4" aria-hidden="true" />
          </button>
        </div>
      </form>
    </section>
  );
}
