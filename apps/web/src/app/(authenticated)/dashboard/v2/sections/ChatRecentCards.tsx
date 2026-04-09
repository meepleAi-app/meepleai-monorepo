'use client';

import Link from 'next/link';

export interface ChatRecentPreview {
  id: string;
  gameName: string;
  topic: string;
  snippet: string;
  confidence: number;
  timestamp: string;
}

interface ChatRecentCardsProps {
  chats: ChatRecentPreview[];
}

function formatConfidence(score: number): string {
  return `✓ ${Math.round(score * 100)}% accurata`;
}

export function ChatRecentCards({ chats }: ChatRecentCardsProps) {
  if (chats.length === 0) return null;

  return (
    <section className="mb-7">
      <div className="mb-3.5 flex items-center justify-between">
        <h3 className="flex items-center gap-3 font-quicksand text-[1.1rem] font-extrabold">
          <span
            aria-hidden
            className="inline-block h-[18px] w-1 rounded-sm"
            style={{ background: 'hsl(220 80% 55%)' }}
          />
          Chat recenti con l&apos;agente
        </h3>
        <Link
          href="/chat"
          className="rounded-lg px-3 py-1.5 text-[0.78rem] font-bold text-[hsl(25_95%_40%)] transition-colors hover:bg-[hsla(25,95%,45%,0.08)]"
        >
          Vedi tutto →
        </Link>
      </div>
      <div className="grid grid-cols-3 gap-4">
        {chats.slice(0, 3).map(chat => (
          <div
            key={chat.id}
            className="relative flex min-h-[140px] cursor-pointer flex-col gap-3 overflow-hidden rounded-[20px] border border-[var(--nh-border-default)] bg-[var(--nh-bg-elevated)] p-5 shadow-[var(--shadow-warm-sm)] transition-all duration-300 hover:-translate-y-0.5 hover:shadow-[var(--shadow-warm-md)]"
          >
            <span
              aria-hidden
              className="absolute left-0 top-0 bottom-0 w-1"
              style={{ background: 'hsl(220 80% 55%)' }}
            />
            <div className="flex items-center gap-2.5">
              <div
                className="flex h-[38px] w-[38px] items-center justify-center rounded-[10px] text-lg"
                style={{
                  background: 'linear-gradient(135deg, hsl(220 72% 72%), hsl(220 80% 55%))',
                }}
                aria-hidden
              >
                💬
              </div>
              <div className="font-quicksand text-[0.88rem] font-extrabold leading-tight">
                {chat.gameName} · {chat.topic}
              </div>
            </div>
            <p className="line-clamp-2 text-[0.78rem] text-[var(--nh-text-secondary)]">
              {chat.snippet}
            </p>
            <div className="mt-auto flex items-center justify-between">
              <span
                className="inline-flex items-center gap-1.5 rounded-full px-2 py-0.5 font-nunito text-[0.68rem] font-extrabold uppercase tracking-wider"
                style={{
                  background: 'hsla(140, 60%, 45%, 0.12)',
                  color: 'hsl(140 60% 30%)',
                }}
              >
                {formatConfidence(chat.confidence)}
              </span>
              <span className="text-[0.7rem] font-semibold text-[var(--nh-text-muted)]">
                {chat.timestamp}
              </span>
            </div>
          </div>
        ))}
      </div>
    </section>
  );
}
