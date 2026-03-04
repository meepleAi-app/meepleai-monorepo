'use client';

/**
 * GameDetailAgentTab — Agent chat CTA for the game detail page
 */

import { Bot, MessageCircle } from 'lucide-react';
import { useRouter } from 'next/navigation';

import { Button } from '@/components/ui/primitives/button';

export interface GameDetailAgentTabProps {
  gameId: string;
  gameTitle: string;
}

export function GameDetailAgentTab({ gameId, gameTitle }: GameDetailAgentTabProps) {
  const router = useRouter();

  return (
    <div className="rounded-2xl border border-border/40 bg-card p-6 text-center">
      <div className="mx-auto mb-4 flex h-14 w-14 items-center justify-center rounded-full bg-primary/10">
        <Bot className="h-7 w-7 text-primary" />
      </div>
      <h2 className="mb-2 font-quicksand text-lg font-bold text-foreground">
        Agente AI per {gameTitle}
      </h2>
      <p className="mx-auto mb-6 max-w-md font-nunito text-sm text-muted-foreground">
        Chiedi regole, strategie, chiarimenti e consigli al tuo agente AI dedicato a questo gioco.
        L&apos;agente ha accesso alla knowledge base e ai documenti caricati.
      </p>
      <Button
        size="lg"
        onClick={() => router.push(`/chat?gameId=${gameId}`)}
        className="font-quicksand font-semibold"
      >
        <MessageCircle className="mr-2 h-4 w-4" />
        Avvia Chat
      </Button>
    </div>
  );
}
