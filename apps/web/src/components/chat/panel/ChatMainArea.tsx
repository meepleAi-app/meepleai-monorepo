'use client';

import { ChatInputBar } from './ChatInputBar';
import { ChatMessageBubble, type ChatMessageRole } from './ChatMessageBubble';

export interface ChatMessage {
  id: string;
  role: ChatMessageRole;
  content: string;
  authorName: string;
  timestamp: string;
}

interface ChatMainAreaProps {
  messages: ChatMessage[];
  gameName?: string;
  suggestedQuestions: string[];
  onSend: (message: string) => void;
  /** Stream error message to show above the input bar. */
  error?: string | null;
}

export function ChatMainArea({
  messages,
  gameName,
  suggestedQuestions,
  onSend,
  error,
}: ChatMainAreaProps) {
  const isEmpty = messages.length === 0;

  return (
    <div className="flex min-w-0 flex-1 flex-col bg-[var(--nh-bg-base)]">
      <div className="flex-1 overflow-y-auto px-6 py-6">
        {isEmpty ? (
          <div className="mx-auto flex h-full max-w-md flex-col items-center justify-center text-center">
            <div
              aria-hidden
              className="mb-4 flex h-14 w-14 items-center justify-center rounded-2xl text-3xl text-white shadow-[var(--shadow-warm-md)]"
              style={{
                background: 'linear-gradient(135deg, hsl(262 83% 70%), hsl(262 83% 50%))',
              }}
            >
              🤖
            </div>
            <h3 className="mb-2 font-quicksand text-xl font-extrabold text-[var(--nh-text-primary)]">
              Ciao! Sono il tuo assistente AI
            </h3>
            <p className="mb-6 text-sm text-[var(--nh-text-muted)]">
              {gameName
                ? `Chiedimi qualsiasi cosa sulle regole di ${gameName}.`
                : 'Chiedimi qualsiasi cosa sui regolamenti dei tuoi giochi da tavolo.'}
            </p>
            {suggestedQuestions.length > 0 && (
              <div className="flex flex-wrap justify-center gap-2">
                {suggestedQuestions.map(q => (
                  <button
                    key={q}
                    type="button"
                    onClick={() => onSend(q)}
                    className="rounded-full border border-[var(--nh-border-default)] bg-[var(--nh-bg-elevated)] px-3.5 py-2 font-nunito text-[0.76rem] font-bold text-[var(--nh-text-secondary)] transition-all hover:-translate-y-px hover:bg-white hover:shadow-[var(--shadow-warm-sm)]"
                  >
                    {q}
                  </button>
                ))}
              </div>
            )}
          </div>
        ) : (
          <div className="mx-auto flex max-w-3xl flex-col gap-[18px]">
            {messages.map(msg => (
              <ChatMessageBubble
                key={msg.id}
                role={msg.role}
                content={msg.content}
                authorName={msg.authorName}
                timestamp={msg.timestamp}
              />
            ))}
          </div>
        )}
      </div>
      {error && (
        <div className="mx-6 mb-3 rounded-xl border border-red-200 bg-red-50 px-4 py-2.5 font-nunito text-sm text-red-700">
          {error}
        </div>
      )}
      <ChatInputBar
        placeholder={gameName ? `Chiedi una regola su ${gameName}…` : 'Chiedi una regola…'}
        onSend={onSend}
      />
    </div>
  );
}
