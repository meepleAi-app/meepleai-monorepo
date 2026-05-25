/**
 * GameDetailChatTab - Wave C follow-up (Issue #1471)
 *
 * Inline chat preview rendered inside the Agents tab content. Shows the N most
 * recent messages (caller passes them, default cap 3) + a "Open full chat" CTA
 * that links to the dedicated chat surface. Pure presentational: data fetching,
 * loading/error states are the caller's responsibility.
 *
 * Scope note: per PILOT_GAP_REPORT § 3.5 #2 the codebase `agents` tab is the
 * semantic match for the mockup's "Chat" tab. The full chat-unified primitives
 * (ChatMessageList, CitationBlock, …) are intentionally NOT reused here — this
 * is a compact preview, not the full chat surface (LibraryGameAgentShell scope).
 */

'use client';

import type { ReactElement } from 'react';

import clsx from 'clsx';

export type GameDetailChatRole = 'user' | 'assistant';

export interface GameDetailChatPreviewMessage {
  readonly id: string;
  readonly role: GameDetailChatRole;
  readonly content: string;
  readonly timestamp: string;
}

export interface GameDetailChatTabLabels {
  readonly title: string;
  readonly empty: string;
  readonly openCta: string;
  readonly userPrefix: string;
  readonly assistantPrefix: string;
}

export interface GameDetailChatTabProps {
  readonly messages: ReadonlyArray<GameDetailChatPreviewMessage>;
  readonly openHref: string;
  readonly labels: GameDetailChatTabLabels;
  /** Max messages rendered (default 3). */
  readonly maxItems?: number;
  readonly className?: string;
}

export function GameDetailChatTab(props: GameDetailChatTabProps): ReactElement {
  const { messages, openHref, labels, maxItems = 3, className } = props;
  const rows = messages.slice(0, maxItems);

  return (
    <section
      data-slot="game-detail-chat-tab"
      className={clsx('rounded-2xl border border-border bg-card p-[18px] shadow-sm', className)}
    >
      <div className="mb-3.5 flex items-center justify-between gap-3">
        <h3 className="font-display text-[15px] font-extrabold text-foreground">{labels.title}</h3>
        <a
          href={openHref}
          data-slot="game-detail-chat-tab-open-cta"
          className="rounded-md border border-border bg-transparent px-2.5 py-1 font-display text-[12px] font-extrabold text-foreground transition-colors hover:bg-muted focus-visible:outline-none focus-visible:ring-2 focus-visible:ring-ring"
        >
          {labels.openCta}
        </a>
      </div>

      {rows.length === 0 ? (
        <p
          data-slot="game-detail-chat-tab-empty"
          className="py-6 text-center font-mono text-[11px] text-muted-foreground"
        >
          {labels.empty}
        </p>
      ) : (
        <ol role="list" aria-label={labels.title} className="flex flex-col gap-2">
          {rows.map(msg => (
            <li
              key={msg.id}
              role="listitem"
              data-slot="game-detail-chat-tab-message"
              data-role={msg.role}
              className="flex flex-col gap-1 rounded-xl border border-border bg-background p-3"
            >
              <div className="flex items-center gap-2">
                <span
                  className={clsx(
                    'font-mono text-[9px] font-extrabold uppercase tracking-[0.08em]',
                    msg.role === 'assistant' ? 'text-entity-agent' : 'text-muted-foreground'
                  )}
                >
                  {msg.role === 'assistant' ? labels.assistantPrefix : labels.userPrefix}
                </span>
                <span className="font-mono text-[10px] text-muted-foreground">{msg.timestamp}</span>
              </div>
              <p className="line-clamp-3 font-mono text-[12px] text-foreground">{msg.content}</p>
            </li>
          ))}
        </ol>
      )}
    </section>
  );
}
