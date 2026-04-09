'use client';

import { cn } from '@/lib/utils';

export interface ChatRecentItem {
  id: string;
  emoji: string;
  title: string;
  timestamp: string;
  active?: boolean;
}

export interface ChatKbGame {
  id: string;
  name: string;
  status: 'ready' | 'indexing' | 'failed';
  imageUrl?: string;
}

interface ChatSidebarProps {
  chats: ChatRecentItem[];
  kbGames: ChatKbGame[];
  onNewChat: () => void;
  onSelectChat: (chatId: string) => void;
  onSelectGame: (gameId: string) => void;
}

const STATUS_COLORS: Record<ChatKbGame['status'], string> = {
  ready: 'hsl(140 60% 45%)',
  indexing: 'hsl(38 92% 50%)',
  failed: 'hsl(0 70% 55%)',
};

export function ChatSidebar({
  chats,
  kbGames,
  onNewChat,
  onSelectChat,
  onSelectGame,
}: ChatSidebarProps) {
  return (
    <aside className="flex w-[220px] flex-shrink-0 flex-col gap-1 overflow-y-auto border-r border-[var(--nh-border-default)] bg-[rgba(255,252,248,0.5)] px-2.5 py-3.5">
      <button
        type="button"
        onClick={onNewChat}
        className="mb-2.5 flex items-center justify-center gap-2 rounded-[10px] px-3.5 py-2.5 font-nunito text-[0.8rem] font-extrabold text-white transition-all hover:-translate-y-px"
        style={{
          background: 'linear-gradient(135deg, hsl(220 80% 58%), hsl(220 80% 42%))',
          boxShadow: '0 2px 8px hsla(220, 80%, 55%, 0.3)',
        }}
      >
        ＋ Nuova chat
      </button>

      <div className="mt-2 px-2.5 pb-1 text-[0.64rem] font-extrabold uppercase tracking-wider text-[var(--nh-text-muted)]">
        ▾ Chat recenti
      </div>
      {chats.map(chat => (
        <button
          key={chat.id}
          type="button"
          onClick={() => onSelectChat(chat.id)}
          className={cn(
            'relative flex items-start gap-2.5 rounded-[10px] px-2.5 py-2.5 text-left transition-all',
            chat.active
              ? 'bg-[hsla(220,80%,55%,0.08)] shadow-[inset_0_0_0_1px_hsla(220,80%,55%,0.2)]'
              : 'hover:bg-[var(--nh-bg-elevated)]'
          )}
        >
          {chat.active && (
            <span
              aria-hidden
              className="absolute left-0 top-2.5 bottom-2.5 w-[3px] rounded-r"
              style={{ background: 'hsl(220 80% 55%)' }}
            />
          )}
          <span aria-hidden className="flex-shrink-0 text-[15px] leading-tight">
            {chat.emoji}
          </span>
          <div className="min-w-0 flex-1">
            <div className="truncate font-quicksand text-[0.76rem] font-bold leading-tight text-[var(--nh-text-primary)]">
              {chat.title}
            </div>
            <div className="mt-0.5 text-[0.66rem] text-[var(--nh-text-muted)]">
              {chat.timestamp}
            </div>
          </div>
        </button>
      ))}

      <div className="mt-4 px-2.5 pb-1 text-[0.64rem] font-extrabold uppercase tracking-wider text-[var(--nh-text-muted)]">
        ▾ Giochi con KB
      </div>
      {kbGames.map(game => (
        <button
          key={game.id}
          type="button"
          onClick={() => onSelectGame(game.id)}
          className="flex items-center gap-2.5 rounded-[9px] px-2.5 py-1.5 text-left transition-all hover:bg-[var(--nh-bg-elevated)]"
        >
          <div
            aria-hidden
            className="flex h-[26px] w-[26px] flex-shrink-0 items-center justify-center overflow-hidden rounded-md"
            style={{
              background: 'linear-gradient(135deg, hsl(25 75% 78%), hsl(25 80% 55%))',
            }}
          >
            {game.imageUrl ? (
              // eslint-disable-next-line @next/next/no-img-element
              <img src={game.imageUrl} alt="" className="h-full w-full object-cover" />
            ) : (
              <span className="text-[11px]">🎲</span>
            )}
          </div>
          <span className="min-w-0 flex-1 truncate font-nunito text-[0.74rem] font-bold text-[var(--nh-text-primary)]">
            {game.name}
          </span>
          <span
            aria-hidden="true"
            title={`KB status: ${game.status}`}
            className={cn(
              'h-[7px] w-[7px] flex-shrink-0 rounded-full',
              game.status === 'indexing' && 'animate-pulse'
            )}
            style={{ background: STATUS_COLORS[game.status] }}
          />
        </button>
      ))}
    </aside>
  );
}
