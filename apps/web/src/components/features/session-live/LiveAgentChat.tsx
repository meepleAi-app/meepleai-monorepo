/* eslint-disable local/no-hardcoded-color-utility -- text-white / button color on style-prop colored bg or entity-colored CTA; mockup .e-bg pattern. DS-12 will introduce primitives encoding bg via className. */
'use client';

/**
 * LiveAgentChat — Wave D.2 Interactions sub-PR (Issue #750).
 *
 * Chat panel with messages list and send form.
 *
 * Role variants:
 *   Spectator: visibility forced 'shared' (no private toggle visible)
 *   Player+Host: both visibility options (private/shared toggle)
 *
 * Gate C: DIVERGES from MeepleCard — live chat panel, not a card pattern.
 *
 * data-slot="live-agent-chat" — required by unit tests.
 * data-viewer-role={viewerRole} — role variant assertion in unit tests.
 */

import { type ReactElement, useState, useRef, useEffect } from 'react';

import { Send } from 'lucide-react';

import type { ParticipantRole } from '@/lib/session-live/participant-role';

// ─── Types ────────────────────────────────────────────────────────────────────

export interface ChatMessage {
  readonly id: string;
  readonly senderId: string;
  readonly senderName: string;
  readonly content: string;
  readonly visibility: 'private' | 'shared';
  readonly timestamp: string;
}

// ─── Labels ───────────────────────────────────────────────────────────────────

export interface LiveAgentChatLabels {
  readonly title: string;
  readonly inputAriaLabel: string;
  readonly sendAriaLabel: string;
  readonly visibilityPrivate: string;
  readonly visibilityShared: string;
  readonly emptyMessage: string;
}

// ─── Props ────────────────────────────────────────────────────────────────────

export interface LiveAgentChatProps {
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
  messages,
  viewerRole,
  viewerId,
  onSendMessage,
  compact = false,
  labels,
  className,
}: LiveAgentChatProps): ReactElement {
  const [inputValue, setInputValue] = useState('');
  // Spectator forced to 'shared'; Player+Host can toggle
  const [visibility, setVisibility] = useState<'private' | 'shared'>('shared');
  const messagesEndRef = useRef<HTMLDivElement>(null);

  const isSpectator = viewerRole === 'Spectator';

  // Auto-scroll to bottom on new messages
  useEffect(() => {
    messagesEndRef.current?.scrollIntoView({ behavior: 'smooth' });
  }, [messages.length]);

  const handleSubmit = (e: React.FormEvent) => {
    e.preventDefault();
    const trimmed = inputValue.trim();
    if (!trimmed) return;
    // Spectator always sends as shared
    onSendMessage(trimmed, isSpectator ? 'shared' : visibility);
    setInputValue('');
  };

  return (
    <section
      data-slot="live-agent-chat"
      data-viewer-role={viewerRole}
      aria-label={labels.title}
      className={`flex flex-col ${compact ? 'gap-2' : 'gap-3'} ${className ?? ''}`}
    >
      <h3 className="shrink-0 text-xs font-semibold uppercase tracking-wider text-muted-foreground">
        {labels.title}
      </h3>

      {/* Messages list */}
      <div
        className="flex min-h-0 flex-1 flex-col gap-2 overflow-y-auto"
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
                  className={`max-w-[85%] rounded-lg px-3 py-1.5 text-sm ${
                    isOwn ? 'bg-card text-slate-100' : 'bg-card text-slate-200'
                  } ${isPrivate ? 'border border-amber-700/40' : ''}`}
                >
                  {msg.content}
                  {isPrivate && (
                    <span className="ml-2 text-xs text-amber-400/70">
                      {labels.visibilityPrivate}
                    </span>
                  )}
                </div>
              </div>
            );
          })
        )}
        <div ref={messagesEndRef} />
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
                  ? 'bg-slate-600 text-slate-100'
                  : 'text-muted-foreground hover:text-slate-300'
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
                  : 'text-muted-foreground hover:text-slate-300'
              }`}
            >
              {labels.visibilityPrivate}
            </button>
          </div>
        )}

        <div className="flex gap-2">
          <input
            type="text"
            value={inputValue}
            onChange={e => setInputValue(e.target.value)}
            aria-label={labels.inputAriaLabel}
            placeholder={labels.inputAriaLabel}
            className="min-w-0 flex-1 rounded-lg border border-border/60 bg-card
              px-3 py-2 text-sm text-slate-200 placeholder-slate-500
              focus-visible:outline-none focus-visible:ring-2 focus-visible:ring-slate-400"
          />
          <button
            type="submit"
            aria-label={labels.sendAriaLabel}
            disabled={!inputValue.trim()}
            className="flex shrink-0 items-center justify-center rounded-lg border
              border-border/60 bg-card px-3 py-2 text-slate-200
              transition-colors hover:bg-slate-600
              disabled:cursor-not-allowed disabled:opacity-40
              focus-visible:outline-none focus-visible:ring-2 focus-visible:ring-slate-400"
          >
            <Send className="h-4 w-4" aria-hidden="true" />
          </button>
        </div>
      </form>
    </section>
  );
}
