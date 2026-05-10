/**
 * ChatBubble — bubble user|agent. Pure component.
 * Agent bubble include header (avatar + name) e slot children per citations,
 * confidence badge, disclaimer, out-of-context actions.
 * Spec: §3.2
 */
import type { ReactElement, ReactNode } from 'react';
import clsx from 'clsx';

export interface ChatBubbleProps {
  readonly role: 'user' | 'agent';
  readonly content: ReactNode;
  readonly agentName?: string;
  readonly avatar?: string;
  readonly children?: ReactNode;
  readonly className?: string;
  /** G2: messaggio storico ricaricato da hydrate — visual variant attenuato */
  readonly isHistorical?: boolean;
}

export function ChatBubble({
  role,
  content,
  agentName,
  avatar,
  children,
  className,
  isHistorical,
}: ChatBubbleProps): ReactElement {
  const isAgent = role === 'agent';
  return (
    <div
      data-testid="chat-bubble"
      data-role={role}
      data-historical={isHistorical ? 'true' : undefined}
      className={clsx(
        'max-w-[85%] rounded-2xl px-4 py-3 text-base leading-relaxed',
        isAgent
          ? 'self-start rounded-bl-sm border border-[hsl(var(--c-agent)/0.18)] bg-[hsl(var(--c-agent)/0.08)] text-foreground'
          : 'self-end rounded-br-sm bg-[hsl(var(--c-chat))] text-white',
        isHistorical && 'opacity-90',
        className
      )}
    >
      {isAgent && agentName && (
        <div className="mb-2 flex items-center gap-2">
          <span
            aria-hidden="true"
            className="flex h-6 w-6 items-center justify-center rounded-full bg-[hsl(var(--c-agent)/0.25)] text-xs"
          >
            {avatar ?? '🤖'}
          </span>
          <span className="font-bold text-sm text-[hsl(var(--c-agent))]">{agentName}</span>
        </div>
      )}
      <div data-slot="bubble-content">{content}</div>
      {children && <div data-slot="bubble-extras">{children}</div>}
    </div>
  );
}
