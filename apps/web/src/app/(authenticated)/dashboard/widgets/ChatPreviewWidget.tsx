'use client';

import { useRouter } from 'next/navigation';

import { BentoWidget, WidgetLabel } from './BentoWidget';
import { C } from './dashboard-colors';

export function ChatPreviewWidget() {
  const router = useRouter();
  return (
    <BentoWidget
      colSpan={6}
      rowSpan={4}
      accentColor={C.chat}
      className="flex flex-col"
      onClick={() => router.push('/chat')}
    >
      <div className="flex items-center justify-between mb-2">
        <WidgetLabel>Chat AI</WidgetLabel>
        <span
          className="text-[9px] font-bold rounded-full px-2 py-0.5"
          style={{ background: `${C.chat}20`, color: C.chat }}
        >
          Regole & Domande
        </span>
      </div>
      <div className="flex-1 flex flex-col items-center justify-center gap-3 text-center px-4">
        <div
          className="w-12 h-12 rounded-2xl flex items-center justify-center text-2xl"
          style={{ background: `${C.chat}15` }}
        >
          💬
        </div>
        <div>
          <p className="font-quicksand font-bold text-sm text-foreground">Chiedi all&apos;AI</p>
          <p className="text-[11px] text-muted-foreground mt-0.5">
            Regole, strategie e suggerimenti per i tuoi giochi da tavolo
          </p>
        </div>
      </div>
      <div
        className="mt-auto pt-2 flex gap-1.5"
        onClick={e => {
          e.stopPropagation();
          router.push('/chat');
        }}
      >
        <div
          className="flex-1 h-7 rounded-lg flex items-center px-2.5 text-[11px] text-muted-foreground/50"
          style={{
            background: 'rgba(255,255,255,0.03)',
            border: '1px solid rgba(255,255,255,0.07)',
          }}
        >
          Fai una domanda…
        </div>
        <button
          type="button"
          className="w-7 h-7 rounded-lg flex items-center justify-center text-white text-sm"
          style={{ background: C.chat }}
          aria-label="Vai alla chat"
          onClick={e => {
            e.stopPropagation();
            router.push('/chat');
          }}
        >
          ↑
        </button>
      </div>
    </BentoWidget>
  );
}
