'use client';

import { useState } from 'react';
import type { ReactElement } from 'react';

import Link from 'next/link';

import { useGamebookCampaign } from '@/lib/gamebook/hooks/useGamebookCampaign';
import { useUpdateGamebookProgress } from '@/lib/gamebook/hooks/useUpdateGamebookProgress';
import { useChatPanelStore } from '@/lib/stores/chat-panel-store';

export interface GamebookPlayShellProps {
  campaignId: string;
  gameId: string;
  agentId?: string;
}

export function GamebookPlayShell({
  campaignId,
  gameId,
  agentId: _agentId,
}: GamebookPlayShellProps): ReactElement {
  const { data, isLoading } = useGamebookCampaign(campaignId);
  const updateProgress = useUpdateGamebookProgress(campaignId);
  const openChat = useChatPanelStore(s => s.open);

  const [paragraphInput, setParagraphInput] = useState<string>('');

  if (isLoading || !data) {
    return (
      <div
        data-testid="gamebook-play-shell-skeleton"
        className="grid grid-rows-[auto_1fr] h-full bg-[var(--c-game)]/5"
      >
        <div className="h-16 border-b border-[var(--c-game)]/20 animate-pulse bg-[var(--c-game)]/10" />
        <div className="animate-pulse" />
      </div>
    );
  }

  const currentDisplay = data.currentParagraph > 0 ? `§ ${data.currentParagraph}` : '§ —';

  const handleSubmit = (e: React.FormEvent) => {
    e.preventDefault();
    const n = Number.parseInt(paragraphInput, 10);
    if (!Number.isFinite(n) || n < 0) return;
    updateProgress.mutate(n, {
      onSuccess: () => setParagraphInput(''),
    });
  };

  const handleOpenChat = () => {
    openChat();
  };

  return (
    <div className="grid grid-rows-[auto_1fr] h-full">
      <header
        className="flex items-center justify-between gap-4 px-6 py-3 border-b border-[var(--c-game)]/20 bg-[var(--c-game)]/5"
        data-testid="gamebook-play-shell-header"
      >
        <div className="flex flex-col">
          <h1 className="text-lg font-semibold text-[var(--c-game)]">{data.title}</h1>
          <span className="text-sm text-muted-foreground">{currentDisplay}</span>
        </div>
        <div className="flex items-center gap-2">
          <Link
            href={`/library/${gameId}/play/${campaignId}/translate`}
            className="rounded-md border border-[var(--c-game)]/40 bg-[var(--c-game)]/10 px-3 py-1.5 text-sm font-medium text-[var(--c-game)] hover:bg-[var(--c-game)]/20"
            data-testid="gamebook-open-translate"
          >
            📸 Traduci pagina
          </Link>
          <button
            type="button"
            onClick={handleOpenChat}
            className="rounded-md border border-[var(--c-agent)]/40 bg-[var(--c-agent)]/10 px-3 py-1.5 text-sm font-medium text-[var(--c-agent)] hover:bg-[var(--c-agent)]/20"
            data-testid="gamebook-open-chat"
          >
            Apri chat con agente
          </button>
        </div>
      </header>

      <main className="px-6 py-8 space-y-6">
        <section className="rounded-lg border border-[var(--c-game)]/20 bg-background p-4">
          <h2 className="text-base font-semibold mb-3">Aggiorna paragrafo corrente</h2>
          <form onSubmit={handleSubmit} className="flex items-end gap-3">
            <label className="flex flex-col text-sm flex-1 max-w-xs">
              <span className="mb-1 text-muted-foreground">Numero paragrafo (§)</span>
              <input
                type="number"
                min={0}
                value={paragraphInput}
                onChange={e => setParagraphInput(e.target.value)}
                className="rounded-md border border-input bg-background px-3 py-1.5"
                placeholder="es. 47"
                data-testid="gamebook-paragraph-input"
              />
            </label>
            <button
              type="submit"
              disabled={updateProgress.isPending || !paragraphInput}
              className="rounded-md bg-[var(--c-game)] px-4 py-1.5 text-sm font-medium text-white disabled:opacity-50"
              data-testid="gamebook-paragraph-submit"
            >
              {updateProgress.isPending ? 'Salvataggio…' : 'Salva'}
            </button>
          </form>
          {data.history.length > 0 && (
            <p className="mt-3 text-xs text-muted-foreground">
              Storia: {data.history.slice(-10).join(' → ')}
            </p>
          )}
        </section>
      </main>
    </div>
  );
}
