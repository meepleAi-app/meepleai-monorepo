'use client';

import { MessageCircle } from 'lucide-react';
import Link from 'next/link';

/**
 * Displayed when the user has no active conversation.
 * Guides them to the library to start chatting about a game's rules.
 */
export function ChatEmptyState() {
  return (
    <div className="flex flex-col items-center justify-center gap-4 px-6 py-16 text-center">
      <div className="flex h-16 w-16 items-center justify-center rounded-full bg-purple-500/20">
        <MessageCircle className="h-8 w-8 text-purple-400" />
      </div>

      <div className="flex flex-col gap-1">
        <h2 className="text-lg font-semibold text-[var(--gaming-text-primary,white)]">
          Nessuna conversazione
        </h2>
        <p className="text-sm text-[var(--gaming-text-secondary,rgba(255,255,255,0.6))]">
          Vai alla libreria e chiedi qualcosa sulle regole di un gioco!
        </p>
      </div>

      <Link
        href="/library"
        className="text-sm font-medium text-amber-400 hover:text-amber-300 transition-colors underline-offset-4 hover:underline"
      >
        Vai alla Libreria
      </Link>
    </div>
  );
}
