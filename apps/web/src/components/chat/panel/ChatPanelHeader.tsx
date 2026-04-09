'use client';

interface ChatPanelHeaderProps {
  subtitle: string;
  onClose: () => void;
}

export function ChatPanelHeader({ subtitle, onClose }: ChatPanelHeaderProps) {
  return (
    <div className="flex flex-shrink-0 items-center gap-3.5 border-b border-[var(--nh-border-default)] bg-[rgba(255,252,248,0.7)] px-5 py-4 backdrop-blur-md">
      <div
        className="flex h-[42px] w-[42px] flex-shrink-0 items-center justify-center rounded-xl text-xl text-white"
        style={{
          background: 'linear-gradient(135deg, hsl(220 80% 60%), hsl(220 80% 42%))',
          boxShadow: '0 2px 8px hsla(220, 80%, 55%, 0.4)',
        }}
        aria-hidden
      >
        💬
      </div>
      <div className="min-w-0 flex-1">
        <h2 className="font-quicksand text-[1.08rem] font-extrabold leading-tight">
          Chat con l&apos;agente
        </h2>
        <p className="mt-0.5 text-[0.74rem] text-[var(--nh-text-muted)]">{subtitle}</p>
      </div>
      <button
        type="button"
        aria-label="Chiudi chat panel"
        onClick={onClose}
        className="flex h-9 w-9 flex-shrink-0 items-center justify-center rounded-[10px] border border-[var(--nh-border-default)] bg-[var(--nh-bg-surface)] text-lg text-[var(--nh-text-secondary)] transition-all hover:bg-white hover:text-[var(--nh-text-primary)] hover:shadow-[var(--shadow-warm-sm)]"
      >
        ✕
      </button>
    </div>
  );
}
