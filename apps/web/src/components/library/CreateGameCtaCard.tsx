'use client';

import { Plus } from 'lucide-react';
import { useRouter } from 'next/navigation';

import { cn } from '@/lib/utils';

// ── CTA card — "Crea gioco" at the end of the custom games section ──────────

export function CreateGameCtaCard() {
  const router = useRouter();

  return (
    <div
      className={cn(
        'flex flex-col items-center justify-center gap-2',
        'w-full sm:w-[140px] flex-shrink-0 rounded-xl',
        'bg-[#161b22] border border-dashed border-[#30363d]',
        'min-h-[80px] sm:min-h-[160px]',
        'cursor-pointer transition-all duration-200',
        'hover:border-[#58a6ff] hover:bg-[#1c2128]',
        'focus:outline-none focus:ring-1 focus:ring-[#58a6ff]'
      )}
      onClick={() => router.push('/library/private/add')}
      onKeyDown={e => {
        if (e.key === 'Enter' || e.key === ' ') {
          e.preventDefault();
          router.push('/library/private/add');
        }
      }}
      role="button"
      tabIndex={0}
      aria-label="Crea un gioco personalizzato"
      data-testid="create-game-cta"
    >
      <Plus className="w-5 h-5 text-[#58a6ff]" aria-hidden="true" />
      <span className="text-[10px] font-medium text-[#58a6ff] text-center px-2">Crea gioco</span>
    </div>
  );
}
