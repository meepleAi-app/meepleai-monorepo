/**
 * MeepleChatCard — Chat session entity adapter for MeepleCard.
 *
 * Renders a chat session with nav-footer wired to messages, agent link, and archive.
 */

'use client';

import { useMemo } from 'react';

import { format, formatDistanceToNow } from 'date-fns';
import { it } from 'date-fns/locale';
import { useRouter } from 'next/navigation';

import { MeepleCard, type MeepleCardVariant } from '@/components/ui/data-display/meeple-card';
import { buildChatNavItems } from '@/components/ui/data-display/meeple-card/nav-items';

// ============================================================================
// Types
// ============================================================================

export interface ChatSummary {
  id: string;
  title: string;
  /** ISO date string */
  lastMessageAt: string;
  /** Total messages in this chat session */
  messageCount: number;
  /** Optional linked agent id */
  agentId?: string | null;
}

export interface MeepleChatCardProps {
  chat: ChatSummary;
  variant?: MeepleCardVariant;
  onClick?: (chatId: string) => void;
  onArchive?: (chatId: string) => void;
  className?: string;
}

// ============================================================================
// Component
// ============================================================================

export function MeepleChatCard({
  chat,
  variant = 'list',
  onClick,
  onArchive,
  className,
}: MeepleChatCardProps) {
  const router = useRouter();

  const subtitle = useMemo(() => {
    try {
      return formatDistanceToNow(new Date(chat.lastMessageAt), {
        addSuffix: true,
        locale: it,
      });
    } catch {
      return format(new Date(chat.lastMessageAt), 'PPp', { locale: it });
    }
  }, [chat.lastMessageAt]);

  const navItems = useMemo(
    () =>
      buildChatNavItems(
        { messageCount: chat.messageCount },
        {
          onMessagesClick: () => router.push(`/chat/${chat.id}`),
          onAgentLinkClick: chat.agentId ? () => router.push(`/agents/${chat.agentId}`) : undefined,
          onArchiveClick: onArchive ? () => onArchive(chat.id) : undefined,
        }
      ),
    [chat.messageCount, chat.id, chat.agentId, router, onArchive]
  );

  return (
    <MeepleCard
      id={chat.id}
      entity="chat"
      variant={variant}
      title={chat.title}
      subtitle={subtitle}
      navItems={navItems}
      onClick={onClick ? () => onClick(chat.id) : undefined}
      className={className}
      data-testid={`chat-card-${chat.id}`}
    />
  );
}

export default MeepleChatCard;
