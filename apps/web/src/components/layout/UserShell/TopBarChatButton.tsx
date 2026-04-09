'use client';

interface TopBarChatButtonProps {
  onOpen?: () => void;
  hasUnread?: boolean;
}

export function TopBarChatButton({ onOpen, hasUnread = false }: TopBarChatButtonProps) {
  const handleClick = () => {
    if (onOpen) {
      onOpen();
      return;
    }
    // Phase 1: chat panel not wired yet — Phase 4 will provide onOpen

    console.warn(
      '[TopBarChatButton] onOpen handler not provided — chat panel not wired yet (Phase 4)'
    );
  };

  return (
    <button
      type="button"
      aria-label="Chat agente"
      onClick={handleClick}
      className="relative flex h-10 w-10 items-center justify-center rounded-[10px] border border-[var(--nh-border-default)] bg-[var(--nh-bg-surface)] text-base shrink-0 hover:bg-white hover:shadow-[var(--shadow-warm-sm)] hover:-translate-y-px transition-all"
    >
      <span aria-hidden>💬</span>
      {hasUnread && (
        <span
          data-testid="chat-unread-dot"
          aria-label="Unread messages"
          className="absolute top-[7px] right-[7px] h-2 w-2 rounded-full"
          style={{
            background: 'hsl(350 89% 58%)',
            boxShadow: '0 0 0 2px var(--nh-bg-surface)',
          }}
        />
      )}
    </button>
  );
}
