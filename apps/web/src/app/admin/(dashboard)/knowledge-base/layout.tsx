import { type ReactNode } from 'react';

import { KbSubNav } from '@/components/admin/knowledge-base/explorer/KbSubNav';

/**
 * Layout condiviso di tutte le route /admin/knowledge-base/*.
 * Inserisce la sub-nav KB sopra il contenuto di ogni sub-page; le pagine
 * esistenti restano invariate (ereditano la sub-nav senza modifiche).
 *
 * Padding orizzontale `px-6`: la KbSubNav usa `-mx-6 px-6` per bleed
 * full-width del border-bottom; questo wrapper deve fornire i 24px che
 * compensano il margine negativo.
 */
export default function KnowledgeBaseLayout({ children }: { children: ReactNode }) {
  return (
    <div className="space-y-6 px-6">
      <KbSubNav />
      {children}
    </div>
  );
}
